import math

from BaseClasses import MultiWorld, CollectionState, ItemClassification
from .locations import bestiary_moons, scrap_moons, scrap_names
from typing import TYPE_CHECKING
from .options import LCOptions
from .items import moons, shop_items

if TYPE_CHECKING:
    from . import LethalCompanyWorld


def has_location_access_rule(multiworld: MultiWorld, moon: str, player: int, item_number: int, options: LCOptions) \
        -> None:
    if item_number == 1:
        multiworld.get_location(f"{moon} check {item_number}", player).access_rule = \
            lambda state: (state.has(moon, player) and
                           ((state.has("Inventory Slot", player) or options.starting_inventory_slots.value >= 2) and
                            (state.has("Stamina Bar", player) or options.starting_stamina_bars.value >= 1)))
    else:
        multiworld.get_location(f"{moon} check {item_number}", player).access_rule = \
            lambda state: check_location(moon=moon, player=player, state=state, item_number=item_number)


def has_quota_access_rule(multiworld: MultiWorld, player: int, item_number: int, options: LCOptions) \
        -> None:
    if item_number == 1:
        multiworld.get_location(f"Quota check {item_number}", player).access_rule = \
            lambda state: ((state.has("Inventory Slot", player) or options.starting_inventory_slots.value >= 2) and
                           (state.has("Stamina Bar", player) or options.starting_stamina_bars.value >= 1))
    elif item_number == math.ceil(options.num_quotas.value / 4.0):
        multiworld.get_location(f"Quota check {item_number}", player).access_rule = lambda state: check_quota(
            player=player, state=state, item_number=item_number) and state.has("Completed 25% Quota", player)
    elif item_number == math.ceil(options.num_quotas.value / 2.0):
        multiworld.get_location(f"Quota check {item_number}", player).access_rule = lambda state: check_quota(
            player=player, state=state, item_number=item_number) and state.has("Completed 50% Quota", player)
    elif item_number == math.ceil(3.0 * options.num_quotas.value / 4.0):
        multiworld.get_location(f"Quota check {item_number}", player).access_rule = lambda state: check_quota(
            player=player, state=state, item_number=item_number) and state.has("Completed 75% Quota", player)
    else:
        multiworld.get_location(f"Quota check {item_number}", player).access_rule = \
            lambda state: check_quota(player=player, state=state, item_number=item_number)


def check_location(state, moon: str, player: int, item_number: int) -> None:
    return state.can_reach(f"{moon} check {item_number - 1}", "Location", player)


def check_quota(state, player: int, item_number: int) -> None:
    return state.can_reach(f"Quota check {item_number - 1}", "Location", player)


def set_rules(lc_world) -> None:
    player = lc_world.player
    multiworld = lc_world.multiworld
    options: LCOptions = lc_world.options
    for moon in moons:
        for i in range(options.checks_per_moon.value):
            has_location_access_rule(multiworld, moon, player, i + 1, options)

    for i in range(options.num_quotas.value):
        has_quota_access_rule(multiworld, player, i + 1, options)

    multiworld.get_location("Log - Smells Here!", player).access_rule = lambda state: state.has("Assurance", player)
    multiworld.get_location("Log - Swing of Things", player).access_rule = \
        lambda state: state.has("Experimentation", player)
    multiworld.get_location("Log - Shady", player).access_rule = \
        lambda state: (state.has("Experimentation", player)
                       and (state.has("Stamina Bar", player)
                            or options.starting_stamina_bars.value >= 1))
    multiworld.get_location("Log - Golden Planet", player).access_rule = lambda state: state.has("Rend", player)
    multiworld.get_location("Log - Goodbye", player).access_rule = lambda state: state.has("March", player)
    multiworld.get_location("Log - Screams", player).access_rule = \
        lambda state: state.has("Vow", player) or state.has("March", player)
    multiworld.get_location("Log - Idea", player).access_rule = lambda state: state.has("Rend", player)
    multiworld.get_location("Log - Nonsense", player).access_rule = lambda state: state.has("Rend", player)
    multiworld.get_location("Log - Hiding", player).access_rule = lambda state: state.has("Dine", player)
    multiworld.get_location("Log - Real Job", player).access_rule = \
        lambda state: ((state.has("Extension ladder", player)
                        or state.has("Jetpack", player))
                       and state.has("Titan", player))
    multiworld.get_location("Log - Desmond", player).access_rule = \
        lambda state: state.has("Jetpack", player) and state.has("Titan", player)
    multiworld.get_location("Victory", player).access_rule = \
        lambda state: state.has_all(shop_items, player) and state.has_all(moons, player) and state.has(
            "Progressive Flashlight", player, count=2)
    multiworld.get_location("Quota 25%", player).access_rule = \
        lambda state: ((state.has("Inventory Slot", player) or options.starting_inventory_slots.value >= 2) and
                       (state.has("Stamina Bar", player) or options.starting_stamina_bars.value >= 1))
    multiworld.get_location("Quota 50%", player).access_rule = \
        lambda state: ((state.has("Inventory Slot", player) or options.starting_inventory_slots.value >= 2) and
                       (state.has("Stamina Bar", player) or options.starting_stamina_bars.value >= 1)) and state.has(
            "Completed 25% Quota", player)
    multiworld.get_location("Quota 75%", player).access_rule = \
        lambda state: ((state.has("Inventory Slot", player) or options.starting_inventory_slots.value >= 2) and
                       (state.has("Stamina Bar", player) or options.starting_stamina_bars.value >= 1)) and state.has(
            "Completed 25% Quota", player) and state.has("Completed 50% Quota", player)
    for entry in bestiary_moons:
        cant_spawn = bestiary_moons[entry]
        can_spawn = [moon for moon in moons]
        for moon in cant_spawn:
            can_spawn.remove(moon)
        multiworld.get_location(f"Bestiary Entry - {entry}", player).access_rule = \
            lambda state: has_multi(state, can_spawn, player) and (state.has("Scanner", player)
                                                                   or options.randomize_scanner.value == 0)

    if options.scrapsanity.value == 1:
        for scrap_index in range(len(scrap_names)):
            possible_moons = []
            for moon in moons:
                if scrap_index in scrap_moons[moon]:
                    possible_moons.append(moon)
            multiworld.get_location(f"Scrap - {scrap_names[scrap_index]}", player).access_rule = \
                lambda state, _possible_moons=possible_moons, _scrap_name=scrap_names[scrap_index]: (state.has_any(
                    _possible_moons, player) and (state.has("Stamina Bar", player) or
                                                  options.starting_stamina_bars.value > 0)
                                             and (state.has("Shovel", player) or _scrap_name != "Double-barrel"))
            if scrap_names[scrap_index] == "Bee Hive" and options.exclude_hive.value == 1:
                multiworld.get_location("Scrap - Bee Hive", player).item_rule = lambda item: not \
                    (item.classification == ItemClassification.progression or
                     item.classification == ItemClassification.useful)

            if scrap_names[scrap_index] == "Double-barrel" and options.exclude_shotgun.value == 1:
                multiworld.get_location("Scrap - Double-barrel", player).item_rule = lambda item: not \
                    (item.classification == ItemClassification.progression or
                     item.classification == ItemClassification.useful)

            if scrap_names[scrap_index] == "Gold bar":
                multiworld.get_location("Scrap - Gold bar", player).item_rule = lambda item: not \
                    (item.classification == ItemClassification.progression or
                     item.classification == ItemClassification.useful)

    multiworld.completion_condition[player] = lambda state: state.has("Victory", player)


def has_multi(state: CollectionState, items, player):
    success = False
    for item in items:
        success = success or state.has(item, player)
    return success


def has_all(state, items, player):
    success = True
    for item in items:
        success = success and state.has(item, player)
    return success
