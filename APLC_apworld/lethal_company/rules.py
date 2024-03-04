import math
import string

from BaseClasses import MultiWorld, CollectionState, ItemClassification
from .locations import scrap_names
from .options import LCOptions
from .items import moons


def has_location_access_rule(multiworld: MultiWorld, moon: str, player: int, item_number: int, options: LCOptions,
                             lc_world) -> None:
    if item_number == 1:
        multiworld.get_location(f"{moon} check {item_number}", player).access_rule = \
            lambda state: ((state.has("Inventory Slot", player) or options.starting_inventory_slots.value >= 2) and
                            (state.has("Stamina Bar", player) or options.starting_stamina_bars.value >= 1))
    else:
        multiworld.get_location(f"{moon} check {item_number}", player).access_rule = \
            lambda state: check_location(moon=moon, player=player, state=state, item_number=item_number)


def has_quota_access_rule(multiworld: MultiWorld, player: int, item_number: int, options: LCOptions,
                          lc_world) -> None:
    if item_number == 1:
        return
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
            has_location_access_rule(multiworld, moon, player, i + 1, options, lc_world)
    
    for i in range(options.num_quotas.value):
        has_quota_access_rule(multiworld, player, i + 1, options, lc_world)

    multiworld.get_location("Quota 50%", player).access_rule = \
        lambda state: state.has("Completed 25% Quota", player)
    multiworld.get_location("Quota 75%", player).access_rule = \
        lambda state: state.has("Completed 50% Quota", player)

    if options.scrapsanity.value == 1:
        for scrap_index in range(len(scrap_names)):
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


def has_multi_moon(state: CollectionState, items, player, options, world):
    success = False
    for item in items:
        success = success or check_moon_accessible(state, item, player, options, world)
    return success


def has_all(state, items, player):
    success = True
    for item in items:
        success = success and state.has(item, player)
    return success


def check_item_accessible(state: CollectionState, item: string, player: int, options: LCOptions):
    return (state.has(item, player) and (state.has("Terminal", player) or options.randomize_terminal.value == 0)
            and (state.has("Company Building", player) or options.randomize_company_building.value == 0))


def check_moon_accessible(state: CollectionState, moon: string, player: int,
                          options: LCOptions, world):
    return state.has(moon, player) and (state.has("Terminal", player) or
                                        (options.randomize_terminal.value == 0 or world.initial_world == moon))
