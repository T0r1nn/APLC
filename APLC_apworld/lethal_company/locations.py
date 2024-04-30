import math
from typing import Dict, List, TYPE_CHECKING
from BaseClasses import Location
from .options import ChecksPerMoon, NumQuotas
from .imported import data

if TYPE_CHECKING:
    from . import LethalCompanyWorld


class LethalCompanyLocation(Location):
    game: str = "Lethal Company"


lc_locations_start_id = 1966720

log_names = [
    "Smells Here!",
    "Swing of Things",
    "Shady",
    "Golden Planet",
    "Sound Behind the Wall",
    "Goodbye",
    "Screams",
    "Idea",
    "Nonsense",
    "Hiding",
    "Real Job",
    "Desmond"
]

bestiary_names = [[key for key in monster.keys()][0] for monster in data["bestiary"]]

moons = [" ".join(moon.split(" ")[1:]) for moon in data.get("moons")]

scrap_names = []

for item in data["scrap"]:
    key = [key for key in item.keys()][0]
    scrap_names.append(key)

for moon in moons:
    if not f"AP Apparatus - {moon}" in scrap_names:
        scrap_names.append(f"AP Apparatus - {moon}")


def generate_bestiary_moons(chance: float) -> Dict[str, List[str]]:
    bestiary_moons = {

    }

    bestiary_data = data["bestiary"]
    for entry in bestiary_data:
        key = [key for key in entry.keys()][0]
        b_moons = []
        for moon in entry[key]:
            if moon["chance"] > chance:
                b_moons.append(" ".join(moon["moon_name"].split(" ")[1:]))
        bestiary_moons[key] = b_moons

    return bestiary_moons


def generate_scrap_moons(chance: float) -> Dict[str, List[str]]:
    scrap_moons = {

    }

    scrap_data = data["scrap"]
    for entry in scrap_data:
        key = [key for key in entry.keys()][0]
        s_moons = []
        for moon in entry[key]:
            if moon["chance"] > chance:
                s_moons.append(" ".join(moon["moon_name"].split(" ")[1:]))
        scrap_moons[key] = s_moons

    return scrap_moons


def generate_scrap_moons_alt(world: 'LethalCompanyWorld') -> Dict[str, List[str]]:
    scrap_moons = {

    }

    normal = generate_scrap_moons(chance=world.options.min_scrap_chance.value/100)

    scrap = [name for name in scrap_names]

    for name in scrap:
        if "AP Apparatus" in name:
            scrap.remove(name)
        elif "Archipelago Chest" in name:
            scrap.remove(name)
        elif "Apparatus" in name or "Shotgun" in name or "Knife" in name or "Hive" in name:
            scrap.remove(name)

    items_per_bin = math.floor(len(scrap) / len(moons))
    world.multiworld.random.shuffle(scrap)

    for i in range(len(scrap)):
        bin_num = math.floor(i/items_per_bin)
        if bin_num < len(moons):
            scrap_moons[scrap[i]] = [moons[bin_num]]
        else:
            scrap_moons[scrap[i]] = ["Common"]

    scrap_moons["Archipelago Chest"] = []
    scrap_moons["Apparatus"] = normal["Apparatus"]
    scrap_moons["Shotgun"] = normal["Shotgun"]
    scrap_moons["Knife"] = normal["Knife"]
    scrap_moons["Hive"] = normal["Hive"]

    for moon in moons:
        scrap_moons[f"AP Apparatus - {moon}"] = [moon]
        scrap_moons["Archipelago Chest"].append(moon)

    world.scrap_map = scrap_moons

    return scrap_moons


def generate_locations(checks_per_moon: int, num_quota: int, scrapsanity: int) -> Dict[str, int]:
    locations = {}
    offset = lc_locations_start_id
    for i in range(len(moons)):
        for j in range(checks_per_moon):
            locations.update({f"{moons[i]} check {j + 1}": j + i * checks_per_moon + offset})
    offset += len(moons) * checks_per_moon
    for i in range(num_quota):
        locations.update({f"Quota check {i + 1}": offset + i})
    offset += num_quota
    for i in range(len(log_names)):
        locations.update({f"Log - {log_names[i]}": offset + i})
    offset += len(log_names)
    for i in range(len(bestiary_names)):
        locations.update({f"Bestiary Entry - {bestiary_names[i]}": offset + i})
    offset += len(bestiary_names)
    if scrapsanity == 1:
        for i in range(len(scrap_names)):
            locations.update({f"Scrap - {scrap_names[i]}": offset + i})
    return locations


max_locations = generate_locations(ChecksPerMoon.range_end, NumQuotas.range_end, 1)
