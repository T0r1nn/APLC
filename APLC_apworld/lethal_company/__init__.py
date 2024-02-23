import string

from .items import LethalCompanyItem, item_table, items, filler_items, classification_table
from .locations import LethalCompanyLocation, generate_locations, max_locations, moon_names
from .rules import set_rules
from BaseClasses import Item, ItemClassification, Tutorial, MultiWorld, Region
from .options import LCOptions
from worlds.AutoWorld import World, WebWorld
from typing import List
from .regions import create_regions


class LethalCompanyWeb(WebWorld):
    tutorials = [Tutorial(
        "Multiworld Setup Guide",
        "A guide to setting up the Lethal Company integration for Archipelago multiworld games.",
        "English",
        "setup_en.md",
        "setup/en",
        ["T0r1nn"]
    )]


class LethalCompanyWorld(World):
    """
    Welcome, new hire! The company needs you to go to a bunch of perfectly harmless moons to gather scrap.
    We thank you for your efforts. There is nothing to worry about, absolutely zero danger to your health.
    """
    game = "Lethal Company"
    options_dataclass = LCOptions
    options: LCOptions
    topology_present = False

    item_name_to_id = item_table
    location_name_to_id = max_locations

    data_version = 7
    required_client_version = (0, 4, 2)
    web = LethalCompanyWeb()
    total_revivals: int

    def __init__(self, multiworld, player: int):
        super().__init__(multiworld, player)

    def create_items(self) -> None:
        # precollect one moon
        environment_pool = ["Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan"]

        unlock = None
        starting_moon = self.options.starting_moon.value
        if starting_moon < 8:
            unlock = [moon_names[starting_moon]]
        else:
            unlock = self.multiworld.random.choices(environment_pool, k=1)
        self.multiworld.push_precollected(self.create_item(unlock[0]))
        environment_pool.pop(environment_pool.index(unlock[0]))

        # Generate item pool
        itempool: List = []

        for item in items:
            names = item.create_item(self)
            for name in names:
                if not name == unlock[0]:
                    itempool.append(name)

        total_locations = len(
            generate_locations(
                checks_per_moon=self.options.checks_per_moon.value,
                num_quota=self.options.num_quotas.value,
                scrapsanity=self.options.scrapsanity.value
            )
        )

        # Fill remaining items with randomly generated junk
        while len(itempool) < total_locations:
            itempool.append(self.get_filler_item_name())

        # Convert itempool into real items
        itempool = list(map(lambda name: self.create_item(name), itempool))
        self.multiworld.itempool += itempool

    def set_rules(self) -> None:
        set_rules(self)

    def get_filler_item_name(self) -> str:
        weights = [data for data in filler_items.values()]
        filler = self.multiworld.random.choices([filler for filler in filler_items.keys()], weights,
                                                k=1)[0]
        return filler

    def create_regions(self) -> None:
        create_regions(self.options, self.multiworld, self.player)
        create_events(self.multiworld, self.player)

    def fill_slot_data(self):
        slot_data = {
            "deathLink": self.options.death_link.value
        }

        for option in dir(self.options):
            if hasattr(getattr(self.options, option), "slot"):
                if getattr(self.options, option).slot:
                    print(option, getattr(self.options, option).slot_name, getattr(self.options, option).value)
                    slot_data[getattr(self.options, option).slot_name] = getattr(self.options, option).value

        return slot_data

    def create_item(self, name: str) -> Item:
        item_id = item_table[name]
        classification = classification_table.get(name)
        item = LethalCompanyItem(name, classification, item_id, self.player)
        return item


def create_events(world: MultiWorld, player: int) -> None:
    world_region = world.get_region("The Company", player)
    victory_region = world.get_region("Victory", player)
    victory_event = LethalCompanyLocation(player, "Victory", None, victory_region)
    quota_region = world.get_region("Quota", player)
    quota_quarter1_event = LethalCompanyLocation(player, "Quota 25%", None, quota_region)
    quota_quarter2_event = LethalCompanyLocation(player, "Quota 50%", None, quota_region)
    quota_quarter3_event = LethalCompanyLocation(player, "Quota 75%", None, quota_region)
    victory_event.place_locked_item(LethalCompanyItem("Victory", ItemClassification.progression, None, player))
    quota_quarter1_event.place_locked_item(LethalCompanyItem("Completed 25% Quota", ItemClassification.progression,
                                                             None, player))
    quota_quarter2_event.place_locked_item(LethalCompanyItem("Completed 50% Quota", ItemClassification.progression,
                                                             None, player))
    quota_quarter3_event.place_locked_item(LethalCompanyItem("Completed 75% Quota", ItemClassification.progression,
                                                             None, player))
    quota_region.locations.append(quota_quarter1_event)
    quota_region.locations.append(quota_quarter2_event)
    quota_region.locations.append(quota_quarter3_event)
    world_region.locations.append(victory_event)


def create_region(world: MultiWorld, player: int, name: str, loc=None) -> Region:
    if loc is None:
        loc = {}
    ret = Region(name, player, world)
    for location_name, location_id in loc.items():
        ret.locations.append(LethalCompanyLocation(player, location_name, location_id, ret))
    return ret
