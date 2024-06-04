using System;
using LethalAPI.LibTerminal;
using LethalAPI.LibTerminal.Attributes;
using LethalAPI.LibTerminal.Models;
using System.Collections.ObjectModel;
using System.Linq;
using Archipelago.MultiClient.Net.Models;

namespace APLC;

public class TerminalCommands
{
    public static Logic LcLogic;
    
    public static void Patch()
    {
        TerminalModRegistry commands = TerminalRegistry.CreateTerminalRegistry();
        commands.RegisterFrom<TerminalCommands>();
    }

    public static void SetLogic()
    {
        LcLogic = new Logic();
    }

    [TerminalCommand("Tracker", true), CommandInfo("Shows all logically reachable checks")]
    public string TestCommand()
    {
        if (MultiworldHandler.Instance == null) return "Not connected to AP server";

        Collection<Location> locations = LcLogic.GetAccessibleLocations();

        string terminalText = "Currently accessible locations:\n\n";

        string[] lines = new string[locations.Count];

        for (var index = 0; index < locations.Count; index++)
        {
            var location = locations[index];
            lines[index] = location.GetLocationString();
        }
        
        Array.Sort(lines);

        terminalText += string.Join('\n', lines);

        return terminalText;
    }

    [TerminalCommand("Progress", true), CommandInfo("Shows current progress towards goal")]
    public string ProgressCommand()
    {
        try
        {
            if (MultiworldHandler.Instance == null) return "Not connected to AP server";

            Terminal t = Plugin._instance.getTerminal();
            int totalQuota = ((Quota)MultiworldHandler.Instance.GetLocationMap("Quota"))._totalQuota;
            int moneyPerQuota = ((Quota)MultiworldHandler.Instance.GetLocationMap("Quota"))._moneyPerQuotaCheck;
            string result =
                $@"{(MultiworldHandler.Instance.GetGoal() == 1 ? $"Collectathon progress: {MultiworldHandler.Instance.GetCollectathonTracker()}\n\n" : "")}{(MultiworldHandler.Instance.GetGoal() == 2 ? $"Credit progress: {MultiworldHandler.Instance.GetCreditTracker()}\n\n" : "")}Moons:
{GenerateMoonProgressTracker()}

Logs: {t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1}

Bestiary: {t.scannedEnemyIDs.Count}/{t.enemyFiles.Count - 1}

Scrap: {MultiworldHandler.Instance.GetLocationMap("Scrap").GetTrackerText()}

Quota: {((Quota)MultiworldHandler.Instance.GetLocationMap("Quota")).GetTrackerText()}, {totalQuota % moneyPerQuota}/{moneyPerQuota}";
            return result;
        }
        catch (Exception e)
        {
            return e.Message + "\n" + e.StackTrace;
        }
    }

    [TerminalCommand("Scrap", true),
     CommandInfo("Returns the names of every scrap that is accessible on the moon that was entered as an argument.")]
    public string ScrapCommand(Terminal caller, [RemainingText] string text)
    {
        if (text == "")
        {
            return "";
        }

        string result = "";
        
        foreach (var moon in StartOfRound.Instance.levels)
        {
            if (moon.PlanetName.ToLower().Contains(text.ToLower()))
            {
                result += $"Scrap on {moon.PlanetName}:\n";
                foreach (var scrap in moon.spawnableScrap)
                {
                    result+=$" - {scrap.spawnableItem.itemName}\n";
                }

                return result;
            }
        }
        
        return $"No moons found with name {text}";
    }

    [TerminalCommand("Hints", true), CommandInfo("Shows all received hints that haven't yet been completed.")]
    public string HintsCommand()
    {
        if (MultiworldHandler.Instance == null) return "Not connected to AP server";
        
        string result = "Hints:\n";
        int slot = MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot;
        int team = MultiworldHandler.Instance.GetSession().ConnectionInfo.Team;
        Hint[] hints = MultiworldHandler.Instance.GetSession().DataStorage.GetHints(slot, team);
        var accessibleLocations = LcLogic.GetAccessibleLocations();
        foreach (Hint hint in hints)
        {
            string locationName = MultiworldHandler.Instance.GetSession().Locations
                .GetLocationNameFromId(hint.LocationId);
            bool inLogic = false;
            foreach (Location location in accessibleLocations)
            {
                if (location.GetLocationString() == locationName)
                {
                    inLogic = true;
                    break;
                }
            }

            string itemName = MultiworldHandler.Instance.GetSession().Items.GetItemName(hint.ItemId);
            string playerName = MultiworldHandler.Instance.GetSession().Players.GetPlayerAlias(hint.ReceivingPlayer);
            result +=  $"{itemName} for {playerName} is at {locationName} {(inLogic ? "(Reachable)" : "(Unreachable)")}\n";
        }
        return result;
    }

    private static string GenerateMoonProgressTracker()
    {
        string result = "";
        foreach (SelectableLevel moon in StartOfRound.Instance.levels)
        {
            string[] parts = moon.PlanetName.Split(" ");
            string moonName = String.Join(" ", parts.Skip(1).Take(parts.Length - 1).ToArray());
            
            if (moonName == "Gordion" || moonName == "Liquidation")
            {
                continue;
            }

            result += $"    {moonName} {MultiworldHandler.Instance.GetLocationMap(moonName).GetTrackerText()}\n";
        }

        return result;
    }
}