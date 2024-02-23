from . import LCTestBase
from .. import LethalCompanyWorld


class ExclusionTest(LCTestBase):
    options = {"exclude_hive": "true", "exclude_shotgun": "true", "scrapsanity": "true",
               "starting_inventory_slots": "1"}

    # Test if progression items are successfully excluded
    def test_exclude_scraps(self):
        for player in self.multiworld.get_game_players("Lethal Company"):
            self.assertFalse(self.multiworld.get_location("Scrap - Bee Hive", player)
                             .item_rule(self.get_item_by_name("Inventory Slot")))
            self.assertTrue(self.multiworld.get_location("Scrap - Bee Hive", player)
                            .item_rule(self.get_item_by_name("Money")))
            self.assertTrue(self.multiworld.get_location("Scrap - Bee Hive", player)
                            .item_rule(self.get_item_by_name("Less Time")))

            self.assertFalse(self.multiworld.get_location("Scrap - Double-barrel", player)
                             .item_rule(self.get_item_by_name("Inventory Slot")))
            self.assertTrue(self.multiworld.get_location("Scrap - Double-barrel", player)
                            .item_rule(self.get_item_by_name("Money")))
            self.assertTrue(self.multiworld.get_location("Scrap - Double-barrel", player)
                            .item_rule(self.get_item_by_name("Less Time")))

            self.assertFalse(self.multiworld.get_location("Scrap - Gold bar", player)
                             .item_rule(self.get_item_by_name("Inventory Slot")))
            self.assertTrue(self.multiworld.get_location("Scrap - Gold bar", player)
                            .item_rule(self.get_item_by_name("Money")))
            self.assertTrue(self.multiworld.get_location("Scrap - Gold bar", player)
                            .item_rule(self.get_item_by_name("Less Time")))


class InclusionTest(LCTestBase):
    options = {"exclude_hive": "false", "exclude_shotgun": "false", "scrapsanity": "true",
               "starting_inventory_slots": "1"}

    # Test if progression items are successfully included when exclude is set to false
    def test_include_scraps(self):
        for player in self.multiworld.get_game_players("Lethal Company"):
            self.assertTrue(self.multiworld.get_location("Scrap - Bee Hive", player)
                            .item_rule(self.get_item_by_name("Inventory Slot")))
            self.assertTrue(self.multiworld.get_location("Scrap - Bee Hive", player)
                            .item_rule(self.get_item_by_name("Money")))
            self.assertTrue(self.multiworld.get_location("Scrap - Bee Hive", player)
                            .item_rule(self.get_item_by_name("Less Time")))

            self.assertTrue(self.multiworld.get_location("Scrap - Double-barrel", player)
                            .item_rule(self.get_item_by_name("Inventory Slot")))
            self.assertTrue(self.multiworld.get_location("Scrap - Double-barrel", player)
                            .item_rule(self.get_item_by_name("Money")))
            self.assertTrue(self.multiworld.get_location("Scrap - Double-barrel", player)
                            .item_rule(self.get_item_by_name("Less Time")))

            self.assertFalse(self.multiworld.get_location("Scrap - Gold bar", player)
                             .item_rule(self.get_item_by_name("Inventory Slot")))
            self.assertTrue(self.multiworld.get_location("Scrap - Gold bar", player)
                            .item_rule(self.get_item_by_name("Money")))
            self.assertTrue(self.multiworld.get_location("Scrap - Gold bar", player)
                            .item_rule(self.get_item_by_name("Less Time")))
