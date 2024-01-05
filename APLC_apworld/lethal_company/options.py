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


@dataclass
class LCOptions(PerGameCommonOptions):
    game_mode: Goal
    checks_per_moon: ChecksPerMoon
    money_per_quota_check: MoneyPerQuotaCheck
    num_quotas: NumQuotas
    enable_inventory_unlock: EnableInventoryUnlock
    bracken_trap: BrackenTrapWeight
    haunt_trap: HauntTrapWeight
    money: MoneyWeight
    death_link: DeathLink
