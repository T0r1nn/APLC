using System;

namespace APLC;

public static class TerminalHandler
{
    private static int moonsIndex = 21;
    private static int logsIndex = 61;
    private static int bestiaryIndex = 16;
    private static int storeIndex = 7;
    private static bool setIndecies = false;

    public static string MoonTrackerText()
    {
        return $@"Welcome to the exomoons catalogue.
To route the autopilot to a moon, use the word ROUTE.
To learn about any moon, use the word INFO.{(MultiworldHandler.Instance.GetGoal() == 1 ? $"\nCollectathon progress: {MultiworldHandler.Instance.GetCollectathonTracker()}" : "")}{(MultiworldHandler.Instance.GetGoal() == 2 ? $"\nCredit progress: {MultiworldHandler.Instance.GetCreditTracker()}" : "")}
____________________________

* The Company building   //   {GetCompanyTrackerText()}

{GetMoonList()}
";
    }
    
    public static void DisplayMoonTracker(Terminal t)
    {
        if (!setIndecies)
        {
            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Moons")
                {
                    moonsIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Sigurd")
                {
                    logsIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Bestiary")
                {
                    bestiaryIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Store")
                {
                    storeIndex = i;
                }
            }

            setIndecies = true;
        }

        var moons = t.terminalNodes.allKeywords[moonsIndex].specialKeywordResult;
        moons.displayText = $@"Welcome to the exomoons catalogue.
To route the autopilot to a moon, use the word ROUTE.
To learn about any moon, use the word INFO.{(MultiworldHandler.Instance.GetGoal() == 1 ? $"\nCollectathon progress: {MultiworldHandler.Instance.GetCollectathonTracker()}" : "")}{(MultiworldHandler.Instance.GetGoal() == 2 ? $"\nCredit progress: {MultiworldHandler.Instance.GetCreditTracker()}" : "")}
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
            moonName = moonName.Substring(moonName.IndexOf(" ") + 1, moonName.Length - moonName.IndexOf(" ") - 1);

            var weather = moon.currentWeather;

            if (weather == LevelWeatherType.None)
            {
                output +=
                    $"* {moonName} {MultiworldHandler.Instance.GetLocationMap(moonName).GetTrackerText()}\n";
            }
            else
            {
                output +=
                    $"* {moonName} ({weather.ToString()}) {MultiworldHandler.Instance.GetLocationMap(moonName).GetTrackerText()}\n";
            }
        }

        return output;
    }
    
    public static string GetCompanyTrackerText()
    {
        try
        {
            if (MultiworldHandler.Instance.GetItemMap<MoonItems>("Company").GetTotal() > 0)
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
        if (!setIndecies)
        {
            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Moons")
                {
                    moonsIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Sigurd")
                {
                    logsIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Bestiary")
                {
                    bestiaryIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Store")
                {
                    storeIndex = i;
                }
            }

            setIndecies = true;
        }
        
        var logs = t.terminalNodes.allKeywords[logsIndex].specialKeywordResult;
        logs.displayText = $@"SIGURD'S LOG ENTRIES  ({t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1})

To read a log, use keyword ""VIEW"" before its name.
---------------------------------

[currentUnlockedLogsList]


";
    }

    public static void DisplayBestiaryTracker(Terminal t)
    {
        if (!setIndecies)
        {
            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Moons")
                {
                    moonsIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Sigurd")
                {
                    logsIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Bestiary")
                {
                    bestiaryIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Store")
                {
                    storeIndex = i;
                }
            }

            setIndecies = true;
        }
        
        var bestiary = t.terminalNodes.allKeywords[bestiaryIndex].specialKeywordResult;
        bestiary.displayText = $@"BESTIARY  ({t.scannedEnemyIDs.Count}/{t.enemyFiles.Count - 1})

To access a creature file, type ""INFO"" after its name.
---------------------------------

[currentScannedEnemiesList]


";
    }

    public static void DisplayModifiedShop(Terminal t)
    {
        if (!setIndecies)
        {
            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Moons")
                {
                    moonsIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Sigurd")
                {
                    logsIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Bestiary")
                {
                    bestiaryIndex = i;
                }
            }

            for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
            {
                if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Store")
                {
                    storeIndex = i;
                }
            }

            setIndecies = true;
        }
        
        t.terminalNodes.allKeywords[storeIndex].specialKeywordResult.displayText = 
        $@"Welcome to the Company store. 
Use words BUY and INFO on any item. 
Order tools in bulk by typing a number.
____________________________

[buyableItemsList]

SHIP UPGRADES:
* Loud horn    //    {(MultiworldHandler.Instance.GetItemMap<ShipUpgrades>("Loud horn").GetTotal() >= 1 ? "Price: $100" : "Locked!")}
* Signal Translator    //    {(MultiworldHandler.Instance.GetItemMap<ShipUpgrades>("Signal translator").GetTotal() >= 1 ? "Price: $255" : "Locked!")}
* Teleporter    //    {(MultiworldHandler.Instance.GetItemMap<ShipUpgrades>("Teleporter").GetTotal() >= 1 ? "Price: $375" : "Locked!")}
* Inverse Teleporter    //    {(MultiworldHandler.Instance.GetItemMap<ShipUpgrades>("Inverse Teleporter").GetTotal() >= 1 ? "Price: $425" : "Locked!")}

The selection of ship decor rotates per-quota. Be sure to check back next week:
------------------------------
[unlockablesSelectionList]

";
    }

    public static void DisplayMoneyTracker(Terminal t)
    {
        t.topRightText.text =
            $"${t.groupCredits}  {((Quota)MultiworldHandler.Instance.GetLocationMap("Quota")).GetTrackerText()}";
    }
}