using System;
using Archipelago.MultiClient.Net.Models;
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
        logs.displayText = $@"SIGURD'S LOG ENTRIES  ({t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1} found)

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
        bestiary.displayText = $@"BESTIARY  ({t.scannedEnemyIDs.Count}/{t.enemyFiles.Count - 1} scanned)

To access a creature file, type ""INFO"" after its name.
---------------------------------

[currentScannedEnemiesList]


";
    }

    public static void DisplayMoneyTracker(Terminal t)  // todo: remove this
    {
        t.topRightText.text =
            $"${t.groupCredits}  {((Quota)MwState.Instance.GetLocationMap("Quota")).GetTrackerText()}";
    }
}