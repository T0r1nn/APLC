from BaseClasses import Item, MultiWorld, ItemClassification
from typing import Dict, Any
from worlds.AutoWorld import World


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


filler_items: Dict[str, int] = {}
environment_pool: Dict[str, int] = {}
classification_table: Dict[str, ItemClassification] = {}
item_table: Dict[str, int] = {}
moons = []
shop_items = []

items = [
    LCItem("Walkie-talkie", shop_item=True),
    LCItem("Shovel", shop_item=True),
    LCItem("Lockpicker", shop_item=True),
    LCItem("Progressive Flashlight", 0, 2, shop_item=True),
    LCItem("Stun grenade", shop_item=True),
    LCItem("Boombox", shop_item=True),
    LCItem("TZP-Inhalant", shop_item=True),
    LCItem("Zap gun", shop_item=True),
    LCItem("Jetpack", shop_item=True),
    LCItem("Extension ladder", shop_item=True),
    LCItem("Radar-booster", shop_item=True),
    LCItem("Spray paint", shop_item=True),
    LCItem("LoudHorn", shop_item=True),
    LCItem("SignalTranslator", shop_item=True),
    LCItem("Teleporter", shop_item=True),
    LCItem("InverseTeleporter", shop_item=True),
    LCItem("Experimentation", environment=True, classification=ItemClassification.progression),
    LCItem("Assurance", environment=True, classification=ItemClassification.progression),
    LCItem("Vow", environment=True, classification=ItemClassification.progression),
    LCItem("Offense", environment=True, classification=ItemClassification.progression),
    LCItem("March", environment=True, classification=ItemClassification.progression),
    LCItem("Rend", environment=True, classification=ItemClassification.progression),
    LCItem("Dine", environment=True, classification=ItemClassification.progression),
    LCItem("Titan", environment=True, classification=ItemClassification.progression),
    LCItem("Inventory Slot", 2, lambda w: 4 - w.options.starting_inventory_slots.value,
           classification=ItemClassification.progression),
    LCItem("Stamina Bar", 2, lambda w: 4 - w.options.starting_stamina_bars.value,
           classification=ItemClassification.progression),
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
