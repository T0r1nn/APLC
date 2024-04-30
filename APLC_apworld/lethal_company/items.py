import math

from BaseClasses import Item, MultiWorld, ItemClassification
from typing import Dict, Any
from worlds.AutoWorld import World
from .locations import generate_locations
from .imported import data


class LethalCompanyItem(Item):
    game: str = "Lethal Company"


class LCItem:
    id = 1966720

    def __init__(self, name, count_mode=0, count_arg: Any = 1, environment=False,
                 classification=ItemClassification.progression, shop_item=False):
        self.name = name
        self.item_id = LCItem.id
        LCItem.id += 1
        self.count_mode = count_mode
        self.count_arg = count_arg
        item_table.update({self.name: self.item_id})
        if environment:
            environment_pool[self.name] = self.item_id
            moons.append(self.name)
        if shop_item:
            shop_items.append(self.name)
        classification_table[self.name] = classification

    def create_item(self, lcworld: World):
        names = []
        match self.count_mode:
            case 0:
                # arg is # of item
                for i in range(self.count_arg):
                    names.append(self.name)
            case 1:
                # arg is name of option that contains the count of the item
                for i in range(getattr(lcworld.options, self.count_arg).value):
                    names.append(self.name)
            case 2:
                # arg is a lambda function that takes in the multiworld and outputs a number
                for i in range(self.count_arg(lcworld)):
                    names.append(self.name)
            case 3:
                # used for filler items
                filler_items.update({self.name: getattr(lcworld.options, self.count_arg).value})
                return []

        return names


def calculate_credits(world):
    if not world.options.game_mode.value == 2:
        return 0

    location_count = len(generate_locations(world.options.checks_per_moon.value, world.options.num_quotas.value,
                                            world.options.scrapsanity.value))
    location_count -= 7
    location_count -= world.options.randomize_company_building.value
    location_count -= world.options.randomize_scanner.value
    location_count -= world.options.randomize_terminal.value
    location_count -= (4 - world.options.starting_stamina_bars.value)
    location_count -= (4 - world.options.starting_inventory_slots.value)
    location_count -= 16

    credit_count = math.ceil(location_count * (world.options.credit_replacement/100.0))
    world.required_credit_count = round(credit_count * (world.options.required_credits/100.0))
    return credit_count


filler_items: Dict[str, int] = {}
environment_pool: Dict[str, int] = {}
classification_table: Dict[str, ItemClassification] = {}
item_table: Dict[str, int] = {}
moons = []
shop_items = []

items = [
    LCItem("LoudHorn", classification=ItemClassification.useful),
    LCItem("SignalTranslator", classification=ItemClassification.useful),
    LCItem("Teleporter", classification=ItemClassification.useful),
    LCItem("InverseTeleporter", classification=ItemClassification.useful),
    LCItem("Company Building", 1, "randomize_company_building", classification=ItemClassification.progression),
    LCItem("Terminal", 1, "randomize_terminal", classification=ItemClassification.progression),
    LCItem("Inventory Slot", 2, lambda w: 4 - w.options.starting_inventory_slots.value,
           classification=ItemClassification.progression),
    LCItem("Stamina Bar", 2, lambda w: 4 - w.options.starting_stamina_bars.value,
           classification=ItemClassification.progression),
    LCItem("Company Credit", 2, calculate_credits, classification=ItemClassification.progression),
    LCItem("Strength Training", 3, "weight_reducers",
           classification=ItemClassification.filler),
    LCItem("Scanner", 1, "randomize_scanner", classification=ItemClassification.progression),
    LCItem("Money", 3, "money", classification=ItemClassification.filler),
    LCItem("More Time", 3, "time_add", classification=ItemClassification.filler),
    LCItem("Clone Scrap", 3, "scrap_clone", classification=ItemClassification.filler),
    LCItem("Birthday Gift", 3, "birthday", classification=ItemClassification.filler),
    LCItem("HauntTrap", 3, "haunt_trap", classification=ItemClassification.trap),
    LCItem("BrackenTrap", 3, "bracken_trap", classification=ItemClassification.trap),
    LCItem("Less Time", 3, "time_trap", classification=ItemClassification.trap)
]

for item in data.get("store"):
    items.append(LCItem(item, shop_item=True))

for moon in data.get("moons"):
    items.append(LCItem(" ".join(moon.split(" ")[1:]), environment=True))
