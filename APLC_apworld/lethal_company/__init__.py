import string

from .items import LethalCompanyItem, item_table, shop_items
from .locations import LethalCompanyLocation, generate_locations, max_locations, moon_names
from .rules import set_rules
from .lcenvironments import moons, shift_by_offset
from BaseClasses import Item, ItemClassification, Tutorial, MultiWorld, Region
from .options import LCOptions
from worlds.AutoWorld import World, WebWorld
from typing import List, Dict, Any
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
        self.junk_pool: Dict[str, int] = {}

    def create_items(self) -> None:
        # shortcut for starting_inventory... The start_with_revive option lets you start with a Dio's Best Friend

        # only mess with the environments if they are set as items
        environments_pool = shift_by_offset(moons, 11100)

        # percollect environments for each stage (or just stage 1)
        unlock = None
        starting_moon = self.multiworld.starting_moon[self.player].value
        if starting_moon < 8:
            unlock = [moon_names[starting_moon]]
        else:
            unlock = self.multiworld.random.choices(list(environments_pool.keys()), k=1)
        self.multiworld.push_precollected(self.create_item(unlock[0]))
        environments_pool.pop(unlock[0])

        # Generate item pool
        itempool: List = []

        for env_name, _ in environments_pool.items():
            itempool += [env_name]

        for buyable, _ in shop_items.items():
            itempool += [buyable]

        if self.multiworld.enable_inventory_unlock[self.player].value == 1:
            itempool += ["Inventory Slot", "Inventory Slot", "Inventory Slot"]

        total_locations = len(
            generate_locations(
                checks_per_moon=self.multiworld.checks_per_moon[self.player].value,
                num_quota=self.multiworld.num_quotas[self.player].value
            )
        )

        # Create junk items
        self.junk_pool = self.create_junk_pool()
        # Fill remaining items with randomly generated junk
        while len(itempool) < total_locations:
            itempool.append(self.get_filler_item_name())

        # Convert itempool into real items
        itempool = list(map(lambda name: self.create_item(name), itempool))
        self.multiworld.itempool += itempool

    def set_rules(self) -> None:
        set_rules(self)

    def get_filler_item_name(self) -> str:
        if not self.junk_pool:
            self.junk_pool = self.create_junk_pool()
        weights = [data for data in self.junk_pool.values()]
        filler = self.multiworld.random.choices([filler for filler in self.junk_pool.keys()], weights,
                                                k=1)[0]
        return filler

    def create_junk_pool(self) -> Dict:
        junk_pool = {
            "Money": self.multiworld.money[self.player].value,
            "Haunt Trap!": self.multiworld.haunt_trap[self.player].value,
            "Bracken Trap!": self.multiworld.bracken_trap[self.player].value
        }
        return junk_pool

    def create_regions(self) -> None:
        create_regions(self.multiworld, self.player)
        create_events(self.multiworld, self.player)

    def fill_slot_data(self):
        return {
            "goal": self.multiworld.game_mode[self.player].value,
            "moneyPerQuotaCheck": self.multiworld.money_per_quota_check[self.player].value,
            "numQuota": self.multiworld.num_quotas[self.player].value,
            "checksPerMoon": self.multiworld.checks_per_moon[self.player].value,
            "deathLink": self.multiworld.death_link[self.player].value,
            "inventorySlot": self.multiworld.enable_inventory_unlock[self.player].value,
            "minMoney": self.multiworld.min_money[self.player].value,
            "maxMoney": self.multiworld.max_money[self.player].value,
            "moonRank": self.multiworld.moon_grade[self.player].value,
            "collectathonGoal": self.multiworld.collectathon_scrap_goal[self.player].value
        }

    def create_item(self, name: str) -> Item:
        item_id = item_table[name]
        classification = ItemClassification.filler
        if name in moons.keys() or name == "Inventory Slot":
            classification = ItemClassification.progression
        elif name in shop_items:
            classification = ItemClassification.useful
        elif name in {"Bracken Trap", "Haunt Trap"}:
            classification = ItemClassification.trap

        item = LethalCompanyItem(name, classification, item_id, self.player)
        return item


def create_events(world: MultiWorld, player: int) -> None:
    world_region = world.get_region("The Company", player)
    victory_region = world.get_region("Victory", player)
    victory_event = LethalCompanyLocation(player, "Victory", None, victory_region)
    victory_event.place_locked_item(LethalCompanyItem("Victory", ItemClassification.progression, None, player))
    world_region.locations.append(victory_event)


def create_region(world: MultiWorld, player: int, name: str, loc=None) -> Region:
    if loc is None:
        loc = {}
    ret = Region(name, player, world)
    for location_name, location_id in loc.items():
        ret.locations.append(LethalCompanyLocation(player, location_name, location_id, ret))
    return ret
