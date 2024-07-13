from .imported import data
from .custom_content import custom_content


def GetImportedData():
    data_copy = Copy(data)

    if type(custom_content) is not dict:
        return data_copy

    if "moons" in custom_content.keys():
        [data_copy["moons"].append(moon) for moon in custom_content.get("moons")]
    if "store" in custom_content.keys():
        [data_copy.get("store").append(shop_item) for shop_item in custom_content.get("store")]
    if "scrap" in custom_content.keys():
        data_copy["scrap"] = custom_content.get("scrap")
    if "bestiary" in custom_content.keys():
        data_copy["bestiary"] = custom_content.get("bestiary")

    return data_copy


def Copy(input_data):
    copied_data = {}
    for key, value in input_data.items():
        if value is dict:
            copied_data[key] = Copy(value)
        elif value is list:
            copied_data[key] = value.copy()
        else:
            copied_data[key] = value
    return copied_data
