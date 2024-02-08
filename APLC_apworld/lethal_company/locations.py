from typing import Dict
from BaseClasses import Location
from .options import ChecksPerMoon, NumQuotas


class LethalCompanyLocation(Location):
    game: str = "Lethal Company"


lc_locations_start_id = 1966720

log_offset = 12
bestiary_offset = 17

moon_names = [
    "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan"
]

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

scrap_names = [
    "Airhorn", "Apparatice", "Bee Hive", "Big bolt", "Bottles", "Brass bell", "Candy", "Cash register",
    "Chemical jug", "Clown horn", "Coffee mug", "Comedy", "Cookie mold pan", "DIY-Flashbang", "Double-barrel", "Dust pan",
    "Egg beater", "Fancy lamp", "Flask", "Gift Box", "Gold bar", "Golden cup", "Hair brush", "Hairdryer",
    "Jar of pickles", "Large axle", "Laser pointer", "Magic 7 ball", "Magnifying glass", "Old phone",
    "Painting", "Perfume bottle", "Pill bottle", "Plastic fish", "Red soda", "Remote", "Ring", "Robot toy",
    "Rubber Ducky", "Steering wheel", "Stop sign", "Tattered metal sheet", "Tea kettle", "Teeth", "Toothpaste",
    "Toy cube", "Tragedy", "V-type engine", "Whoopie-Cushion", "Yield sign"
]

scrap_moons = {
    "Experimentation": [47, 41, 25, 3, 18, 39, 15, 13, 4, 19, 33, 16, 24, 49, 12, 1, 2],
    "Assurance": [3, 4, 12, 47, 40, 16, 42, 25, 5, 22, 45, 41, 28, 24, 19, 49, 13, 35, 39, 34, 18, 10, 48, 33, 9, 0, 26,
                  29, 37, 1, 2],
    "Vow": [16, 12, 18, 48, 22, 8, 47, 40, 5, 33, 42, 3, 25, 4, 28, 38, 19, 24, 35, 45, 41, 49, 13, 10, 34, 39, 0, 9,
            26, 29, 1, 2],
    "Offense": [41, 25, 3, 47, 4, 33, 40, 42, 19, 9, 35, 0, 28, 45, 49, 43, 13, 26, 37, 29, 12, 1],
    "March": [25, 3, 41, 47, 4, 9, 18, 0, 40, 33, 42, 19, 45, 35, 12, 49, 1, 2],
    "Rend": [30, 11, 17, 5, 37, 4, 10, 23, 28, 45, 43, 31, 34, 22, 42, 46, 21, 44, 27, 36, 19, 29, 33, 38, 7, 6, 0, 9,
             14],
    "Dine": [46, 30, 17, 5, 37, 4, 10, 23, 28, 45, 43, 11, 31, 34, 22, 42, 21, 44, 27, 19, 36, 48, 29, 33, 38, 6, 7, 0,
             9, 13, 14],
    "Titan": [3, 46, 11, 47, 34, 25, 5, 44, 37, 30, 4, 22, 23, 42, 17, 21, 36, 48, 28, 38, 0, 29, 10, 43, 45, 19, 33,
              27, 9, 32, 31, 26, 13, 24, 1, 14]
}

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


def generate_locations(checks_per_moon: int, num_quota: int, scrapsanity: int) -> Dict[str, int]:
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
    offset += bestiary_offset
    if scrapsanity == 1:
        for i in range(len(scrap_names)):
            locations.update({f"Scrap - {scrap_names[i]}": offset + i})
    return locations


max_locations = generate_locations(ChecksPerMoon.range_end, NumQuotas.range_end, 1)
