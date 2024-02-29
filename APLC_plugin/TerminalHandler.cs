using System;

namespace APLC;

public static class TerminalHandler
{
    public static void DisplayMoonTracker(Terminal t)
    {
        var moons = t.terminalNodes.allKeywords[21].specialKeywordResult;
        moons.displayText = $@"Welcome to the exomoons catalogue.
To route the autopilot to a moon, use the word ROUTE.
To learn about any moon, use the word INFO.{(MultiworldHandler.Instance.GetGoal() == 1 ? $"\nCollectathon progress: {MultiworldHandler.Instance.GetCollectathonTracker()}" : "")}{(MultiworldHandler.Instance.GetGoal() == 2 ? $"\nCredit progress: {MultiworldHandler.Instance.GetCreditTracker()}" : "")}
____________________________

* The Company building   //   {GetCompanyTrackerText()}

* Experimentation [planetTime] {MultiworldHandler.Instance.GetLocationMap("Experimentation").GetTrackerText()}
* Assurance [planetTime] {MultiworldHandler.Instance.GetLocationMap("Assurance").GetTrackerText()}
* Vow [planetTime] {MultiworldHandler.Instance.GetLocationMap("Vow").GetTrackerText()}

* Offense [planetTime] {MultiworldHandler.Instance.GetLocationMap("Offense").GetTrackerText()}
* March [planetTime] {MultiworldHandler.Instance.GetLocationMap("March").GetTrackerText()}

* Rend [planetTime] {MultiworldHandler.Instance.GetLocationMap("Rend").GetTrackerText()}
* Dine [planetTime] {MultiworldHandler.Instance.GetLocationMap("Dine").GetTrackerText()}
* Titan [planetTime] {MultiworldHandler.Instance.GetLocationMap("Titan").GetTrackerText()}

";
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
        var logs = t.terminalNodes.allKeywords[61].specialKeywordResult;
        logs.displayText = $@"SIGURD'S LOG ENTRIES  ({t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1})

To read a log, use keyword ""VIEW"" before its name.
---------------------------------

[currentUnlockedLogsList]


";
    }

    public static void DisplayBestiaryTracker(Terminal t)
    {
        var bestiary = t.terminalNodes.allKeywords[16].specialKeywordResult;
        bestiary.displayText = $@"BESTIARY  ({t.scannedEnemyIDs.Count}/{t.enemyFiles.Count - 1})

To access a creature file, type ""INFO"" after its name.
---------------------------------

[currentScannedEnemiesList]


";
    }

    public static void DisplayMoneyTracker(Terminal t)
    {
        t.topRightText.text =
            $"${t.groupCredits}  {((Quota)MultiworldHandler.Instance.GetLocationMap("Quota")).GetTrackerText()}";
    }
}