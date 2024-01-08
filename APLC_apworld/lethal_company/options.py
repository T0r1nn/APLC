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


class ChecksPerMoon(Range):
    """
    The total number of checks you can get at one moon
    """
    display_name = "Checks Per Moon"
    range_start = 1
    range_end = 10
    default = 3


class MoneyPerQuotaCheck(Range):
    """
    The total amount of quota you have to acheive to meet each quota check
    For example, if money per quota check is 1000, you need to reach 1000 total quota for each quota check
    """
    display_name = "Money Per Quota Check"
    range_start = 100
    range_end = 10000
    default = 1000


class NumQuotas(Range):
    """
    Will not give checks for quotas past this number. For example, if maximum quotas is 10, the 11th quota check will not count as a check
    """
    display_name = "Num Quotas"
    range_start = 10
    range_end = 50
    default = 20


class EnableInventoryUnlock(Toggle):
    """
    When on, you only start with one open inventory slot and are required to unlock the other three throughout the multiworld
    """
    display_name = "Enable Inventory Unlock"


class BrackenTrapWeight(Range):
    """
    The weight of the bracken traps in the pool.
    """
    display_name = "Bracken Trap Weight"
    range_start = 0
    range_end = 100
    default = 8


class HauntTrapWeight(Range):
    """
    The weight of haunt traps in the pool.
    """
    display_name = "Haunt Trap Weight"
    range_start = 0
    range_end = 100
    default = 4


class MoneyWeight(Range):
    """
    The weight of money drops in the pool. Each money drop can give anywhere from 100-1000 scrap, though it doesn't count towards the quota
    """
    display_name = "Money Weight"
    range_start = 0
    range_end = 100
    default = 80


# Yaml options to add:
#   Variable scrap number for collectathon
#   Range that money checks give
#   Choice of a starting moon or random
#   Required rank to complete moon checks
class CollectathonScrapGoal(Range):
    """
    The number of collectathon scrap you need to complete the collectathon goal
    """
    display_name = "Collectathon Scrap Goal"
    range_start = 5
    range_end = 30
    default = 20


class MinMoneyCheck(Range):
    """
    The minimum amount of money that a money check can give you
    """
    display_name = "Min Money Check Amount"
    range_start = 0
    range_end = 5000
    default = 100


class MaxMoneyCheck(Range):
    """
    The maximum amount of money that a money check can give you
    """
    display_name = "Max Money Check Amount"
    range_start = 0
    range_end = 10000
    default = 1000


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


@dataclass
class LCOptions(PerGameCommonOptions):
    game_mode: Goal
    checks_per_moon: ChecksPerMoon
    money_per_quota_check: MoneyPerQuotaCheck
    num_quotas: NumQuotas
    enable_inventory_unlock: EnableInventoryUnlock
    collectathon_scrap_goal: CollectathonScrapGoal
    min_money: MinMoneyCheck
    max_money: MaxMoneyCheck
    starting_moon: StartingMoon
    moon_grade: MoonCheckGrade
    bracken_trap: BrackenTrapWeight
    haunt_trap: HauntTrapWeight
    money: MoneyWeight
    death_link: DeathLink
