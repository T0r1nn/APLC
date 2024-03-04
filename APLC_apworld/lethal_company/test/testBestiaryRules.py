from . import LCTestBase
from ..locations import bestiary_moons


class AssuranceTest(LCTestBase):
    options = {"starting_moon": "Assurance"}

    def test_exclusion(self):
        for player in self.multiworld.get_game_players("Lethal Company"):
            for entry in bestiary_moons.keys():
                if "Assurance" in bestiary_moons[entry]:
                    self.assertFalse(self.multiworld.get_location(f"Bestiary Entry - {entry}",player).can_reach(self.multiworld.state))
                else:
                    self.assertTrue(self.multiworld.get_location(f"Bestiary Entry - {entry}",player).can_reach(self.multiworld.state))