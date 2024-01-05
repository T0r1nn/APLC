from worlds.generic.Rules import set_rule, add_rule
from BaseClasses import MultiWorld
from .locations import generate_locations, bestiary_moons
from .lcenvironments import moons
from typing import Set, TYPE_CHECKING

if TYPE_CHECKING:
    from . import LethalCompanyWorld


def has_location_access_rule(multiworld: MultiWorld, moon: str, player: int, item_number: int) \
        -> None:
    if item_number == 1:
        multiworld.get_location(f"{moon} check {item_number}", player).access_rule = \
            lambda state: state.has(moon, player)
    else:
        multiworld.get_location(f"{moon} check {item_number}", player).access_rule = \
            lambda state: check_location(moon=moon, player=player, state=state, item_number=item_number)


def check_location(state, moon: str, player: int, item_number: int) -> None:
    return state.can_reach(f"{moon} check {item_number - 1}", "Location", player)


def set_rules(lc_world) -> None:
    player = lc_world.player
    multiworld = lc_world.multiworld
    total_locations = len(generate_locations(
        checks_per_moon=multiworld.checks_per_moon[player].value,
        num_quota=multiworld.num_quotas[player].value
    ))
    for moon in moons:
        for i in range(multiworld.checks_per_moon[player].value):
            has_location_access_rule(multiworld, moon, player, i+1)
    multiworld.get_location("Log - Smells Here!", player).access_rule = lambda state: state.has("Assurance", player)
    multiworld.get_location("Log - Swing of Things", player).access_rule = \
        lambda state: state.has("Experimentation", player)
    multiworld.get_location("Log - Shady", player).access_rule = lambda state: state.has("Experimentation", player)
    multiworld.get_location("Log - Screams", player).access_rule = \
        lambda state: state.has("Vow", player) or state.has("March", player)
    multiworld.get_location("Log - Nonsense", player).access_rule = lambda state: state.has("Rend", player)
    for entry in bestiary_moons:
        cant_spawn = bestiary_moons[entry]
        can_spawn = [moon for moon in moons]
        for moon in cant_spawn:
            can_spawn.remove(moon)
        multiworld.get_location(f"Bestiary Entry - {entry}", player).access_rule = \
            lambda state: has_multi(state, can_spawn, player)

    multiworld.completion_condition[player] = lambda state: state.has("Victory", player)


def has_multi(state, items, player):
    success = False
    for item in items:
        success = success or state.has(item, player)
    return success
