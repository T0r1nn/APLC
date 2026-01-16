using System;

namespace APLC;

/**
 * Handles the terminal modifications for the moon tracker, log tracker, bestiary, store, and money display
 */
public static class TerminalHandler
{
    private static int _moonsIndex = 21;
    private static int _logsIndex = 61;
    private static int _bestiaryIndex = 16;
    private static int _storeIndex = 7;
    private static bool _setIndecies = false;

    public static string MoonTrackerText()  // This never gets called. I don't know why it's here
    {
        return $@"Welcome to the exomoons catalogue.
To route the autopilot to a moon, use the word ROUTE.
To learn about any moon, use the word INFO.{(MwState.Instance.GetGoal() == 1 ? $"\nCollectathon progress: {MwState.Instance.GetCollectathonTracker()}" : "")}{(MwState.Instance.GetGoal() == 2 ? $"\nCredit progress: {MwState.Instance.GetCreditTracker()}" : "")}
____________________________

* The Company building   //   {GetCompanyTrackerText()}

{GetMoonList()}
";
    }

    /** 
     * Displays the moon tracker information in the terminal
     * @param t The terminal to display the information in
     */
    public static void DisplayMoonTracker(Terminal t)   // this was breaking the moon display, was not actually modifying the catalog text, and is incompatible with other mods that modify the moon catalog
    {
        if (!_setIndecies)
        {
            Terminal terminal = Plugin.Instance.GetTerminal();
            for (int i = 0; i < terminal.terminalNodes.allKeywords.Length; i++)
            {
                if (terminal.terminalNodes.allKeywords[i].name == "Moons")
                {
                    _moonsIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Sigurd")
                {
                    _logsIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Bestiary")
                {
                    _bestiaryIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Store")
                {
                    _storeIndex = i;
                }
            }

            _setIndecies = true;
        }

        var moons = t.terminalNodes.allKeywords[_moonsIndex].specialKeywordResult;  // none of this is currently applying
        moons.displayText = $@"Welcome to the exomoons catalogue.
To route the autopilot to a moon, use the word ROUTE.
To learn about any moon, use the word INFO.{(MwState.Instance.GetGoal() == 1 ? $"\nCollectathon progress: {MwState.Instance.GetCollectathonTracker()}" : "")}{(MwState.Instance.GetGoal() == 2 ? $"\nCredit progress: {MwState.Instance.GetCreditTracker()}" : "")}
____________________________

* The Company building   //   {GetCompanyTrackerText()}

{GetMoonList()}
";
    }

    public static string GetMoonList()
    {
        var moons = StartOfRound.Instance.levels;
        string output = "";
        foreach (var moon in moons)
        {
            if (moon.PlanetName.Contains("Gordion") || moon.PlanetName.Contains("Liquidation")) continue;
            
            string moonName = moon.PlanetName;

            var weather = moon.currentWeather;

            if (weather == LevelWeatherType.None)
            {
                output +=
                    $"* {moonName} {MwState.Instance.GetLocationMap(moonName).GetTrackerText()}\n";
            }
            else
            {
                output +=
                    $"* {moonName} ({weather.ToString()}) {MwState.Instance.GetLocationMap(moonName).GetTrackerText()}\n";
            }
        }

        return output;
    }
    
    public static string GetCompanyTrackerText()
    {
        try
        {
            if (MwState.Instance.GetItemMap<MoonItems>("71 Gordion").GetTotal() > 0)    // change back to "Company" if we ever support custom company moons
            {
                return "Buying at [companyBuyingPercent]";
            }
            else
            {
                return "Locked!";
            }
        }
        catch (Exception)
        {
            return "Buying at [companyBuyingPercent]";
        }
    }

    public static void DisplayLogTracker(Terminal t)
    {
        if (!_setIndecies)
        {
            Terminal terminal = Plugin.Instance.GetTerminal();
            for (int i = 0; i < terminal.terminalNodes.allKeywords.Length; i++)
            {
                if (terminal.terminalNodes.allKeywords[i].name == "Moons")
                {
                    _moonsIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Sigurd")
                {
                    _logsIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Bestiary")
                {
                    _bestiaryIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Store")
                {
                    _storeIndex = i;
                }
            }

            _setIndecies = true;
        }
        
        var logs = t.terminalNodes.allKeywords[_logsIndex].specialKeywordResult;
        logs.displayText = $@"SIGURD'S LOG ENTRIES  ({t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1})

To read a log, use keyword ""VIEW"" before its name.
---------------------------------

[currentUnlockedLogsList]


";
    }

    public static void DisplayBestiaryTracker(Terminal t)
    {
        if (!_setIndecies)
        {
            Terminal terminal = Plugin.Instance.GetTerminal();
            for (int i = 0; i < terminal.terminalNodes.allKeywords.Length; i++)
            {
                if (terminal.terminalNodes.allKeywords[i].name == "Moons")
                {
                    _moonsIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Sigurd")
                {
                    _logsIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Bestiary")
                {
                    _bestiaryIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Store")
                {
                    _storeIndex = i;
                }
            }

            _setIndecies = true;
        }
        
        var bestiary = t.terminalNodes.allKeywords[_bestiaryIndex].specialKeywordResult;
        bestiary.displayText = $@"BESTIARY  ({t.scannedEnemyIDs.Count}/{t.enemyFiles.Count - 1})

To access a creature file, type ""INFO"" after its name.
---------------------------------

[currentScannedEnemiesList]


";
    }

    public static void DisplayModifiedShop(Terminal t)
    {
        if (!_setIndecies)
        {
            Terminal terminal = Plugin.Instance.GetTerminal();
            for (int i = 0; i < terminal.terminalNodes.allKeywords.Length; i++)
            {
                if (terminal.terminalNodes.allKeywords[i].name == "Moons")
                {
                    _moonsIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Sigurd")
                {
                    _logsIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Bestiary")
                {
                    _bestiaryIndex = i;
                }
                if (terminal.terminalNodes.allKeywords[i].name == "Store")
                {
                    _storeIndex = i;
                }
            }

            _setIndecies = true;
        }
        
        t.terminalNodes.allKeywords[_storeIndex].specialKeywordResult.displayText = 
        $@"Welcome to the Company store. 
Use words BUY and INFO on any item. 
Order tools in bulk by typing a number.
____________________________

[buyableItemsList]
[buyableVehiclesList]

SHIP UPGRADES:
* Loud horn    //    {((MultiworldHandler.Instance == null || MwState.Instance.GetItemMap<ShipUpgrades>("Loud horn").GetTotal() >= 1) ? "Price: $100" : "Locked!")}
* Signal Translator    //    {((MultiworldHandler.Instance == null || MwState.Instance.GetItemMap<ShipUpgrades>("Signal translator").GetTotal() >= 1) ? "Price: $255" : "Locked!")}
* Teleporter    //    {((MultiworldHandler.Instance == null || MwState.Instance.GetItemMap<ShipUpgrades>("Teleporter").GetTotal() >= 1) ? "Price: $375" : "Locked!")}
* Inverse Teleporter    //    {((MultiworldHandler.Instance == null || MwState.Instance.GetItemMap<ShipUpgrades>("Inverse Teleporter").GetTotal() >= 1) ? "Price: $425" : "Locked!")}

The selection of ship decor rotates per-quota. Be sure to check back next week:
------------------------------
[unlockablesSelectionList]

";
    }

    public static void DisplayMoneyTracker(Terminal t)
    {
        t.topRightText.text =
            $"${t.groupCredits}  {((Quota)MwState.Instance.GetLocationMap("Quota")).GetTrackerText()}";
    }
}