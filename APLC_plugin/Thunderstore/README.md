## Setup
Download the following two files from the [Releases page in Github](https://github.com/T0r1nn/APLC/releases/latest):
- lethal_company.yaml
- lethal_company.apworld

You also need to install the latest version of the [Archipelago Multiworld Randomizer](https://github.com/ArchipelagoMW/Archipelago/releases/latest) client.

Install the APLC mod from the Thunderstore (you may also install the mod via the R2ModManager, Gale, or other Thunderstore alternatives).

  **Note**: If you have an ongoing multiworld, you should use whatever version of the mod you **started with** unless you are severely impacted by a bug that was fixed in the latest version.

Then, follow the below steps to setup the randomizer:

### YAML configuration
<details>
The first thing you need to do is configure the lethal_company.yaml file. For that, open and
edit the YAML to fit whatever settings you want to play with:

- name: Enter your desired slot name here, could be almost anything. {player} or {PLAYER} are replaced with the slot number, while {number} or {NUMBER} will increment once for each duplicate YAML name
- death_link: Can be 'true' or 'false', when true turns on death link. When a party wipe occurs, a death link is sent, and when a death link is received, a random player dies.
- game_mode: Can be collectathon, trophy, or credit hunt. In trophy mode, you need to collect one trophy apparatus from each moon. In collectathon mode, you need to collect a certain amount of AP chest scraps. In credit hunt, you need to receive a certain amount of company credit items.
- collectathon_scrap_goal: Can be any number from 3 to 30, default is 10. This is the number of AP chests you have to collect to beat collectathon mode. This does nothing in other modes.
- credit_replacement: Can be any number from 5 to 80, represents the percent of filler items that are replaced with company credits for the credit hunt game mode. This does nothing in other modes.
- required_credits: Can be any number from 10 to 100, represents the percent of company credit items that are required to beat the game. This does nothing in other modes.
- checks_per_moon: Can be any number between 1 and 10, default is 3. This is the amount of checks per moon that can be gotten from completing that moon
- num_quotas: Can be any number from 10 to 50, default is 20. This is the number of quota checks you can get.
- money_per_quota_check: Can be any number between 100 and 10000, default is 500. This is the amount of total quota you must complete to get the check. Be aware that each quota only contributes the required scrap towards this goal, not the sold scrap. If you sell 500 scrap but the quota is only 130, you will only contribute 130 towards the check.
- scrapsanity: Can be 'true' or 'false', when true 50 new checks are added to the game for collecting at least one of each type of scrap.
- randomize_company_building: Can be 'true' or 'false', when true you start without the ability to go to the company building
- randomize_scanner: Can be 'true' or 'false', when true you start without the ability to scan and must unlock it.
- randomize_terminal: Can be 'true' or 'false', when true you start without the ability to open the terminal(which means you cannot route to any moon other than your starting moon)
- starting_moon: Can be any of the following: experimentation, assurance, vow, offense, march, rend, dine, titan, or randomize. This sets the moon you start the run on.
- starting_stamina_bars: Can be any number from 0 to 4, default is 4. This is the number of stamina bars you start with. The rest have to be unlocked.
- starting_inventory_slots: Can be any number from 1 to 4, default is 4. This is the number of inventory slots you start the game with, the rest have to be unlocked.
- moon_grade: Can be any of the following: s, a, b, c, d, or f. Any moons completed on this grade or above will complete a moon check.
- split_moon_grades: Can be 'true' or 'false', when 'true' the moon_grade option is ignored and the low, medium, and high_moon_grade options are used instead. When 'false', the inverse happens.
- low_moon_grade: Same options as moon_grade, only affects Experimentation, Assurance, and Vow.
- medium_moon_grade: Same options as moon_grade, only affects Offense and March.
- high_moon_grade: Same options as moon_grade, only affects Rend, Dine, and Titan.
- min_scrap_chance: Can be any number between 0 and 20, default is 3. Scrap will be in logic if their spawn chance on an in-logic moon is greater than or equal to this percentage. A value of less than 3% can significantly slow down your game. This does nothing when scrapsanity is disabled.
- min_monster_chance: Can be any number between 0 and 20, default is 5. monsters will be in logic if their spawn chance on an in-logic moon is greater than or equal to this percentage. A value of less than 3% can significantly slow down your game.
- min_money: Can be any number between 0 and 5000, default is 100. This is the minimum amount of money that the money checks will reward you with.
- max_money: Can be any number between 0 and 5000, default is 1000. This is the maximum amount of money that the money checks will reward you with. This can't be less than min_money
- modify_scrap_spawns: Can be 'true' or 'false'. When true, scrap spawn rates are modified in the following ways: all scrap have the same spawn chance, each moon has five scrap that are exclusive to it, and there are 7 scrap that are common between all the moons. This makes it a lot easier to find every in-logic scrap, which removes those times you are going to the same moon again and again, trying to find a rare item which blocks progression. There are also five special scrap (apparatus, hive, shotgun, kitchen knife, and sapsucker egg) which stay on their normal moons.
- exclude_killing: Can be 'true' or 'false'. When true, the Double-barrel and Kitchen knife locations are guaranteed to not have progression or useful items behind them
- exclude_hive: Can be 'true' or 'false'. When true, the Bee Hive location is guaranteed to not have a progression or useful item behind it
- exclude_egg: Can be 'true' or 'false'. When true, the Sapsucker Egg location is guaranteed to not have a progression or useful item behind it
- The following are filler items, and they fill the empty spots in the item pool according to their weight values, where higher weighted items will appear more than lower weighted items. All have a minimum of 0 and a maximum of 100
  - time_add: Default is 20. Adds one day to quota
  - scrap_clone: Default is 20. Clones one scrap on the ship
  - birthday: Default is 20. Sends one random store item from the dropship
  - weight_reducers: Default is 5. Reduces stamina use and increases speed
  - bracken_trap: Default is 8 Spawns a bracken in the next facility that can spawn a bracken
  - haunt_trap: Default is 4 Spawns a ghost girl in the next facility that can spawn a ghost girl
  - time_trap: Default is 30. Removes one day from quota. If quota already has one day left, it'll wait until the next quota to remove a day
  - money: Default is 80. Adds a random amount of money (between min_money and max_money) to your total
</details>

### Multiworld generation
<details>
Once your YAML is configured, navigate to your Archipelago installation folder (will vary
depending on where you installed Archipielago, but an example path would be
C:\ProgramData\Archipelago). In the 'Players' folder, paste your YAML file as well as the
YAMLs of any other players participating in the multiworld (Note: Only one YAML per lobby
of Lethal Company. An example of such would be 2 players playing in the same LC lobby
while another player plays a different Archipelago game (or LC lobby). You would need only
1 YAML for Lethal Company and 1 YAML for the other game/s). In the custom_worlds folder, paste
lethal_company.apworld. Then, open the archipelago launcher and click Generate. This will
generate a .zip in the 'output' folder found in your archipelago installation, in the same path
where the Players folder is. Once the generation finishes, navigate to [the archipelago](https://archipelago.gg)
website, click on get started, then click Host Game, then "Upload File", and select the zip
folder in the 'output' subfolder of your Archipelago folder. The game will generate along with
the Spoiler log. Click on 'Create New Room', and you're done! The Archipelago server is now
running!
</details>

### Setting up the mod
Run the game through your chosen mod installer (as you would usually start a modded LC game),
and boot up a save. Once you are ready, you can type /connect archipelago.gg:port in the
chat, then follow the instructions as they appear. Everyone in the lobby should connect when 
the host performs a /connect, but if anyone joins late and doesn't sutomatically connect, 
they can type /connect with no arguments in the chat to connect themselves to the multiworld.

## Locations and items
<details>In multiworld games setups, locations are the places you need to go to unlock new things.
The locations in Lethal Company are as follows:

- Log Entries(Excluding first log).
- Bestiary Entries.
- Completing moons on a set grade or higher as specified in the yaml.
- Completing a set amount of quota.
- Gathering each type of scrap for the first time if scrapsanity is enabled.

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
</details>

## Useful Terminal Commands
<details>
`hints` - Shows all hints in your world that you haven't completed yet, where the item is, and whether the item is logically accessible.

`progress` - Shows your current progress in the multiworld

`scrap [moon name]` - Shows all scrap on a moon and whether they're logically accessible

`scrap [scrap name]` - Shows all moons a scrap can be found on and whether it's logically accessible on those moons

`config` - Shows the current value of all config settings, as well as what you can set them to.

`config [setting name] [value]` - Sets a config setting to the value

`apfiller` - Shows how many of each filler item has been received, as well as how many are available to be used

`apfiller [item name]` - Uses a filler item if that item is currently available to use

`tracker` - Shows all logically accessible checks

`world [world name]` - Used only for advanced custom content, will allow you to connect to custom apworlds
</details>

## Adding custom scrap, monsters, and moons to the randomizer
<details>
EXPERIMENTAL FEATURE - WILL CHANGE OVER TIME, MAY BE BUGGY AND BROKEN
As of 0.6.0, APLC now supports adding custom scrap, monsters, moons, and store items to the randomizer.
To set up a world with custom content, follow these steps: 
1. Install all custom content that you want to include, as well as the [UnityExplorer](https://thunderstore.io/c/lethal-company/p/LethalCompanyModding/Yukieji_UnityExplorer/) mod. Boot up the game and create a new save file.
2. Open the UnityExplorer overlay with F7 if you do not already see it, then open the **C# Console** from the button at the top of the screen. 
3. From the dropdown in the Console window, click **REPL**, then replace all content in the file with the following line: APLC.Plugin.Instance.GetGameLogicString(); Click **Compile**.
4. Open the **Log** window from the button at the top of the screen, then click **Open Log File**. Highlight the entire the logic string, from the first { to the last }, then copy it. 
5. Go to your lethal company apworld. Rename the file to lethal_company.zip, then copy the lethal_company sub-folder into the custom_worlds folder. 
6. Inside the lethal_company folder, replace the contents of imported.py with 'data = ', then paste your logic string. 
7. Right click the lethal_company folder and select 'Send to' > 'Compressed (zipped) folder'. When prompted to name the file, change the '.zip' extension to '.apworld'.
Now, you can generate a multiworld using this apworld with the custom content.

### Using custom content in multiworlds with other lethal company games
Once you have your custom content apworld, to make it compatible with other people you need to do a few more steps.
1. First, you must decided on a name for your game. This name can't be the same as anyone else who is making a custom
content lethal company game, as its what keeps the apworlds from colliding. 
2. In the custom_content.py file, navigate to the "name": "" line, and replace the "" with " - [world name]" where you replace [world name] with whatever you
chose for your game's name. 
3. Then, you must rename the lethal_company folder to lethal_company-[world name], zip it back up, and change the filetype back to .apworld, before changing the apworld's name to lethal_company[world_name].apworld

Once this is complete, you can boot up the game. Before you connect to the game, you first must enter the following command 
in the ship terminal: `world [world name]`. This will sync your save file up to the new apworld name, which will allow you to 
successfully connect to archipelago. If you ever delete the save file or make a new one, make sure to always run this command
BEFORE running /connect.

### Custom Content Disclaimers:
Multiworlds might be unbeatable with custom content. If this happens, let me know so I can improve the logic string generation method.

Custom content might be bugged, as testing with every possible modded moon/scrap/monster combo is not feasible for me.
If you run into any bugs, however, don't hesitate to either create a github issue or message me in the archipelago discord
so I can work on fixing that bug.
</details>

## Credits
Thanks to my friends for helping me test this and fix a few annoying crashes. 

Thanks to everyone in the Lethal Company thread in the Archipelago discord server, who helped me fix a ton of bugs and get a bunch of ideas, without which this archipelago mod probably wouldn't be possible.

Thanks to Faxium for rewriting the setup portion of this guide, making it easier to understand.

Thanks to StevieSP for helping troubleshoot problems in the discord.

Thanks to ThisGuyHere for his feedback on bugs in the custom content system.

This project uses the Archipelago.MultiClient.Net library by Hussein Farran, Zach Parks, licensed under MIT.