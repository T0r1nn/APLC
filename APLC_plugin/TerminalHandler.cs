using System;
using System.Linq;
using Archipelago.MultiClient.Net.Models;
using Dawn;
using UnityEngine;

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

public class APLCPurchasePredicate(DawnMoonInfo moonInfo, ITerminalPurchasePredicate priorPredicate) : ITerminalPurchasePredicate
{
    DawnMoonInfo moonInfo = moonInfo;
    ITerminalPurchasePredicate priorPredicate = priorPredicate;
    private TerminalNode _failNode = null;

    TerminalPurchaseResult ITerminalPurchasePredicate.CanPurchase()
    {
        // if the multiworldhandler is null, check the predicate. otherwise, check if the moon item has been received
        // if the predicate is also null, set the location to always accesible. otherwise, just use that predicate
        if (MultiworldHandler.Instance == null)
        {
            if (priorPredicate == null) return TerminalPurchaseResult.Success();
            return priorPredicate.CanPurchase();
        }
        if (MultiworldHandler.Instance.GetReceivedItems().Contains(moonInfo.Level.PlanetName) || (moonInfo.Level.PlanetName == "71 Gordion" && MultiworldHandler.Instance.GetReceivedItems().Contains("Company Building")))
        {
            return TerminalPurchaseResult.Success();
        }
        if (_failNode == null)
        {
            _failNode = ScriptableObject.CreateInstance<TerminalNode>();
            _failNode.name = $"{moonInfo.Level.PlanetName.Replace(" ", "").SkipWhile(x => !char.IsLetter(x)).ToArray()}APLCTerminalPredicateFail";
            _failNode.displayText = "This moon is not unlocked yet! Find it in the multiworld to travel there.";
        }
        return TerminalPurchaseResult.Fail(_failNode).SetOverrideName($"{(moonInfo.Level.PlanetName == "71 Gordion" ? "The Company building" : moonInfo.GetNumberlessPlanetName())} (Locked)");
    }
}