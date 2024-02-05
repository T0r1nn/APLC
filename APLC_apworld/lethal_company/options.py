from dataclasses import dataclass
from Options import Toggle, DeathLink, Range, Choice, PerGameCommonOptions


class Goal(Choice):
    """
    Trophy Mode: Each moon has a rare trophy scrap, the goal is to get all eight

    Collectathon: A new rare scrap is added, you need to collect at least ten of it to win
    """
    display_name = "Game Mode"
    option_trophy = 0
    option_collectathon = 1
    default = 1
    slot = True
    slot_name = "goal"


class ChecksPerMoon(Range):
    """
    The total number of checks you can get at one moon
    """
    display_name = "Checks Per Moon"
    range_start = 1
    range_end = 10
    default = 3
    slot = True
    slot_name = "checksPerMoon"


class MoneyPerQuotaCheck(Range):
    """
    The total amount of quota you have to acheive to meet each quota check
    For example, if money per quota check is 1000, you need to reach 1000 total quota for each quota check
    """
    display_name = "Money Per Quota Check"
    range_start = 100
    range_end = 10000
    default = 500
    slot = True
    slot_name = "moneyPerQuotaCheck"


class NumQuotas(Range):
    """
    Will not give checks for quotas past this number. For example, if maximum quotas is 10, the 11th quota check will not count as a check
    """
    display_name = "Num Quotas"
    range_start = 10
    range_end = 50
    default = 20
    slot = True
    slot_name = "numQuota"


class BrackenTrapWeight(Range):
    """
    The weight of the bracken traps in the pool.
    """
    display_name = "Bracken Trap Weight"
    range_start = 0
    range_end = 100
    default = 8
    slot = False


class HauntTrapWeight(Range):
    """
    The weight of haunt traps in the pool.
    """
    display_name = "Haunt Trap Weight"
    range_start = 0
    range_end = 100
    default = 4
    slot = False


class MoneyWeight(Range):
    """
    The weight of money drops in the pool. Each money drop can give anywhere from 100-1000 scrap, though it doesn't count towards the quota
    """
    display_name = "Money Weight"
    range_start = 0
    range_end = 100
    default = 80
    slot = False


class DayIncreaseWeight(Range):
    """
    The weight of extra day items in the pool
    """
    display_name = "Extra Day Weight"
    range_start = 0
    range_end = 100
    default = 20
    slot = False


class DayDecreaseWeight(Range):
    """
    The weight of day decrease traps in the pool.
    """
    display_name = "Lose a Day Weight"
    range_start = 0
    range_end = 100
    default = 30
    slot = False


class ScrapDupeWeight(Range):
    """
    The weight of scrap duplication items in the pool
    """
    display_name = "Scrap Cloning Weight"
    range_start = 0
    range_end = 100
    default = 20
    slot = False


class BirthdayGiftWeight(Range):
    """
    The weight of birthday gifts in the pool(random item sent in a dropship)
    """
    display_name = "Birthday Gift Weight"
    range_start = 0
    range_end = 100
    default = 20
    slot = False


class CollectathonScrapGoal(Range):
    """
    The number of collectathon scrap you need to complete the collectathon goal
    """
    display_name = "Collectathon Scrap Goal"
    range_start = 3
    range_end = 30
    default = 10
    slot = True
    slot_name = "collectathonGoal"


class MinMoneyCheck(Range):
    """
    The minimum amount of money that a money check can give you
    """
    display_name = "Min Money Check Amount"
    range_start = 0
    range_end = 5000
    default = 100
    slot = True
    slot_name = "minMoney"


class MaxMoneyCheck(Range):
    """
    The maximum amount of money that a money check can give you
    """
    display_name = "Max Money Check Amount"
    range_start = 0
    range_end = 10000
    default = 1000
    slot = True
    slot_name = "maxMoney"


class StartingMoon(Choice):
    """
    The moon you start on
    """
    display_name = "Starting Moon"
    option_experimentation = 0
    option_assurance = 1
    option_vow = 2
    option_offense = 3
    option_march = 4
    option_rend = 5
    option_dine = 6
    option_titan = 7
    option_randomize = 8
    default = 8
    slot = False


class MoonCheckGrade(Choice):
    """
    The grade you need to get to get a check on a moon
    """
    display_name = "Moon Check Grade"
    option_S = 0
    option_A = 1
    option_B = 2
    option_C = 3
    option_D = 4
    option_F = 5
    default = 2
    slot = True
    slot_name = "moonRank"


"""
Stuff to add:
Starting inventory spots replaces enable_inventory_unlock - done
Starting stamina bars - done
Scanner - done
Jumping
Movement keys
Holding your breath
Sprinting
Random items spawning in the ship - done
Items added to dropship - done
Extra quota days - done
Speed increase
"""


class StartingInventorySlots(Range):
    """
    The number of inventory slots you start the game with
    """
    display_name = "Starting Inventory Slots"
    range_start = 1
    range_end = 4
    default = 4
    slot = True
    slot_name = "inventorySlots"


class StartingStaminaBars(Range):
    """
    The number of stamina bars you start the game with
    """
    display_name = "Starting Stamina Bars"
    range_start = 0
    range_end = 4
    default = 4
    slot = True
    slot_name = "staminaBars"


class RandomizeScanner(Toggle):
    """
    Allows you to randomize your scanner, rendering you unable to scan until you receive the check
    """
    display_name = "Randomize Scanner"
    default = 0
    slot = True
    slot_name = "scanner"


class WeightReducers(Range):
    """
    The total weight of strength training items. Every item received makes you 2% stronger.
    """
    display_name = "Strength Training Weight"
    default = 5
    range_start = 0
    range_end = 100


@dataclass
class LCOptions(PerGameCommonOptions):
    game_mode: Goal
    checks_per_moon: ChecksPerMoon
    money_per_quota_check: MoneyPerQuotaCheck
    num_quotas: NumQuotas
    starting_inventory_slots: StartingInventorySlots
    starting_stamina_bars: StartingStaminaBars
    collectathon_scrap_goal: CollectathonScrapGoal
    randomize_scanner: RandomizeScanner
    min_money: MinMoneyCheck
    max_money: MaxMoneyCheck
    starting_moon: StartingMoon
    moon_grade: MoonCheckGrade
    time_add: DayIncreaseWeight
    scrap_clone: ScrapDupeWeight
    birthday: BirthdayGiftWeight
    weight_reducers: WeightReducers
    bracken_trap: BrackenTrapWeight
    haunt_trap: HauntTrapWeight
    time_trap: DayDecreaseWeight
    money: MoneyWeight
    death_link: DeathLink
