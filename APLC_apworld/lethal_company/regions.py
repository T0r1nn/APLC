from BaseClasses import MultiWorld, Region, Location
from Utils import visualize_regions
from .items import moons, shop_items
from .locations import bestiary_names, scrap_names, bestiary_moons, scrap_moons, max_locations, log_names
from .rules import check_item_accessible
from .options import LCOptions


def create_regions(options: LCOptions, world):
    multiworld: MultiWorld = world.multiworld
    player: int = world.player

    menu: Region = Region("Menu", player, multiworld)
    multiworld.regions.append(menu)
    ship: Region = Region("Ship", player, multiworld)
    multiworld.regions.append(ship)
    starting_moon: Region = Region(world.initial_world, player, multiworld)
    multiworld.regions.append(starting_moon)
    terminal: Region = Region("Terminal", player, multiworld)
    multiworld.regions.append(terminal)
    logs: list[Region] = []
    bestiary: list[Region] = []
    scrap: list[Region] = []
    moon_regions: list[Region] = []
    company_building: Region = Region("Company Building", player, multiworld)
    multiworld.regions.append(company_building)
    quotas: Region = Region("Quotas", player, multiworld)
    multiworld.regions.append(quotas)
    victory: Region = Region("Victory", player, multiworld)
    multiworld.regions.append(victory)

    if options.game_mode.value == 2:
        company_building.connect(victory,
                                 rule=lambda state: (state.has("Company Credit", player,
                                                               count=world.required_credit_count)))
    else:
        company_building.connect(victory, rule=lambda state: (state.has_all(shop_items, player)
                                                              and state.has_all(moons, player)
                                                              and state.has("Progressive Flashlight", player, count=2)))

    menu.connect(ship, rule=lambda state: True)
    ship.connect(starting_moon, rule=lambda state: True)
    ship.connect(terminal, rule=lambda state: state.has("Terminal", player) or options.randomize_terminal.value == 0)
    for moon in moons:
        if not moon == world.initial_world:
            moon_regions.append(Region(moon, player, multiworld))
            multiworld.regions.append(moon_regions[-1])
            terminal.connect(moon_regions[-1], rule=lambda state, m=moon: state.has(m, player))

    terminal.connect(company_building, rule=lambda state: (state.has("Company Building", player)
                                                           or options.randomize_company_building == 0))
    company_building.connect(quotas, rule=lambda state: ((state.has("Inventory Slot", player)
                                                          or options.starting_inventory_slots.value >= 2)
                                                         and (state.has("Stamina Bar", player)
                                                              or options.starting_stamina_bars.value >= 1)))

    for monster in bestiary_names:
        bestiary.append(Region(monster, player, multiworld))
        multiworld.regions.append(bestiary[-1])
        cant_spawn = bestiary_moons[monster]
        can_spawn = [moon for moon in moons]
        for moon in cant_spawn:
            can_spawn.remove(moon)
        for moon in can_spawn:
            multiworld.get_region(moon, player).connect(multiworld.get_region(monster, player),
                                                        rule=lambda state: (state.has("Scanner", player)
                                                                            or options.randomize_scanner == 0))

    if options.scrapsanity.value == 1:
        for scrap_name in scrap_names:
            scrap.append(Region(scrap_name, player, multiworld))
            multiworld.regions.append(scrap[-1])

        for moon in scrap_moons.keys():
            for scrap_ind in scrap_moons[moon]:
                multiworld.get_region(moon, player).connect(scrap[scrap_ind],
                                                            rule=lambda state, s_name=scrap_names[scrap_ind]:
                                                            ((state.has("Stamina Bar", player)
                                                              or options.starting_stamina_bars.value > 0)
                                                             and (check_item_accessible(state, "Shovel",
                                                                                        player, options)
                                                                  or s_name != "Double-barrel")))

    logs.append(Region("Sound Behind the Wall", player, multiworld))
    multiworld.regions.append(logs[-1])
    company_building.connect(logs[0])
    logs.append(Region("Smells Here!", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Assurance", player).connect(logs[1])
    logs.append(Region("Swing of Things", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Experimentation", player).connect(logs[2])
    logs.append(Region("Shady", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Experimentation", player).connect(logs[3],
                                                             rule=lambda state: (state.has("Stamina Bar", player)
                                                                                 or options.starting_stamina_bars >= 1))
    logs.append(Region("Golden Planet", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Rend", player).connect(logs[4])
    logs.append(Region("Goodbye", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("March", player).connect(logs[5])
    logs.append(Region("Screams", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Vow", player).connect(logs[6])
    logs.append(Region("Idea", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Rend", player).connect(logs[7])
    logs.append(Region("Nonsense", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Rend", player).connect(logs[8])
    logs.append(Region("Hiding", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Dine", player).connect(logs[9])
    logs.append(Region("Real Job", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Titan", player).connect(logs[10],
                                                   rule=lambda state:
                                                   (state.has_any(["Extension Ladder", "Jetpack"], player)
                                                    and (state.has("Terminal", player)
                                                         or options.randomize_terminal == 0)
                                                    and (state.has("Company Building", player)
                                                         or options.randomize_company_building == 0)))
    logs.append(Region("Desmond", player, multiworld))
    multiworld.regions.append(logs[-1])
    multiworld.get_region("Titan", player).connect(logs[11],
                                                   rule=lambda state:
                                                   (state.has_any(["Extension Ladder", "Jetpack"], player)
                                                    and (state.has("Terminal", player)
                                                         or options.randomize_terminal == 0)
                                                    and (state.has("Company Building", player)
                                                         or options.randomize_company_building == 0)))

    # Generate locations
    for i in range(options.checks_per_moon.value):
        for moon in moons:
            add_location(player, f"{moon} check {i+1}", multiworld.get_region(moon, player))

    for i in range(options.num_quotas.value):
        add_location(player, f"Quota check {i+1}", quotas)

    for log in log_names:
        add_location(player, f"Log - {log}", multiworld.get_region(log, player))

    for monster in bestiary_names:
        add_location(player, f"Bestiary Entry - {monster}", multiworld.get_region(monster, player))

    if options.scrapsanity.value == 1:
        for scrap_name in scrap_names:
            add_location(player, f"Scrap - {scrap_name}", multiworld.get_region(scrap_name, player))

    visualize_regions(menu, "lc_regions")


def add_location(player: int, location: str, region: Region):
    region.locations.append(Location(player, location, max_locations[location]))
    region.locations[-1].parent_region = region
