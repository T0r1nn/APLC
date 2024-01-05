from typing import Dict
from BaseClasses import Location
from .options import ChecksPerMoon, NumQuotas, EnableInventoryUnlock
from .lcenvironments import moons, shift_by_offset


class LethalCompanyLocation(Location):
    game: str = "Lethal Company"


lc_locations_start_id = 100000

log_offset = 6
bestiary_offset = 17

moon_names = [
    "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan"
]

log_names = [
    "Smells Here!",
    "Swing of Things",
    "Shady",
    "Sound Behind the Wall",
    "Screams",
    "Nonsense"
]

bestiary_names = [
    "Snare Flea",
    "Bunker Spider",
    "Hoarding Bug",
    "Bracken",
    "Thumper",
    "Hygrodere",
    "Spore Lizard",
    "Nutcracker",
    "Coil-Head",
    "Jester",
    "Eyeless Dog",
    "Forest Keeper",
    "Earth Leviathan",
    "Baboon Hawk",
    "Circuit Bee",
    "Manticoil",
    "Roaming Locust"
]

bestiary_moons = {
    "Snare Flea": [],
    "Bunker Spider": [],
    "Hoarding Bug": ["Rend"],
    "Bracken": [],
    "Thumper": ["Rend"],
    "Hygrodere": [],
    "Spore Lizard": [],
    "Nutcracker": ["Experimentation", "Assurance", "Offense", "March", "Vow"],
    "Coil-Head": ["Experimentation", "Assurance"],
    "Jester": ["Experimentation", "Assurance", "Vow", "Offense", "March"],
    "Eyeless Dog": ["Experimentation", "Assurance", "Vow", "Offense", "March", "Dine"],
    "Forest Keeper": ["Experimentation", "Assurance"],
    "Earth Leviathan": [],
    "Baboon Hawk": ["Experimentation", "Rend", "Dine", "Titan"],
    "Circuit Bee": ["Offense", "Rend", "Dine", "Titan"],
    "Manticoil": ["Rend", "Dine", "Titan"],
    "Roaming Locust": ["Offense", "Rend", "Dine", "Titan"]
}


def generate_locations(checks_per_moon: int, num_quota: int) -> Dict[str, int]:
    locations = {}
    offset = lc_locations_start_id
    for i in range(len(moon_names)):
        for j in range(checks_per_moon):
            locations.update({f"{moon_names[i]} check {j+1}": j + i * checks_per_moon + offset})
    offset += 8 * checks_per_moon
    for i in range(num_quota):
        locations.update({f"Quota check {i+1}": offset + i})
    offset += num_quota
    for i in range(len(log_names)):
        locations.update({f"Log - {log_names[i]}": offset + i})
    offset += log_offset
    for i in range(len(bestiary_names)):
        locations.update({f"Bestiary Entry - {bestiary_names[i]}": offset + i})
    return locations


max_locations = generate_locations(ChecksPerMoon.range_end, NumQuotas.range_end)
