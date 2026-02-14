using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using UnityEngine;
using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

namespace APLC;

/**
 * Handles the connection and communication with an Archipelago multiworld session. 
 * This is typically created by the MwState class when loading a save file.
 * Implemented as a singleton, accessible via MultiworldHandler.Instance.
 */
public class MultiworldHandler
{
    //A list of the names of every received AP item
    private readonly Collection<string> _receivedItemNames = new();

    //The AP session and slot data
    private ArchipelagoSession _session;
    private LoginSuccessful _slotInfo;

    //The instance of the APworld handler
    public static MultiworldHandler Instance;

    //true if death link is enabled
    private bool deathLink;             // can't be readonly if we allow players to toggle it in-game

    //Stores all received hints
    private readonly Collection<string> _hints = new();

    //Handles the deathlink
    private readonly DeathLinkService _dlService;
    
    //Events for item handling
    public event AplcEventHandler TickItems;
    public event AplcEventHandler ProcessItems;
    public event AplcEventHandler ResetItems;
    public event AplcEventHandler RefreshItems;
    
    //Connection info
    public ConnectionInfo ApConnectionInfo;
    
    //Game name, will be changed for custom games
    public string Game = "Lethal Company";

    public MultiworldHandler(ConnectionInfo info)
    {
        if (Config.GameName != "")
        {
            Game += " - " + Config.GameName;
        }
        
        _session = ArchipelagoSessionFactory.CreateSession(info.URL, info.Port);
        ApConnectionInfo = info;
        _session.Items.ItemReceived += OnItemReceived;
        _session.MessageLog.OnMessageReceived += OnMessageReceived;
        var result = _session.TryConnectAndLogin(Game, info.Slot, ItemsHandlingFlags.AllItems,
            new Version(0, 6, 2), [], password: info.Password.Equals("") ? null : info.Password);

        if (!result.Successful)
        {
            var failure = (LoginFailure)result;
            var errorMessage =
                $"Failed to Connect to {info.URL + ":" + info.Port} as {info.Slot}:";
            errorMessage = failure.Errors.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");

            errorMessage = failure.ErrorCodes.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");

            HUDManager.Instance.AddTextToChatOnServer($"AP: <color=red>{errorMessage}</color>");
            Plugin.Instance.LogError(errorMessage);
            _session = null;
            return;
        }

        _slotInfo = (LoginSuccessful)result;
        _dlService = _session.CreateDeathLinkService();
        deathLink = Plugin.BoundConfig.OverrideMWDeathlink.Value ? Plugin.BoundConfig.DeathLink.Value : GetSlotSetting("deathLink") == 1;
        if (deathLink)
        {
            _dlService.EnableDeathLink();
        }

        Instance = this;
    }

    public DeathLinkService GetDLService()
    {
        return _dlService;
    }

    /** Toggles DeathLink on or off.
     * If 'toggle' is true, it will invert the current state.
     * If 'toggle' is false, it will set the state to 'value'.
     */
    public void ToggleDeathLink(bool toggle, bool value = false)
    {
        bool shouldEnable;

        if (toggle)
            shouldEnable = !deathLink;
        else
            shouldEnable = value;

        if (shouldEnable)
        {
            _dlService.EnableDeathLink();
            deathLink = true;
            Plugin.BoundConfig.DeathLink.Value = deathLink;
            Config.DeathLink = deathLink;

        }
        else
        {
            _dlService.DisableDeathLink();
            deathLink = false;
            Plugin.BoundConfig.DeathLink.Value = deathLink;
            Config.DeathLink = deathLink;
        }
    }

    public void Disconnect()
    {
        Plugin.Instance.LogInfo("Disconnecting from the multiworld.");
        _session.Socket.DisconnectAsync();
        _receivedItemNames.Clear();
        _session = null;
        _slotInfo = null;
        Instance = null;
        
        SaveManager.SaveConfig();
    }

    public bool IsConnected()
    {
        return _slotInfo != null;
    }

    public bool CheckComplete(string locationName)
    {
        return _session.Locations.AllLocationsChecked.Contains(_session.Locations.GetLocationIdFromName(Game, locationName));
    }

    public ArchipelagoSession GetSession()
    {
        return _session;
    }

    public int GetSlotSetting(string settingName, int def = 0)
    {
        if (_slotInfo == null) return def;

        if (int.TryParse(_slotInfo.SlotData[settingName].ToString(), out int result)) return result;
        return def;
    }
    
    public double GetSlotSettingDouble(string settingName, double def = 0.0)
    {
        if (_slotInfo == null) return def;

        if (double.TryParse(_slotInfo.SlotData[settingName].ToString(), out double result)) return result;
        return def;
    }

    /** 
     * Gets the mapping of scrap items to moons.
     * If the mapping is not found in the slot data, it returns a default mapping after processing.
     */
    public Dictionary<string, string[]> GetScrapToMoonMap()
    {
        string input;

        try
        {
            var map = _slotInfo.SlotData["moon_to_scrap_map"];
            input = map.ToString();
        }
        catch (Exception)
        {
            input = @"{
    ""V-type engine"": [
        ""Experimentation""
    ],
    ""Homemade flashbang"": [
        ""Experimentation""
    ],
    ""Dust pan"": [
        ""Experimentation""
    ],
    ""Steering wheel"": [
        ""Experimentation""
    ],
    ""Yield sign"": [
        ""Experimentation""
    ],
    ""Apparatus"": [
        ""Experimentation"",
        ""Assurance"",
        ""Vow"",
        ""Offense"",
        ""March"",
        ""Titan""
    ],
    ""Hive"": [
        ""Experimentation"",
        ""Assurance"",
        ""Vow"",
        ""March""
    ],
    ""Sapsucker Egg"": [
        ""Assurance"",
        ""Vow"",
        ""Adamance""
    ],
    ""Big bolt"": [
        ""Assurance""
    ],
    ""Bottles"": [
        ""Assurance""
    ],
    ""Cookie mold pan"": [
        ""Assurance""
    ],
    ""Red soda"": [
        ""Assurance""
    ],
    ""Stop sign"": [
        ""Assurance""
    ],
    ""Egg beater"": [
        ""Vow""
    ],
    ""Chemical jug"": [
        ""Vow""
    ],
    ""Flask"": [
        ""Vow""
    ],
    ""Brush"": [
        ""Vow""
    ],
    ""Rubber Ducky"": [
        ""Vow""
    ],
    ""Metal sheet"": [
        ""Offense""
    ],
    ""Gift"": [
        ""Offense""
    ],
    ""Magnifying glass"": [
        ""Offense""
    ],
    ""Remote"": [
        ""Offense""
    ],
    ""Toy robot"": [
        ""Offense""
    ],
    ""Whoopie cushion"": [
        ""March""
    ],
    ""Airhorn"": [
        ""March""
    ],
    ""Clown horn"": [
        ""March""
    ],
    ""Gold bar"": [
        ""March""
    ],
    ""Toy cube"": [
        ""March""
    ],
    ""Painting"": [
        ""Rend""
    ],
    ""Ring"": [
        ""Rend""
    ],
    ""Fancy lamp"": [
        ""Rend""
    ],
    ""Candy"": [
        ""Rend""
    ],
    ""Bell"": [
        ""Rend""
    ],
    ""Shotgun"": [
        ""Rend"",
        ""Dine"",
        ""Titan""
    ],
    ""Tragedy"": [
        ""Dine""
    ],
    ""Jar of pickles"": [
        ""Dine""
    ],
    ""Cash register"": [
        ""Dine""
    ],
    ""Mug"": [
        ""Dine""
    ],
    ""Hairdryer"": [
        ""Dine""
    ],
    ""Comedy"": [
        ""Titan""
    ],
    ""Golden cup"": [
        ""Titan""
    ],
    ""Old phone"": [
        ""Titan""
    ],
    ""Perfume bottle"": [
        ""Titan""
    ],
    ""Pill bottle"": [
        ""Titan""
    ],
    ""Large axle"": [
        ""Common""
    ],
    ""Laser pointer"": [
        ""Common""
    ],
    ""Magic 7 ball"": [
        ""Common""
    ],
    ""Plastic fish"": [
        ""Common""
    ],
    ""Tea kettle"": [
        ""Common""
    ],
    ""Teeth"": [
        ""Common""
    ],
    ""Toothpaste"": [
        ""Common""
    ],
    ""AP Apparatus - Experimentation"": [
        ""Experimentation""
    ],
    ""AP Apparatus - Assurance"": [
        ""Assurance""
    ],
    ""AP Apparatus - Vow"": [
        ""Vow""
    ],
    ""AP Apparatus - Offense"": [
        ""Offense""
    ],
    ""AP Apparatus - March"": [
        ""March""
    ],
    ""AP Apparatus - Rend"": [
        ""Rend""
    ],
    ""AP Apparatus - Dine"": [
        ""Dine""
    ],
    ""AP Apparatus - Titan"": [
        ""Titan""
    ],
    ""Archipelago Chest"": [
        ""Common""
    ]
}";
        }
        
        Plugin.Instance.LogDebug(input);
        
        input = input.Substring(2, input.Length - 6);
        string[] slots = input.Split("],");
        Dictionary<string, string[]> result = new();
        foreach (string slot in slots)
        {
            string[] data = slot.Split("[");
            string scrapName = data[0].Trim();
            
            Plugin.Instance.LogDebug(scrapName);
            
            string[] scrapMoons = data[1].Split(",");
            for (int i = 0; i < scrapMoons.Length; i++)
            {
                if (scrapMoons[i].Length < 3 && scrapMoons.Length == 1)
                {
                    scrapMoons = [];
                    break;
                }

                scrapMoons[i] = scrapMoons[i].Trim().Substring(1, scrapMoons[i].Trim().Length-2);
            }

            scrapName = Char.IsLetter(scrapName.ToCharArray()[0]) ? scrapName[..^1] : scrapName.Substring(1, scrapName.Length - 3);
            
            Plugin.Instance.LogDebug(scrapName);

            result.Add(scrapName, scrapMoons);
        }

        foreach (var key in result.Keys)
        {
            Plugin.Instance.LogDebug(key+":");
            foreach (var moon in result[key])
            {
                Plugin.Instance.LogDebug("    "+moon);
            }
        }


        return result;
        
    }

    private void OnMessageReceived(LogMessage message)
    {
        if (!Config.ShowAPMessagesInChat) return;
        var chat = "AP: ";
        foreach (var part in message.Parts)
        {
            var hexCode = BitConverter.ToString([part.Color.R, part.Color.G, part.Color.B]).Replace("-", "");
            chat += $"<color=#{hexCode}>{part.Text}</color>";
        }

        switch (message)
        {
            case ChatLogMessage chatLogMessage:
                if (chatLogMessage.Player.Slot == _session.ConnectionInfo.Slot) return;
                break;
        }
        
        MethodInfo methodInfo = typeof(HUDManager).GetMethod("AddChatMessage", BindingFlags.Instance | BindingFlags.NonPublic);

        var parameters = new object[] { chat, "", -1, false };

        Debug.Assert(methodInfo != null, nameof(methodInfo) + " != null");

        methodInfo.Invoke(HUDManager.Instance, parameters);
    }

    private void OnItemReceived(IReceivedItemsHelper helper)
    {
        string itemName = helper.PeekItem().ItemName;
        _receivedItemNames.Add(itemName);
        helper.DequeueItem();

        Process(new AplcEventArgs(_receivedItemNames));
    }

    public Collection<string> GetHints()
    {
        return _hints;
    }

    public void CompleteLocation(string name)
    {
        var id = _session.Locations.GetLocationIdFromName(Game, name);
        if (_session.Locations.AllLocationsChecked.IndexOf(id) == -1)
            _session.Locations.CompleteLocationChecks(id);
    }

    

    public void Victory()
    {
        StatusUpdatePacket victory = new()
        {
            Status = ArchipelagoClientState.ClientGoal
        };
        _session.Socket.SendPacket(victory);
    }

    public void HandleDeathLink()
    {
        if (deathLink)
            _dlService.SendDeathLink(new DeathLink(_session.Players.GetPlayerName(_slotInfo.Slot),
                "failed the company."));
    }

    public void Tick(AplcEventArgs args)
    {
        TickItems?.Invoke(this, args);
    }

    public void Reset(AplcEventArgs args)
    {
        ResetItems?.Invoke(this, args);
    }

    public void Refresh(AplcEventArgs args)
    {
        RefreshItems?.Invoke(this, args);
    }

    public void Process(AplcEventArgs args)
    {
        ProcessItems?.Invoke(this, args);
    }

    public Collection<String> GetReceivedItems()
    {
        return _receivedItemNames;
    }
}