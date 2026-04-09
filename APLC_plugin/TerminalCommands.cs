using System;
using System.Collections.ObjectModel;
using System.Linq;
using Archipelago.MultiClient.Net.Models;
using Unity.Netcode;
using Dawn;
using System.Collections.Generic;

namespace APLC;
/**
 * Handles custom terminal commands to view Archipelago information and change mod settings
 */
public class TerminalCommands
{
    public static Logic LcLogic;

    public static void SetLogic()
    {
        LcLogic = new Logic();
    }

    internal static void SetUpTerminalCommands()
    {
        List<TerminalCommandBasicInformation> commandInfo = [];

        Plugin.Logger.LogDebug("Setting up tracker command");
        // Tracker command
        TerminalCommandBasicInformation trackerCommandInfo = new TerminalCommandBasicInformation("ApTracker", "Archipelago", 
            "Shows all logically reachable checks", ClearText.Result | ClearText.Query);
        DawnLib.DefineTerminalCommand(NamespacedKey<DawnTerminalCommandInfo>.From("aplc", "tracker_command"), trackerCommandInfo, builder =>
        {
            builder.SetKeywords(["tracker", "aptracker"]);
            builder.DefineSimpleCommand(simpleBuilder =>
            {
                simpleBuilder.SetResultDisplayText(GetTrackerCommandResult);
            });
        });
        commandInfo.Add(trackerCommandInfo);

        Plugin.Logger.LogDebug("Setting up progress command");
        // Progress command
        TerminalCommandBasicInformation progressCommandInfo = new TerminalCommandBasicInformation("ApProgress", "Archipelago", 
            "Shows current progress towards the chosen goal", ClearText.Result | ClearText.Query);
        DawnLib.DefineTerminalCommand(NamespacedKey<DawnTerminalCommandInfo>.From("aplc", "progress_command"), progressCommandInfo, builder =>
        {
            builder.SetKeywords(["progress", "approgress"]);
            builder.DefineSimpleCommand(simpleBuilder =>
            {
                simpleBuilder.SetResultDisplayText(GetProgressCommandResult);
            });
        });
        commandInfo.Add(progressCommandInfo);

        Plugin.Logger.LogDebug("Setting up scrap command");
        // Scrap command
        TerminalCommandBasicInformation scrapCommandInfo = new TerminalCommandBasicInformation("ApScrap", "Archipelago", 
            "Returns the names of every scrap that is accessible on the moon that was entered as an argument.", ClearText.Result | ClearText.Query);
        DawnLib.DefineTerminalCommand(NamespacedKey<DawnTerminalCommandInfo>.From("aplc", "scrap_command"), scrapCommandInfo, builder =>
        {
            builder.SetKeywords(["scrap", "apscrap"]);
            builder.DefineInputCommand(inputBuilder =>
            {
                inputBuilder.SetResultDisplayText(GetScrapCommandResult);
            });
        });
        commandInfo.Add(scrapCommandInfo);

        Plugin.Logger.LogDebug("Setting up hint command");
        // Hint command
        TerminalCommandBasicInformation hintCommandInfo = new TerminalCommandBasicInformation("ApHints", "Archipelago", 
            "Shows all received hints that haven't yet been completed.", ClearText.Result | ClearText.Query);
        DawnLib.DefineTerminalCommand(NamespacedKey<DawnTerminalCommandInfo>.From("aplc", "hint_command"), hintCommandInfo, builder =>
        {
            builder.SetKeywords(["hint", "hints", "aphint", "aphints"]);
            builder.DefineSimpleCommand(simpleBuilder =>
            {
                simpleBuilder.SetResultDisplayText(GetHintCommandResult);
            });
        });
        commandInfo.Add(hintCommandInfo);

        Plugin.Logger.LogDebug("Setting up config command");
        // Config command
        TerminalCommandBasicInformation configCommandInfo = new TerminalCommandBasicInformation("ApConfig", "Archipelago", 
            "Shows and sets the value of config settings for APLC.", ClearText.Result | ClearText.Query);    // not working
        DawnLib.DefineTerminalCommand(NamespacedKey<DawnTerminalCommandInfo>.From("aplc", "config_command"), configCommandInfo, builder =>
        {
            builder.SetKeywords(["config", "apconfig"]);
            builder.DefineInputCommand(inputBuilder =>
            {
                inputBuilder.SetResultDisplayText(ProcessConfigCommand);
            });
        });
        commandInfo.Add(configCommandInfo);

        Plugin.Logger.LogDebug("Setting up filler command");
        // Filler command
        TerminalCommandBasicInformation fillerCommandInfo = new TerminalCommandBasicInformation("ApFiller", "Archipelago", 
            "See and use available filler items.", ClearText.Result | ClearText.Query);    // not working
        DawnLib.DefineTerminalCommand(NamespacedKey<DawnTerminalCommandInfo>.From("aplc", "filler_command"), fillerCommandInfo, builder =>
        {
            builder.SetKeywords(["filler", "apfiller"]);
            builder.DefineInputCommand(inputBuilder =>
            {
                inputBuilder.SetResultDisplayText(ProcessFillerCommand);
            });
        });
        commandInfo.Add(fillerCommandInfo);

        Plugin.Logger.LogDebug("Setting up world command");
        // Filler command
        TerminalCommandBasicInformation worldCommandInfo = new TerminalCommandBasicInformation("ApWorld", "Archipelago", 
            "USE ONLY FOR ADVANCED CUSTOM CONTENT - CAN BREAK SAVES ---- Use to set the custom apworld's name to connect to custom apworlds\nUsage: 'world [world name]'", ClearText.Result | ClearText.Query);    // not working
        DawnLib.DefineTerminalCommand(NamespacedKey<DawnTerminalCommandInfo>.From("aplc", "world_command"), worldCommandInfo, builder =>
        {
            builder.SetKeywords(["world", "apworld"]);
            builder.DefineInputCommand(inputBuilder =>
            {
                inputBuilder.SetResultDisplayText(ProcessWorldCommand);
            });
        });
        commandInfo.Add(worldCommandInfo);

        // ApHelp command
        TerminalCommandBasicInformation archipelagoHelpCommandInfo = new TerminalCommandBasicInformation("ApHelp", "Help", 
            "To see the list of Archipelago-related commands.", ClearText.Result | ClearText.Query);
        DawnLib.DefineTerminalCommand(NamespacedKey<DawnTerminalCommandInfo>.From("aplc", "aphelp_command"), archipelagoHelpCommandInfo, builder =>
        {
            builder.SetKeywords(["aphelp"]);
            builder.DefineSimpleCommand(simpleBuilder =>
            {
                simpleBuilder.SetResultDisplayText(() =>
                {
                    string helpText = "Archipelago commands:\n\n";
                    foreach (TerminalCommandBasicInformation info in commandInfo)
                    {
                        helpText += $">{info.CommandName}\n{info.Description}\n\n";
                    }
                    return helpText;
                });
            });
        });

        TerminalTextModifier commandInfoModifier = new TerminalTextModifier("storage.*\\S", new SimpleProvider<string>($"$&\n\n>APHELP\n{archipelagoHelpCommandInfo.Description}"))
            .UseRegexPattern(true).SetNodeFromKeyword("help");
    }

    public static string GetTrackerCommandResult()
    {
        if (MultiworldHandler.Instance == null) return "Not connected to a multiworld\n\n";

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

        return terminalText += "\n\n";
    }

    public static string GetProgressCommandResult()
    {
        if (MultiworldHandler.Instance == null) return "Not connected to a multiworld\n\n";

        Terminal t = Plugin.Instance.GetTerminal();
        int totalQuota = ((Quota)MwState.Instance.GetLocationMap("Quota")).TotalQuota;
        int moneyPerQuota = ((Quota)MwState.Instance.GetLocationMap("Quota")).MoneyPerQuotaCheck;
        string result =
            $@"{(MwState.Instance.GetGoal() == 1 ? $"Collectathon progress: {MwState.Instance.GetCollectathonTracker()}\n\n" : "")}{(MwState.Instance.GetGoal() == 2 ? $"Credit progress: {MwState.Instance.GetCreditTracker()}\n\n" : "")}Moons:
{GenerateMoonProgressTracker()}

Logs: {t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1}

Bestiary: {t.scannedEnemyIDs.Count}/{t.enemyFiles.Count - 1}{(MultiworldHandler.Instance.GetSlotSetting("scrapsanity") == 1 ? "\n\nScrap: " + MwState.Instance.GetLocationMap("Scrap").GetTrackerText() : "")}

Quota: {((Quota)MwState.Instance.GetLocationMap("Quota")).GetTrackerText()}, {totalQuota % moneyPerQuota}/{moneyPerQuota}

";
        return result;
    }

    public static string GetScrapCommandResult(string text)
    {
        if (text == "" || StartOfRound.Instance == null)    // the second condition is just to get DawnLib off my back
        {
            return "Usage: scrap [moon name|scrap name]\n\n";
        }

        string result = "";
        if (MultiworldHandler.Instance == null || LcLogic == null || MultiworldHandler.Instance.GetSlotSetting("scrapsanity") == 0)
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

                    return result += "\n";
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
                    return $"No moons or scrap found with the name '{text}'\n\n";
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

                return result += "\n";
            }
            result += $"Scrap on {moonRegion.GetName()}:\n";
            foreach (Connection connection in moonRegion.GetConnections())
            {
                if (connection.GetExit() == null)
                {
                    Plugin.Logger.LogWarning($"A region connected to {moonRegion.GetName()} was null when running command 'scrap'! Skipping this region.");
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

            return result += "\n";
        }
        return $"No moons found with the name '{text}'\n\n";
    }

    public static string GetHintCommandResult()
    {
        if (MultiworldHandler.Instance == null) return "Not connected to a multiworld\n\n";

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
            result += $"{itemName} for {playerName} is at {locationName} {(inLogic ? "(Reachable)" : "(Unreachable)")}\n";
        }
        return result += "\n";
    }

    public static string ProcessConfigCommand(string text)
    {
        if (text == "")
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
DeathLink status(toggledeathlink): {Config.DeathLink}
    - false: you will not send or receive
        DeathLink effects
    - true: you will send and receive DeathLink

To set a config value, type config followed by the name of the setting, then the value.
    Example: config recfiller true

";
        }
        string[] tokens = text.ToLower().Split(' ');
        if (tokens.Length < 2 && !tokens[0].Equals("toggledeathlink"))
        {
            return "No value set\n\n";
        }
        string resultText;
        switch (tokens[0])
        {
            case "recapchat":
                switch (tokens[1])
                {
                    case "true":
                        Plugin.BoundConfig.ShowAPMessagesInChat.Value = true;
                        Config.ShowAPMessagesInChat = true;
                        resultText = "Set recapchat to true";
                        break;
                    case "false":
                        Plugin.BoundConfig.ShowAPMessagesInChat.Value = false;
                        Config.ShowAPMessagesInChat = false;
                        resultText = "Set recapchat to false";
                        break;
                    default:
                        resultText = "Invalid value for recapchat, valid values are 'false' or 'true'";
                        break;
                }
                break;
            case "sendapchat":
                switch (tokens[1])
                {
                    case "true":
                        Plugin.BoundConfig.SendChatMessagesAsAPChat.Value = true;
                        Config.SendChatMessagesAsAPChat = true;
                        resultText = "Set sendapchat to true";
                        break;
                    case "false":
                        Plugin.BoundConfig.SendChatMessagesAsAPChat.Value = false;
                        Config.SendChatMessagesAsAPChat = false;
                        resultText = "Set sendapchat to false";
                        break;
                    default:
                        resultText = "Invalid value for sendapchat, valid values are 'false' or 'true'";
                        break;
                }
                break;
            case "recfiller":   // We get the config values whenever the round starts and store them in the Config fields.
                                // The config fields are then synched with the host's settings if they need to be. We still don't need to save these settings to the save file (except death link).
                                // The config fields are only used because I don't want to overwrite client configs with the host's settings.
                if (!(GameNetworkManager.Instance.localPlayerController.IsHost || GameNetworkManager.Instance.localPlayerController.IsServer)) return "Only the host can change filler reception settings\n\n";
                switch (tokens[1])
                {
                    case "true":
                        Plugin.BoundConfig.FillerTriggersInstantly.Value = true;
                        Config.FillerTriggersInstantly = true;
                        APLCNetworking.Instance.SyncConfigClientRpc(Config.MaxCharactersPerChatMessage, Config.FillerTriggersInstantly, Config.DeathLink);
                        resultText = "Set recfiller to true";
                        break;
                    case "false":
                        Plugin.BoundConfig.FillerTriggersInstantly.Value = false;
                        Config.FillerTriggersInstantly = false;
                        APLCNetworking.Instance.SyncConfigClientRpc(Config.MaxCharactersPerChatMessage, Config.FillerTriggersInstantly, Config.DeathLink);
                        resultText = "Set recfiller to false";
                        break;
                    default:
                        resultText = "Invalid value for recfiller, valid values are 'false' or 'true'";
                        break;
                }
                break;
            case "maxchat":
                if (!(GameNetworkManager.Instance.localPlayerController.IsHost || GameNetworkManager.Instance.localPlayerController.IsServer)) return "Only the host can change the max message length\n\n";
                if (!Int32.TryParse(tokens[1], out int enteredValue) || enteredValue < 20 || enteredValue > 1000)
                {
                    resultText = "Invalid value for maxchat, valid range is 20-1000";
                }
                else
                {
                    Plugin.BoundConfig.MaxCharactersPerChatMessage.Value = enteredValue;
                    Config.MaxCharactersPerChatMessage = Plugin.BoundConfig.MaxCharactersPerChatMessage.Value;
                    HUDManager.Instance.chatTextField.characterLimit = Config.MaxCharactersPerChatMessage;
                    APLCNetworking.Instance.SyncConfigClientRpc(Config.MaxCharactersPerChatMessage, Config.FillerTriggersInstantly, Config.DeathLink);
                    resultText = $"Set maxchat to {enteredValue}";
                }
                break;
            case "toggledeathlink":
                if (!(GameNetworkManager.Instance.localPlayerController.IsHost || GameNetworkManager.Instance.localPlayerController.IsServer)) return "Only the host can change DeathLink settings\n\n";
                if (!Plugin.BoundConfig.OverrideMWDeathlink.Value) return "Unable to change this setting. 'Override yaml death link option' is not enabled in the mod config.\n\n";
                if (MultiworldHandler.Instance == null) return "You must be connected to a multiworld to change this option from the terminal\n\n";

                if (tokens.Length == 1)
                {
                    MultiworldHandler.Instance?.ToggleDeathLink(true);
                    //Config.DeathLink = !Config.DeathLink;
                    SaveManager.SaveConfig();
                    APLCNetworking.Instance.SyncConfigClientRpc(Config.MaxCharactersPerChatMessage, Config.FillerTriggersInstantly, Config.DeathLink);
                    resultText = "Toggled DeathLink";
                    break;
                }
                switch (tokens[1])
                {
                    case "true":
                        MultiworldHandler.Instance?.ToggleDeathLink(false, true);
                        //Config.DeathLink = true;
                        SaveManager.SaveConfig();
                        APLCNetworking.Instance.SyncConfigClientRpc(Config.MaxCharactersPerChatMessage, Config.FillerTriggersInstantly, Config.DeathLink);
                        resultText = "DeathLink is now enabled";
                        break;
                    case "false":
                        MultiworldHandler.Instance?.ToggleDeathLink(false, false);
                        //Config.DeathLink = false;
                        SaveManager.SaveConfig();
                        APLCNetworking.Instance.SyncConfigClientRpc(Config.MaxCharactersPerChatMessage, Config.FillerTriggersInstantly, Config.DeathLink);
                        resultText = "DeathLink is now disabled";
                        break;
                    default:
                        resultText = "Invalid value for toggledeathlink. Accepted values are 'false' or 'true'.";
                        break;
                }
                break;
            default:
                resultText = $"Invalid config setting, name {tokens[0]} does not exist";
                break;
        }
        return resultText += "\n\n";
    }
    public static string ProcessFillerCommand(string text)
    {
        if (MultiworldHandler.Instance == null)
        {
            return "Not connected to a multiworld.\n\n";
        }

        if (text.Equals("")) return @$"Currently available filler items:
More Time - {MwState.Instance.GetItemMap<FillerItems>("More Time").GetReceived() - MwState.Instance.GetItemMap<FillerItems>("More Time").GetUsed()} available
    {MwState.Instance.GetItemMap<FillerItems>("More Time").GetReceived()} received
Clone Scrap - {MwState.Instance.GetItemMap<FillerItems>("Clone Scrap").GetReceived() - MwState.Instance.GetItemMap<FillerItems>("Clone Scrap").GetUsed()} available
    {MwState.Instance.GetItemMap<FillerItems>("Clone Scrap").GetReceived()} received
Birthday Gift - {MwState.Instance.GetItemMap<FillerItems>("Birthday Gift").GetReceived() - MwState.Instance.GetItemMap<FillerItems>("Birthday Gift").GetUsed()} available
    {MwState.Instance.GetItemMap<FillerItems>("Birthday Gift").GetReceived()} received
Money - {MwState.Instance.GetItemMap<FillerItems>("Money").GetReceived() - MwState.Instance.GetItemMap<FillerItems>("Money").GetUsed()} available
    {MwState.Instance.GetItemMap<FillerItems>("Money").GetReceived()} received

To use a filler item, re-enter this command followed by the item's name.

";

        string[] fillerNames = ["More Time", "Clone Scrap", "Birthday Gift", "Money"];
        foreach (var fillerName in fillerNames)
        {
            if (!MwState.Instance.TryGetItemMap<FillerItems>(fillerName, out FillerItems fillerMap)) return $"{fillerName} is not a valid filler item\n\n";
            if (fillerName.ToLower().Contains(text.ToLower()) && fillerMap.GetReceived() > fillerMap.GetUsed())
            {
                return fillerMap.Use() ? $"Used {fillerName} successfully\n\n" : $"Failed to use {fillerName}. Make sure you are in an area where the filler makes sense to use.\n\n";
            }
            if (fillerName.ToLower().Contains(text.ToLower()))
            {
                return $"You do not have any available {fillerName} items to use\n\n";
            }
        }

        return $"No filler item found with name {text}\n\n";
    }
    public static string ProcessWorldCommand(string text)
    {
        if (GameNetworkManager.Instance == null) return "GameNetworkManager is null. Something is very wrong!\n\n";
        if (MultiworldHandler.Instance != null)
        {
            return "Only use when not connected to a multiworld.\n\n";
        }
        Config.GameName = text;
        SaveManager.SaveConfig();
        return $"Set game name to Lethal Company - {text}\n\n";
    }
    
    private static string GenerateMoonProgressTracker()
    {
        return StartOfRound.Instance.levels.Select(moon => moon.PlanetName).Where(moonName => !moonName.Contains("Gordion") && !moonName.Contains("Liquidation")).Aggregate("", (current, moonName) => current + $"    {moonName} {MwState.Instance.GetLocationMap(moonName).GetTrackerText()}\n");
    }
}