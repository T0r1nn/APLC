from BaseClasses import Item, ItemClassification
from .options import MoneyWeight, HauntTrapWeight, BrackenTrapWeight
from .locations import max_locations
from .lcenvironments import moons, shift_by_offset
from typing import NamedTuple, Optional, Dict


class LethalCompanyItem(Item):
    game: str = "Lethal Company"


offset = 1966720
shop_items = {
    "Walkie-talkie": 1,
    "Flashlight": 2,
    "Shovel": 3,
    "Lockpicker": 4,
    "Pro-flashlight": 5,
    "Stun grenade": 6,
    "Boombox": 7,
    "TZP-Inhalant": 8,
    "Zap gun": 9,
    "Jetpack": 10,
    "Extension ladder": 11,
    "Radar-booster": 12,
    "Spray paint": 13,
    "LoudHorn": 14,
    "SignalTranslator": 15,
    "Teleporter": 16,
    "InverseTeleporter": 17
}

moon_names = [
    "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan"
]


item_table: Dict[str, int] = {
    "Money": offset,
    "Haunt Trap!": offset+1,
    "Bracken Trap!": offset+2,
    "Inventory Slot": offset+3
}

item_table.update(shift_by_offset(moons, offset+3))

item_table.update(shift_by_offset(shop_items, offset+3+len(moons)))
