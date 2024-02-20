## Setup
Download the following two files from the [Releases page in Github](https://github.com/Awesomeness278/APLC/releases/latest):
- lethal_company.yaml
- lethal_company.apworld

You also need to install the [Archipelago Multiworld Randomizer version 0.4.4](https://github.com/ArchipelagoMW/Archipelago/releases/latest) client.

Install the APLC mod from the Thunderstore (you may also install the mod via the
Thunderstore Mod Manager or R2ModManager).

Then, follow the below steps to setup the randomizer:

### YAML configuration
The first thing you need to do is configure the lethal_company.yaml file. For that, open and
edit the YAML to fit whatever settings you want to play with:
- name: Enter your desired slot name here, could be almost anything. {player} or {PLAYER} are replaced with the slot number, while {number} or {NUMBER} will increment once for each duplicate YAML name
- game_mode: Can be collectathon or trophy, in trophy mode you need to collect one trophy apparatus from each moon, while in collectathon mode you need to collect a certain amount of AP chest scraps
- checks_per_moon: Can be any number between 1 and 10, default is 3. This is the amount of checks per moon that can be gotten from completing that moon
- money_per_quota_check: Can be any number between 100 and 10000, default is 500. This is the amount of total quota you must complete to get the check. Be aware that each quota only contributes the required scrap towards this goal, not the sold scrap. If you sell 500 scrap but the quota is only 130, you will only contribute 130 towards the check.
- num_quotas: Can be any number from 10 to 50, default is 20. This is the number of quota checks you can get.
- starting_inventory_slots: Can be any number from 1 to 4, default is 4. This is the number of inventory slots you start the game with, the rest have to be unlocked.
- starting_stamina_bars: Can be any number from 0 to 4, default is 4. This is the number of stamina bars you start with. The rest have to be unlocked.
- collectathon_scrap_goal: Can be any number from 3 to 30, default is 10. This is the number of AP chests you have to collect to beat collectathon mode. This does nothing in trophy mode.
- randomize_scanner: Can be 'true' or 'false', when true you start without the ability to scan and must unlock it.
- scrapsanity: Can be 'true' or 'false', when true 50 new checks are added to the game for collecting at least one of each type of scrap.
- min_money: Can be any number between 0 and 5000, default is 100. This is the minimum amount of money that the money checks will reward you with.
- max_money: Can be any number between 0 and 5000, default is 1000. This is the maximum amount of money that the money checks will reward you with. This can't be less than min_money
- starting_moon: Can be any of the following: experimentation, assurance, vow, offense, march, rend, dine, titan, or randomize. This sets the moon you start the run on.
- split_moon_grades: Can be 'true' or 'false', when 'true' the moon_grade option is ignored and the low, medium, and high_moon_grade options are used instead. When 'false', the inverse happens.
- moon_grade: Can be any of the following: s, a, b, c, d, or f. Any moons completed on this grade or above will complete a moon check.
- low_moon_grade: Same options as moon_grade, only affects Experimentation, Assurance, and Vow.
- medium_moon_grade: Same options as moon_grade, only affects Offense and March.
- high_moon_grade: Same options as moon_grade, only affects Rend, Dine, and Titan.
- The following are filler items, and they fill the empty spots in the item pool according to their weight values, where higher weighted items will appear more than lower weighted items. All have a minimum of 0 and a maximum of 100
  - time_add: Default is 20, adds one day to quota
  - scrap_clone: Default is 20, clones one scrap on the ship
  - birthday: Default is 20, sends one item from the dropship
  - weight_reducers: Default is 5, reduces stamina use and increases speed
  - bracken_trap: Default is 8, spawns a bracken in the next facility that can spawn a bracken
  - haunt_trap: Default is 4, spawns a ghost girl in the next facility that can spawn a ghost girl
  - time_trap: Default is 30, removes one day from quota. If quota is already at one day left, it'll wait until the next quota to remove a day
  - money: Default is 80, adds a random amount of money to your total 
- death_link: Can be 'true' or 'false', when true turns on death link. When a party wipe occurs, a death link is sent, and when a death link is received, a random player dies.

### Multiworld generation
Once your YAML is configured, navigate to your Archipelago installation folder (will vary
depending on where you installed Archipielago, but an example path would be
C:\ProgramData\Archipelago). In the Players folder paste your YAML file as well as the
YAMLs of any other players participating in the multiworld (Note: Only one YAML per lobby
of Lethal Company. An example of such would be 2 players playing in the same LC lobby
while another player plays a different Archipelago game (or LC lobby)). You would need only
1 YAML for Lethal Company and 1 YAML for the other game/s.).In the lib/worlds folder, paste
lethal_company.apworld. Then, open the archipelago launcher and click Generate which will
generate a .zip in the Output folder found in your archipelago installation, in the same path
where the players folder is. Once the generation finishes, navigate to [the archipelago](https://archipelago.gg)
website, click on get started, then click Host Game, then "Upload File" and select the zip
folder in the Output subfolder of your Archipelago folder. The game will generate along with
the Spoiler log. Click on Create New Room and that’s done! The Archipelago server is now
running!

### Setting up the mod
Run the game through Thunderstore (or as you would usually start your modded LC game),
and boot up a save. Once you are ready, you can type /connect archipelago.gg:port in the
chat, then follow the instructions as they appear. Everyone in the lobby should connect when the host performs a /connect, but if anyone joins late, they can type /connect with no arguments in the chat to connect themselves to the multiworld.

## Locations and items
In multiworld games setups, locations are the places you need to go to unlock new things.
The locations in Lethal Company are as follows:
- Log Entries(Excluding first log).
- Bestiary Entries.
- Completing moons on a B or higher grade.
- Completing a set amount of quota.
- Gathering each type of scrap for the first time

The items are:
- Moons
- Shop items
- Ship upgrades
- Inventory slots
- Scanner
- Stamina bars
- Strength training(stamina decreases slower and you move faster)
- Filler
  - Scrap cloning
  - Money
  - Quota time increases
  - Random items in a dropship
- Traps
  - Bracken spawns
  - Ghost girl spawns
  - Quota time decreases

## Credits
Thanks to my friends for helping me test this and fix a few
annoying crashes. Thanks to everyone in the Lethal Company thread in the Archipelago
discord server, who helped me fix a ton of bugs and get a bunch of ideas, without which this archipelago
mod probably wouldn't be possible. Thanks to Faxium for rewriting the setup portion of this guide, making it easier to understand.