from typing import Dict

moons: Dict[str, int] = {
    "Experimentation": 1,
    "Assurance": 2,
    "Vow": 3,
    "Offense": 4,
    "March": 5,
    "Dine": 6,
    "Rend": 7,
    "Titan": 8
}


def shift_by_offset(dictionary: Dict[str, int], offset: int) -> Dict[str, int]:
    return {name: index + offset for name, index in dictionary.items()}