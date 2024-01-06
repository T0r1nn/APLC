# APLC

## Setup

Before you continue, you need the following files:
 - lethal_company.yaml
 - lethal_comapny.apworld

You also need to install Archipelago Multiworld Randomizer version 0.4.4, and you must install APLC from thunderstore

Then, follow the below steps to setup the randomizer:

### YAML configuration
The first thing you need to do is configure lethal_company.yaml. Edit whatever settings you feel you need to.

 - name: Enter your slot name here, make sure it is different from all other multiworld players.
 - goal: Either trophy mode or collectathon. If it is trophy mode, one special scrap will spawn at each moon, the goal is to collect all eight. If it is collectathon mode, you just need to collect 20 of a rare scrap.
 - checks_per_moon: The total number of checks you can get at each moon by finishing it with a B or higher grade
 - money_per_quota_check: The amount of total quota you need to complete to get a quota check. Every quota you meet adds the quota to this value, NOT the contributed money towards the quota. If you contribute 1000 towards a 130 quota, you still only get 130
 - num_quotas: The amount of quota checks that can be completed before running out
 - enable_inventory_unlock: Enables adding inventory slots to the randomized pool. You start with one, the other three can be unlocked throughout the game.
 - bracken_trap: The weight of bracken traps in the pool. Bracken traps spawn a bracken behind a random player.
 - haunt_trap: The weight of haunt traps in the pool. Haunt traps spawn a ghost girl and have her haunt a random player.
 - money: The weight of money items in the pool. Money items give between 100-1000 money, which doesn't count towards quota
 - death_link: If a party wipe happens, send a death link. If a death link is received, a random player will die.

### Multiworld generation
Once your YAML is configured, navigate to your Archipelago folder. In worlds, paste lethal_company.apworld, and in Players, paste your YAML file as well as the YAMLs of any other players participating in the multiworld. Then, open the archipelago launcher and click generate. Once the generation finishes, navigate to [the archipelago](https://archipelago.gg) website. Once you're in, click get started, then click Host Game. Then click "Upload File" and select the zip folder in the Output subfolder of your Archipelago folder. The game will generate, then you need to click Create New Room. The Archipelago server is now running!

### Setting up the mod
First, open the config section in Thunderstore or r2modman, navigate to BepInEx/config/APLC.cfg, and enter the connection information from archipelago. Finally, run the game modded through Thunderstore, and pay close attention to the console that pops up. If it has three lines in a row about APLC, two whites with a yellow between them, then the mod connected to archipelago. You can now create a new save and start playing.

## Locations and items
In multiworlds, locations are the places you need to go to unlock new things. The locations in Lethal Company are as follows: Log Entries(Excluding first log), Bestiary Entries, Completing moons on a B or higher grade, and completing enough quotas.
The items are the things you unlock by going to locations. The items are the store items, the moons, the ship upgrades, inventory slots, money, and some traps.

## Credits
Thanks to my friends Kimmersive and PersonMan for helping me test this and fix a few annoying crashes.
Thanks to everyone in the Lethal Company thread in the Archipelago discord server. They helped me find and fix a ton of bugs, without which this archipelago mod probably wouldn't be possible. Thanks a ton.