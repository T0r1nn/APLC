from typing import Dict, List, NamedTuple, Optional

from BaseClasses import MultiWorld, Region, Entrance
from .locations import LethalCompanyLocation, bestiary_moons, max_locations, bestiary_names, scrap_names
from .options import LCOptions


class LCRegionData(NamedTuple):
    locations: Optional[List[str]]
    region_exits: Optional[List[str]]


def create_regions(options: LCOptions, multiworld: MultiWorld, player: int):
    # Default Locations
    regions: Dict[str, LCRegionData] = {
        "Menu": LCRegionData(None, ["Ship"]),
        "Ship": LCRegionData([], ["Experimentation", "Assurance", "Vow", "Offense", "March", "Dine", "Rend", "Titan",
                                  "The Company"]),
        "Experimentation": LCRegionData(["Log - Swing of Things", "Log - Shady"], []),
        "Assurance": LCRegionData(["Log - Smells Here!"], []),
        "Vow": LCRegionData([], ["Screams"]),
        "Offense": LCRegionData([], []),
        "March": LCRegionData(["Log - Goodbye"], ["Screams"]),
        "Dine": LCRegionData(["Log - Hiding"], []),
        "Rend": LCRegionData(["Log - Nonsense", "Log - Golden Planet", "Log - Idea"], []),
        "Titan": LCRegionData(["Log - Real Job", "Log - Desmond"], []),
        "The Company": LCRegionData(["Log - Sound Behind the Wall"], ["Victory", "Quota"]),
        "Victory": LCRegionData(None, None),
        "Quota": LCRegionData(None, None),
        "Snare Flea": LCRegionData(["Bestiary Entry - Snare Flea"], []),
        "Bunker Spider": LCRegionData(["Bestiary Entry - Bunker Spider"], []),
        "Hoarding Bug": LCRegionData(["Bestiary Entry - Hoarding Bug"], []),
        "Bracken": LCRegionData(["Bestiary Entry - Bracken"], []),
        "Thumper": LCRegionData(["Bestiary Entry - Thumper"], []),
        "Hygrodere": LCRegionData(["Bestiary Entry - Hygrodere"], []),
        "Spore Lizard": LCRegionData(["Bestiary Entry - Spore Lizard"], []),
        "Nutcracker": LCRegionData(["Bestiary Entry - Nutcracker"], []),
        "Coil-Head": LCRegionData(["Bestiary Entry - Coil-Head"], []),
        "Jester": LCRegionData(["Bestiary Entry - Jester"], []),
        "Eyeless Dog": LCRegionData(["Bestiary Entry - Eyeless Dog"], []),
        "Forest Keeper": LCRegionData(["Bestiary Entry - Forest Keeper"], []),
        "Earth Leviathan": LCRegionData(["Bestiary Entry - Earth Leviathan"], []),
        "Baboon Hawk": LCRegionData(["Bestiary Entry - Baboon Hawk"], []),
        "Circuit Bee": LCRegionData(["Bestiary Entry - Circuit Bee"], []),
        "Manticoil": LCRegionData(["Bestiary Entry - Manticoil"], []),
        "Roaming Locust": LCRegionData(["Bestiary Entry - Roaming Locust"], []),
        "Screams": LCRegionData(["Log - Screams"], [])
    }
    # Totals of each item
    per_moon = int(options.checks_per_moon.value)
    num_quota = int(options.num_quotas.value)

    # Locations
    for key in regions:
        if (key == "Menu" or key == "Victory" or key == "Ship" or key in bestiary_names or key == "Screams"
                or key == "Quota"):
            continue
        if key == "The Company":
            for i in range(num_quota):
                regions[key].locations.append(f"Quota check {i+1}")
            continue
        for i in range(per_moon):
            regions[key].locations.append(f"{key} check {i+1}")
        for beast in bestiary_moons:
            invalid_moons = bestiary_moons[beast]
            if key not in invalid_moons:
                regions[key].region_exits.append(f"{beast}")

    if options.scrapsanity.value == 1:
        for scrap_name in scrap_names:
            regions["Ship"].locations.append(f"Scrap - {scrap_name}")

    regions_pool: Dict = regions

    # Create all the regions
    for name, data in regions_pool.items():
        multiworld.regions.append(create_region(multiworld, player, name, data))

    # Connect all the regions to their exits
    for name, data in regions_pool.items():
        create_connections_in_regions(multiworld, player, name, data)


def create_region(multiworld: MultiWorld, player: int, name: str, data: LCRegionData):
    region = Region(name, player, multiworld)
    if data.locations:
        for location_name in data.locations:
            location_data = max_locations.get(location_name)
            location = LethalCompanyLocation(player, location_name, location_data, region)
            region.locations.append(location)
    return region


def create_connections_in_regions(multiworld: MultiWorld, player: int, name: str, data: LCRegionData):
    region = multiworld.get_region(name, player)
    if data.region_exits:
        for region_exit in data.region_exits:
            r_exit_stage = Entrance(player, region_exit, region)
            exit_region = multiworld.get_region(region_exit, player)
            r_exit_stage.connect(exit_region)
            region.exits.append(r_exit_stage)
