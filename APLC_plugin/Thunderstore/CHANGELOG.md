# Changelog

## 0.7.6
**<details><summary>Fixes</summary>**
* actually made LethalLevelLoader a dependency
* changed log levels to use better practices and filter what users see
* fixed 'scrap' and 'tracker' commands not working in some cases
* fixed instances of monsters showing NaN rarity in the logic string
* the logic string now includes custom apparatuses for each moon instead of just Ap Apparatus - Custom
* fixed the logic string outputting incorrectly for in regions that use commas in place of decimal points
* custom ap apparatuses once again send checks
* fixed moon difficulty calculation always using a cost of 0
* fixed cruiser (and other vehicles) not appearing in the shop
</details>

## 0.7.5
**<details><summary>Fixes</summary>**
* the log entries on Assurance and Experimentation now have correct names
* fixed an issue where the Adamance apparatus name didn't match the scrap check (apparently there are two apparatuses and one replaces the other?)
* the (kitchen) knife location is no longer impossible to complete
* fixed (most) custom moons with nonstandard names costing money
* the game *should* no longer crash on close when the user enters invalid connection info
* LethalLevelLoader is now correctly listed as a dependency
</details>

**<details><summary>Changes</summary>**
* custom ap apparatuses now include the moon's name in the scan node text
* the two new logs on Adamance and Artifice are now part of logic, as well as the sapsucker and sapsucker egg
* now uses the latest stable version of Archipelago.MultiClient.Net (6.6.1.0)
</details>

This version has **not** been tested with old multworlds and may cause issues with them. If you have an ongoing multiworld, it may be better to continue using 0.7.4.

## 0.7.4
**<details><summary>Fixes</summary>**
* fixed an issue with server messages not displaying in v70+
* Fixed an issue that prevented unlocked moons from becoming free. This fix was present in 0.7.2 but (accidentally?) reverted in 0.7.3
* fixed two issues that prevented trophies from being marked as found in some cases, and added a log message when victory should be achieved
    * this will not fix multiworlds that have already run into the issue, but it shouldn't happen in future worlds
* hid some log spam and fixed an issue that spammed errors when orbiting the company moon
* connections with no password now save the password as an empty string (ES3 cannot serialize objects of null value)
    * Ongoing saves will fail to auto-connect to the multiworld the *first* time you load into them. Simply go through the connection process again and you'll be good-to-go.
</details>

This version does not include logic for the new log and bestiary entry added in v70. Those will be added in a future update. For now, if you wish to add the Giant Sapsucker to logic, simply follow the custom content tutorial.

**The mod will likely still function in game versions prior to v70, but server messages will not appear in chat due to changes made to support v70+.**

## 0.7.3
**<details><summary>Fixes</summary>**
* Fixed modify scrap spawns not spawning any scrap
* Hopefully made deathlink more random, and only kill one person
* Updated the readme to have the correct instructions to add custom content
* (Hopefully) fixed more issues with moon names in custom content
</details>

## 0.7.2
**<details><summary>Fixes</summary>**
* fixed unlocked moons costing credits to visit
</details>

## 0.7.1
**<details><summary>Fixes</summary>**
* Fixed a few bugs related to scrapsanity and trophies
</details>

## 0.7.0
**<details><summary>Fixes</summary>**
* Multiplayer will no longer break after leaving/rejoining a save
* Days left will no longer infinitely go up
* Dying from a death link will no longer cause another one
* AP Apparatus - Custom should now give out the correct check when brought back to the ship
* Vain Shroud has been removed
* One word custom moons will no longer have a blank name
* Custom and new scrap should now send the right check
</details>

**<details><summary>Changes</summary>**
* Added the config terminal command(can be used even when not connected to the multiworld!)
    * When used with no arguments, will show the current value of all the config options, as well as possible values
    * Can be used with two arguments, the setting name and the value.
        * sendapchat - will enable/disable the sending of LC chat messages to archipelago
        * recapchat - will enable/disable the printing of archipelago messages in the LC chat
        * maxchat - will change the maximum amount of characters a single chat message can use
        * recfiller - the big one, will enable/disable filler items automatically activating
* Added the apfiller terminal command
    * Can be used without arguments to show how many of each filler item you've received, as well as how many are queued up.
    * If recfiller is false, you can use apfiller with one argument to activate a queued filler item
* Added a way to allow you to use multiple custom content lethal company worlds in the same multiworld, explained further in the readme
* Renamed all moons to include their number, commands and yaml options that use moon names won't need to change because they look to see if what you entered is contained in any moon name. Since exp is contained in 41 Experimentation, exp would be all you need to enter.
</details>

**Because so much changed, I expect there to be some bugs that slipped through the cracks. If you notice any, please tell me, and I will fix them as fast as I can.**

## 0.6.16
**<details><summary>Fixes</summary>**
* multiplayer finally works
</details>

## 0.6.15
**<details><summary>Fixes</summary>**
* clients can now properly connect to the multiworld
</details>

## 0.6.14
**<details><summary>Fixes</summary>**
* fixed tulip snake odds
</details>

## 0.6.13
**<details><summary>Fixes</summary>**
* fixed bug where players other than the host wouldn't automatically join the multiworld
* fixed bug where AP chat only showed for the host
* fixed bug where items would sometimes unlock but still cost an insane price
* added the ability to search for scrap with the scrap command, now it works with both scrap and moons
</details>

## 0.6.12
**<details><summary>Fixes</summary>**
* fixed store items not having the correct price after being unlocked
</details>

## 0.6.11
**<details><summary>Fixes</summary>**
* Fixed issues with using old apworlds
* Fixed terminal showing scrapsanity checks when scrapsanity was off
* Fixed terminal not closing when it was still locked
* Scrap progress will no longer show on the progress screen if scrapsanity is disabled.
* The scrap command will now show whether or not you found certain scrap and whether or not certain scrap are in logic if scrapsanity is enabled, otherwise it will show the same as before
</details>

**<details><summary>Changes</summary>**
* Added v60 content and removed kidnapper fox checks
</details>

## 0.6.10
**<details><summary>Fixes</summary>**
* fixed AP Apparatus scrapsanity checks not being marked as complete when the apparatus is collected
</details>

## 0.6.9
**<details><summary>Changes</summary>**
* kidnapper fox and vain shroud scans are now in logic
* various bug fixes
</details>

## 0.6.8
**<details><summary>Changes</summary>**
* Added v56 content
</details>

**DO NOT UPDATE IF ON A VERSION BEFORE V55, THE MOD WILL NOT WORK**

## 0.6.7
**<details><summary>Fixes</summary>**
* made the mod compatible with the broken slot data from an old version
</details>

## 0.6.6
**<details><summary>Fixes</summary>**
* updated multiclient to the most recent version, fixing the bug where items couldn't be received
* fixed issues with trophy mode victory conditions not triggering
* progress command now shows trophy mode progress
</details>

**You shouldn't need to re-find any trophies, they saved correctly before, I just didn't check victory correctly (Assurance does not equal assurance lol).**

## 0.6.5
**<details><summary>Fixes</summary>**
* Hopefully added full compatibility to pre 0.6.0 multiworlds(if they still don't work, downgrade to 0.5.21)
</details>

**<details><summary>Changes</summary>**
* Added three new terminal commands:
    * progress: shows your progress in all check categories, as well as your progress towards your goal condition
    * scrap [moon]: shows all scrap accessible on the moon, useful for seeing where scrap is for modify scrap spawns
    * hints: shows all hints in your game, tells you if they are reachable or not yet
* Modified some things to make recovery easier if checks are skipped
</details>

**This version might require you to be on the previous version branch on steam. To do this, right click on lethal company in steam, then click properties, then betas, then swap the dropdown from None to previous.**

## 0.6.4
**<details><summary>Fixes</summary>**
* locked moons are correctly hidden
* embrion and artifice are no longer hidden on the moons screen if they are unlocked
* artifice apparatus now spawns correctly
* (hopefully) added support for pre 0.6.0 apworlds
</details>

## 0.6.3
**<details><summary>Fixes</summary>**
* temporary fix for the mod just deciding to stop creating the items and locations late into a multiworld
</details>

## 0.6.2
**<details><summary>Fixes</summary>**
* Hopefully fixed lag spikes that happened every 5 seconds late game
</details>

**<details><summary>Changes</summary>**
* Added new /resync command that will refresh all received items in case they desync. This used to be done once every five seconds, which was what caused the lag, but due to some changes shouldn't be required anymore.
</details>

## 0.6.1
**<details><summary>Fixes</summary>**
* Trophy mode now should work with custom moons
* Fixed the bug where the game would freeze when attempting to close, needing to be force closed
</details>

## 0.6.0
**<details><summary>Fixes</summary>**
* fixed bug with the mod not realizing it was connected when it autoconnected you
* fixed some scattered bugs
</details>

**<details><summary>Changes</summary>**
* the mod now disconnects from the multiworld after leaving a save file
* updated tracker command to use the new settings
* added GetGameLogicString() function for adding custom content(check the github readme for more info)
* added new v50 content
</details>