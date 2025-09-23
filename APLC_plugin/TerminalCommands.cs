﻿using System;
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
    public string TrackerCommand()
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

            Terminal t = Plugin.Instance.GetTerminal();
            int totalQuota = ((Quota)MwState.Instance.GetLocationMap("Quota")).TotalQuota;
            int moneyPerQuota = ((Quota)MwState.Instance.GetLocationMap("Quota")).MoneyPerQuotaCheck;
            string result =
                $@"{(MwState.Instance.GetGoal() == 1 ? $"Collectathon progress: {MwState.Instance.GetCollectathonTracker()}\n\n" : "")}{(MwState.Instance.GetGoal() == 2 ? $"Credit progress: {MwState.Instance.GetCreditTracker()}\n\n" : "")}Moons:
{GenerateMoonProgressTracker()}

Logs: {t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1}

Bestiary: {t.scannedEnemyIDs.Count}/{t.enemyFiles.Count - 1}{(MultiworldHandler.Instance.GetSlotSetting("scrapsanity") == 1 ? "\n\nScrap: "+MwState.Instance.GetLocationMap("Scrap").GetTrackerText() : "")}

Quota: {((Quota)MwState.Instance.GetLocationMap("Quota")).GetTrackerText()}, {totalQuota % moneyPerQuota}/{moneyPerQuota}";
            return result;
        }
        catch (Exception e)
        {
            return e.Message + "\n" + e.StackTrace;
        }
    }

    [TerminalCommand("Scrap", true),
     CommandInfo("Returns the names of every scrap that is accessible on the moon that was entered as an argument.", "[moon name|scrap name]")]
    public string ScrapCommand(Terminal caller, [RemainingText] string text)
    {
        if (text == "")
        {
            return "Usage: scrap [moon name|scrap name]";
        }

        string result = "";
        if (MultiworldHandler.Instance == null || MultiworldHandler.Instance.GetSlotSetting("scrapsanity") == 0)
        {
            foreach (var moon in StartOfRound.Instance.levels)
            {
                if (moon.PlanetName.ToLower().Contains(text.ToLower()))
                {
                    result += $"Scrap on {moon.PlanetName}:\n";
                    foreach (var scrap in moon.spawnableScrap)
                    {
                        result += $" - {scrap.spawnableItem.itemName}\n";
                    }

                    return result;
                }
            }
        }
        else
        {
            Region moonRegion = LcLogic.GetMoonRegion(text);
            if (moonRegion == null)
            {
                Region scrapRegion = LcLogic.GetScrapRegion(text);
                if (scrapRegion == null)
                {
                    return $"No moons or scrap found with name {text}";
                }

                result += $"Moons with {scrapRegion.GetName()}\n";
                foreach (var region in LcLogic.GetMoonRegions())
                {
                    foreach (var connection in region.GetConnections())
                    {
                        if (connection.GetExit() == scrapRegion)
                        {
                            result +=                             
                                $" - {region.GetName()}{(LcLogic.GetAccessibleLocations().Contains(scrapRegion.GetLocations()[0]) && LcLogic.GetAccessibleRegions().Contains(region) ? "(in logic)" : "(out of logic)")}\n";
                        }
                    }
                }

                return result;
            }
            result += $"Scrap on {moonRegion.GetName()}:\n";
            foreach (Connection connection in moonRegion.GetConnections())
            {
                if (connection.GetExit() == null)
                {
                    Plugin.Instance.LogWarning($"A region connected to {moonRegion.GetName()} was null when running command 'scrap'! Skipping this region.");
                    continue;
                }
                foreach (Location location in connection.GetExit().GetLocations())
                {
                    if (location.GetLocationString().Contains("Scrap"))
                    {
                        result +=
                            $" - {location.GetLocationString().Remove(0, 8)}{(MwState.Instance.GetLocationMap<ScrapLocations>("Scrap").CheckCollected(location.GetLocationString().Remove(0, 8)) ? "(found)" : (LcLogic.GetAccessibleLocations().Contains(location) ? "(in logic)" : "(out of logic)"))}\n";
                    }
                }
            }

            return result;
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

    [TerminalCommand("config", true), CommandInfo("Shows possible values for all config options")]
    public string ConfigCommand()
    {
        return $@"APLC Config Settings:

Send chat messages to Archipelago(sendapchat): {Config.SendChatMessagesAsAPChat}
    - false: in game chat messages will not be sent
        to Archipelago
    - true: in game chat messages will be sent to
        Archipelago
Show AP messages in the chat(recapchat): {Config.ShowAPMessagesInChat}
    - false: archipelago will not send messages
        into the LC Chat
    - true: archipelago will send messages into the 
        LC Chat
Filler items trigger on reception(recfiller): {Config.FillerTriggersInstantly}
    - false: filler items can be triggered by the
        player whenever they want after receiving
        them
    - true: filler items are triggered on reception
Max characters per chat message(maxchat): {Config.MaxCharactersPerChatMessage}
    - range from 20-1000: maximum amount of 
        characters per chat message(default is 50)

To set a config value, type config followed by the name of the setting, then the value.
    Example: config recfiller true
";
    }

    [TerminalCommand("config", true), CommandInfo("Sets the value of config settings for APLC", "[config name] [config value]")]
    public string ConfigSetCommand(Terminal caller, [RemainingText] string text)
    {
        if (text == "")
        {
            return "No setting specified";
        }
        string[] tokens = text.Split(' ');
        if (tokens.Length < 2)
        {
            return "No value set";
        }
        switch (tokens[0])
        {
            case "recapchat":
                switch (tokens[1])
                {
                    case "true":
                        Config.ShowAPMessagesInChat = true;
                        SaveManager.SaveConfig();
                        return "Set recapchat to true";
                    case "false":
                        Config.ShowAPMessagesInChat = false;
                        SaveManager.SaveConfig();
                        return "Set recapchat to false";
                    default:
                        return "Invalid value for recapchat, valid values are 'false' or 'true'";
                }
            case "sendapchat":
                switch (tokens[1])
                {
                    case "true":
                        Config.SendChatMessagesAsAPChat = true;
                        SaveManager.SaveConfig();
                        return "Set sendapchat to true";
                    case "false":
                        Config.SendChatMessagesAsAPChat = false;
                        SaveManager.SaveConfig();
                        return "Set sendapchat to false";
                    default:
                        return "Invalid value for sendapchat, valid values are 'false' or 'true'";
                }
            case "recfiller":
                switch (tokens[1])
                {
                    case "true":
                        Config.FillerTriggersInstantly = true;
                        SaveManager.SaveConfig();
                        return "Set recfiller to true";
                    case "false":
                        Config.FillerTriggersInstantly = false;
                        SaveManager.SaveConfig();
                        return "Set recfiller to false";
                    default:
                        return "Invalid value for recfiller, valid values are 'false' or 'true'";
                }
            case "maxchat":
                try
                {
                    if (Int32.Parse(tokens[1]) < 20 || Int32.Parse(tokens[1]) > 1000)
                    {
                        return "Invalid value for maxchat, valid range is 20-1000";
                    }
                    else
                    {
                        Config.MaxCharactersPerChatMessage = Int32.Parse(tokens[1]);
                        SaveManager.SaveConfig();
                        HUDManager.Instance.chatTextField.characterLimit = Config.MaxCharactersPerChatMessage;
                        return $"Set maxchat to {tokens[1]}";
                    }
                }
                catch (Exception)
                {
                    return "Invalid value for maxchat, valid range is 20-1000";
                }
            default:
                return $"Invalid config setting, name {tokens[0]} does not exist";
        }
    }
    
    [TerminalCommand("apfiller", true), CommandInfo("See filler items available to use")]
    public string FillerCommand(Terminal caller)
    {
        if (MultiworldHandler.Instance == null)
        {
            return "Not connected to the multiworld.";
        }
        
        return @$"Currently available filler items:
More Time - {MwState.Instance.GetItemMap<FillerItems>("More Time").GetReceived() - MwState.Instance.GetItemMap<FillerItems>("More Time").GetUsed()} avail
    {MwState.Instance.GetItemMap<FillerItems>("More Time").GetReceived()} received
Clone Scrap - {MwState.Instance.GetItemMap<FillerItems>("Clone Scrap").GetReceived() - MwState.Instance.GetItemMap<FillerItems>("Clone Scrap").GetUsed()} avail
    {MwState.Instance.GetItemMap<FillerItems>("Clone Scrap").GetReceived()} received
Birthday Gift - {MwState.Instance.GetItemMap<FillerItems>("Birthday Gift").GetReceived() - MwState.Instance.GetItemMap<FillerItems>("Birthday Gift").GetUsed()} avail
    {MwState.Instance.GetItemMap<FillerItems>("Birthday Gift").GetReceived()} received
Money - {MwState.Instance.GetItemMap<FillerItems>("Money").GetReceived() - MwState.Instance.GetItemMap<FillerItems>("Money").GetUsed()} avail
    {MwState.Instance.GetItemMap<FillerItems>("Money").GetReceived()} received";
    }
    
    [TerminalCommand("apfiller", true), CommandInfo("Use available filler items", "[item name]")]
    public string FillerCommand(Terminal caller, [RemainingText] string text)
    {
        if (MultiworldHandler.Instance == null)
        {
            return "Not connected to the multiworld.";
        }

        string[] fillerNames = ["More Time", "Clone Scrap", "Birthday Gift", "Money"];
        foreach (var fillerName in fillerNames)
        {
            if (fillerName.ToLower().Contains(text.ToLower()) && MwState.Instance.GetItemMap<FillerItems>(fillerName).GetReceived() > MwState.Instance.GetItemMap<FillerItems>(fillerName).GetUsed())
            {
                return MwState.Instance.GetItemMap<FillerItems>(fillerName).Use() ? $"Used {fillerName} successfully" : $"Failed to use {fillerName}. Make sure you are in an area where the filler makes sense to use.";
            }
            if (fillerName.ToLower().Contains(text.ToLower()))
            {
                return $"You do not have any available uses of {fillerName}";
            }
        }

        return $"No filler item found with name {text}";
    }

    [TerminalCommand("world"),
     CommandInfo(
         "USE ONLY FOR ADVANCED CUSTOM CONTENT - CAN BREAK SAVES ---- Use to set the custom apworld's name to connect to custom apworlds",
         "[world name]")]
    public string WorldCommand(Terminal caller, [RemainingText] string text)
    {
        if (MultiworldHandler.Instance != null)
        {
            return "Only use when not connected to the multiworld.";
        }
        Config.GameName = text;
        SaveManager.SaveConfig();
        return $"Set game name to Lethal Company - {text}";
    }

    [TerminalCommand("aplogic", true), CommandInfo("Generates a logic string for custom content and allows copying to clipboard.\nUsage: " +
        "\n\taplogic print - Outputs the logic string in the game log." +
        "\n\taplogic copy - Copies the logic string to the clipboard.")]
    public string LogicCommand(Terminal caller, [RemainingText] string text)
    {
        string logic_string = Plugin.Instance.GetGameLogicString();
        if (text.ToLower() == "copy")
        {
            UnityEngine.GUIUtility.systemCopyBuffer = logic_string; 
            return "Successfully copied logic string to clipboard!";
        }
        else if (text == "print")
        {
            Plugin.Instance.LogInfo(logic_string);
            return "The logic string can now be found in the game log.\nIf you wish to copy the logic string to your clipboard, type 'aplogic copy'.";
        }
        else
        {
            return "Invalid argument. Type 'aplogic' to see the logic string, or 'aplogic copy' to copy it to your clipboard.";
        }
    }


    private static string GenerateMoonProgressTracker()
    {
        return StartOfRound.Instance.levels.Select(moon => moon.PlanetName).Where(moonName => !moonName.Contains("Gordion") && !moonName.Contains("Liquidation")).Aggregate("", (current, moonName) => current + $"    {moonName} {MwState.Instance.GetLocationMap(moonName).GetTrackerText()}\n");
    }
}