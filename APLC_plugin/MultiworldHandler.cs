using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace APLC;

public class MultiworldHandler
{
    //A list of the names of every received AP item
    private readonly Collection<string> _receivedItemNames = new();

    //The AP session and slot data
    private ArchipelagoSession _session;
    private LoginSuccessful _slotInfo;

    //A map to turn item and location names into the correct handlers
    private readonly Dictionary<string, Items> _itemMap = new();
    private readonly Dictionary<string, Locations> _locationMap = new();
    
    //The instance of the APworld handler
    public static MultiworldHandler Instance;
    
    //The minimum and maximum money that a money check can give
    private readonly int _minMoney;
    private readonly int _maxMoney;
    
    //Shows which trophies are collected
    private readonly object[] _trophyModeComplete = new object[8];
    
    //Shows how much scrap is collected, and the scrap goal
    private int _scrapCollected;
    private readonly int _scrapGoal;

    //0 - trophy mode, 1 - collectathon
    private readonly int _goal;
    
    //true if death link is enabled
    private readonly bool _deathLink;
    //Handles the deathlink
    private readonly DeathLinkService _dlService;

    public MultiworldHandler(string url, int port, string slot, string password)
    {
        _session = ArchipelagoSessionFactory.CreateSession(url, port);
        if (password == "") password = null;
        
        _session.Items.ItemReceived += OnItemReceived;
        _session.MessageLog.OnMessageReceived += OnMessageReceived;

        var result =
            _session.TryConnectAndLogin("Lethal Company", slot, ItemsHandlingFlags.AllItems,
                new Version(0, 4, 4), new[] { "Death Link" }, password: password);

        if (!result.Successful)
        {
            Plugin._instance.LogWarning($"URL: {url}, PORT: {port}, SLOT: {slot}, PASSWORD: {password}");
            var failure = (LoginFailure)result;
            var errorMessage =
                $"Failed to Connect to {url + ":" + port} as {slot}:";
            errorMessage = failure.Errors.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");

            errorMessage = failure.ErrorCodes.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");

            HUDManager.Instance.AddTextToChatOnServer($"AP: <color=red>{errorMessage}</color>");
            _session = null;
            return;
        }

        _slotInfo = (LoginSuccessful)result;
        
        Instance = this;

        CreateItems();
        CreateLocations();
        
        foreach (var item in _session.Items.AllItemsReceived)
        {
            _receivedItemNames.Add(_session.Items.GetItemName(item.Item));
        }
        
        _minMoney = GetSlotSetting("minMoney", 100);
        _maxMoney = GetSlotSetting("maxMoney", 100);
        _scrapGoal = GetSlotSetting("collectathonGoal", 5);
        _session.DataStorage["scrapCollected"].Initialize(_scrapCollected);
        _scrapCollected = _session.DataStorage["scrapCollected"];
        _session.DataStorage["trophies"].Initialize(new JArray(_trophyModeComplete));
        _trophyModeComplete = _session.DataStorage["trophies"];
        _goal = GetSlotSetting("goal");
        _deathLink = GetSlotSetting("deathLink") == 1;
        _dlService = _session.CreateDeathLinkService();
        ProcessItems(_receivedItemNames);
    }

    public void Disconnect()
    {
        _receivedItemNames.Clear();
        _session = null;
        _slotInfo = null;
        Instance = null;
    }

    private void CreateItems()
    {
        try
        {
            //Shop items
            _itemMap.Add("Walkie-talkie", new StoreItems("Walkie-talkie", 0));
            _itemMap.Add("Flashlight", new StoreItems("Flashlight", 1));
            _itemMap.Add("Shovel", new StoreItems("Shovel", 2));
            _itemMap.Add("Lockpicker", new StoreItems("Lockpicker", 3));
            _itemMap.Add("Pro-flashlight", new StoreItems("Pro-flashlight", 4));
            _itemMap.Add("Stun grenade", new StoreItems("Stun grenade", 5));
            _itemMap.Add("Boombox", new StoreItems("Boombox", 6));
            _itemMap.Add("TZP-Inhalant", new StoreItems("TZP-Inhalant", 7));
            _itemMap.Add("Zap gun", new StoreItems("Zap gun", 8));
            _itemMap.Add("Jetpack", new StoreItems("Jetpack", 9));
            _itemMap.Add("Extension ladder", new StoreItems("Extension ladder", 10));
            _itemMap.Add("Radar-booster", new StoreItems("Radar-booster", 11));
            _itemMap.Add("Spray paint", new StoreItems("Spray paint", 12));

            //Ship upgrades
            _itemMap.Add("LoudHorn", new ShipUpgrades("LoudHorn", 26));
            _itemMap.Add("SignalTranslator", new ShipUpgrades("SignalTranslator", 34));
            _itemMap.Add("Teleporter", new ShipUpgrades("Teleporter", 16));
            _itemMap.Add("InverseTeleporter", new ShipUpgrades("InverseTeleporter", 28));

            //Moons
            _itemMap.Add("Experimentation", new MoonItems("Experimentation", 0));
            _itemMap.Add("Assurance", new MoonItems("Assurance", 1));
            _itemMap.Add("Vow", new MoonItems("Vow", 2));
            _itemMap.Add("Offense", new MoonItems("Offense", 7));
            _itemMap.Add("March", new MoonItems("March", 4));
            _itemMap.Add("Rend", new MoonItems("Rend", 5));
            _itemMap.Add("Dine", new MoonItems("Dine", 6));
            _itemMap.Add("Titan", new MoonItems("Titan", 8));

            //Player Upgrades
            _itemMap.Add("Inventory Slot", new PlayerUpgrades("Inventory Slot", GetSlotSetting("inventorySlots", 4)));
            _itemMap.Add("Stamina Bar", new PlayerUpgrades("Stamina Bar", GetSlotSetting("staminaBars", 4)));
            _itemMap.Add("Scanner", new PlayerUpgrades("Scanner", 1 - GetSlotSetting("scanner")));
            _itemMap.Add("Strength Training", new PlayerUpgrades("Strength Training", 0));

            //Filler
            _itemMap.Add("Money", new FillerItems("Money", () =>
            {
                Plugin._instance.getTerminal().groupCredits += Random.RandomRangeInt(_minMoney, _maxMoney + 1);
                return true;
            }));
            _itemMap.Add("HauntTrap", new FillerItems("HauntTrap", () => EnemyTrapHandler.SpawnEnemyByName("dress")));
            _itemMap.Add("BrackenTrap",
                new FillerItems("BrackenTrap", () => EnemyTrapHandler.SpawnEnemyByName("flower")));
            _itemMap.Add("More Time", new FillerItems("More Time", () =>
            {
                TimeOfDay.Instance.timeUntilDeadline+=TimeOfDay.Instance.totalTime;
                TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
                return true;
            }));
            _itemMap.Add("Less Time", new FillerItems("Less Time", () =>
            {
                TimeOfDay.Instance.timeUntilDeadline-=TimeOfDay.Instance.totalTime;
                if (TimeOfDay.Instance.timeUntilDeadline < TimeOfDay.Instance.totalTime)
                {
                    TimeOfDay.Instance.timeUntilDeadline = TimeOfDay.Instance.totalTime;
                }
                TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
                return true;
            }));
            _itemMap.Add("Clone Scrap", new FillerItems("Clone Scrap", () =>
            {
                var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
                    where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                    select obj).ToList();
                Collection<GrabbableObject> objects = new();
                foreach (var scrap in list)
                {
                    if (scrap.scrapValue > 0 && scrap.itemProperties.isScrap)
                    {
                        objects.Add(scrap);
                    }
                }

                if (objects.Count == 0)
                {
                    return false;
                }

                int i = Random.RandomRangeInt(0, objects.Count);

                var gameObject = UnityEngine.Object.Instantiate(objects[i].itemProperties.spawnPrefab,
                    objects[i].transform.position + new Vector3(0f, 0.5f, 0f), Quaternion.identity);
                gameObject.GetComponentInChildren<NetworkObject>().Spawn();
                return true;
            }));
            _itemMap.Add("Birthday Gift", new FillerItems("Birthday Gift", () =>
            {
                Item[] items = Plugin._instance.getTerminal().buyableItemsList;
                int i = Random.RandomRangeInt(0, items.Length);
                Plugin._instance.getTerminal().orderedItemsFromTerminal.Add(i);
                return true;
            }));
        }
        catch (Exception e)
        {
            Plugin._instance.LogError($"{e.Message}\n{e.StackTrace}");
        }
    }

    private void CreateLocations()
    {
        //Moons
        _locationMap.Add("Experimentation",new MoonLocations("Experimentation", GetSlotSetting("moonRank",2), GetSlotSetting("checksPerMoon",3)));
        _locationMap.Add("Assurance",new MoonLocations("Assurance", GetSlotSetting("moonRank",2), GetSlotSetting("checksPerMoon",3)));
        _locationMap.Add("Vow",new MoonLocations("Vow", GetSlotSetting("moonRank",2), GetSlotSetting("checksPerMoon",3)));
        _locationMap.Add("Offense",new MoonLocations("Offense", GetSlotSetting("moonRank",2), GetSlotSetting("checksPerMoon",3)));
        _locationMap.Add("March",new MoonLocations("March", GetSlotSetting("moonRank",2), GetSlotSetting("checksPerMoon",3)));
        _locationMap.Add("Rend",new MoonLocations("Rend", GetSlotSetting("moonRank",2), GetSlotSetting("checksPerMoon",3)));
        _locationMap.Add("Dine",new MoonLocations("Dine", GetSlotSetting("moonRank",2), GetSlotSetting("checksPerMoon",3)));
        _locationMap.Add("Titan",new MoonLocations("Titan", GetSlotSetting("moonRank",2), GetSlotSetting("checksPerMoon",3)));
        
        //Quota
        _locationMap.Add("Quota",new Quota(GetSlotSetting("moneyPerQuotaCheck", 500), GetSlotSetting("numQuota", 20)));
        
        //Bestiary
        _locationMap.Add("Roaming Locust", new BestiaryLocations(15, "Roaming Locust"));
        _locationMap.Add("Manticoil", new BestiaryLocations(13, "Manticoil"));
        _locationMap.Add("Circuit Bee", new BestiaryLocations(14, "Circuit Bee"));
        _locationMap.Add("Hoarding Bug", new BestiaryLocations(4, "Hoarding Bug"));
        _locationMap.Add("Snare Flea", new BestiaryLocations(0, "Snare Flea"));
        _locationMap.Add("Spore Lizard", new BestiaryLocations(11, "Spore Lizard"));
        _locationMap.Add("Hygrodere", new BestiaryLocations(5, "Hygrodere"));
        _locationMap.Add("Bunker Spider", new BestiaryLocations(12, "Bunker Spider"));
        _locationMap.Add("Bracken", new BestiaryLocations(1, "Bracken"));
        _locationMap.Add("Thumper", new BestiaryLocations(2, "Thumper"));
        _locationMap.Add("Coil-Head", new BestiaryLocations(7, "Coil-Head"));
        _locationMap.Add("Jester", new BestiaryLocations(10, "Jester"));
        _locationMap.Add("Forest Keeper", new BestiaryLocations(6, "Forest Keeper"));
        _locationMap.Add("Eyeless Dog", new BestiaryLocations(3, "Eyeless Dog"));
        _locationMap.Add("Earth Leviathan", new BestiaryLocations(9, "Earth Leviathan"));
        _locationMap.Add("Baboon Hawk", new BestiaryLocations(16, "Baboon Hawk"));
        _locationMap.Add("Nutcracker", new BestiaryLocations(17, "Nutcracker"));
        
        //Logs
        _locationMap.Add("Smells Here!", new LogLocations(1,"Smells Here!"));
        _locationMap.Add("Swing of Things", new LogLocations(2,"Swing of Things"));
        _locationMap.Add("Shady", new LogLocations(3,"Shady"));
        _locationMap.Add("Sound Behind the Wall", new LogLocations(4,"Sound Behind the Wall"));
        _locationMap.Add("Goodbye", new LogLocations(5,"Goodbye"));
        _locationMap.Add("Screams", new LogLocations(6,"Screams"));
        _locationMap.Add("Golden Planet", new LogLocations(7,"Golden Planet"));
        _locationMap.Add("Idea", new LogLocations(8,"Idea"));
        _locationMap.Add("Nonsense", new LogLocations(9,"Nonsense"));
        _locationMap.Add("Hiding", new LogLocations(10,"Hiding"));
        _locationMap.Add("Real Job", new LogLocations(11,"Real Job"));
        _locationMap.Add("Desmond", new LogLocations(12,"Desmond"));
    }

    public bool IsConnected()
    {
        return _slotInfo != null;
    }

    public ArchipelagoSession GetSession()
    {
        return _session;
    }

    private int GetSlotSetting(string settingName, int def=0)
    {
        if (_slotInfo == null) return def;
        
        try
        {
            return int.Parse(_slotInfo.SlotData[settingName].ToString());
        }
        catch (Exception)
        {
            return def;
        }
    }

    private void OnMessageReceived(LogMessage message)
    {
        var chat = "AP: ";
        foreach (var part in message.Parts)
        {
            var hexCode = BitConverter.ToString(new[] { part.Color.R, part.Color.G, part.Color.B }).Replace("-", "");
            chat += $"<color=#{hexCode}>{part.Text}</color>";
        }

        switch (message)
        {
            case ChatLogMessage chatLogMessage:
                if (chatLogMessage.Player.Slot == _session.ConnectionInfo.Slot) return;
                break;
        }

        ChatHandler.SendMessage(chat);
    }

    private void OnItemReceived(ReceivedItemsHelper helper)
    {
        string itemName = helper.PeekItemName();
        _receivedItemNames.Add(itemName);
        helper.DequeueItem();

        ProcessItems(_receivedItemNames);
    }

    public void CompleteLocation(string name)
    {
        var id = _session.Locations.GetLocationIdFromName("Lethal Company", name);
        if(_session.Locations.AllLocationsChecked.IndexOf(id) == -1)
            _session.Locations.CompleteLocationChecks(id);
    }
    
    public void CheckLogs()
    {
        foreach (var location in _locationMap.Values)
        {
            switch (location.Type)
            {
                case "logB":
                    ((BestiaryLocations)location).CheckComplete();
                    break;
                case "logF":
                    ((LogLocations)location).CheckComplete();
                    break;
            }
        }
    }

    public Items GetItemMap(string key)
    {
        return _itemMap[key];
    }
    
    public Locations GetLocationMap(string key)
    {
        return _locationMap[key];
    }

    public bool CheckTrophy(string moon)
    {
        return Array.IndexOf(_trophyModeComplete, moon) != -1;
    }

    public void CompleteTrophy(string moon)
    {
        if (Array.IndexOf(_trophyModeComplete, moon) != -1) return;
        for (var i = 0; i < 8; i++)
        {
            if (_trophyModeComplete[i] is string) continue;
            _trophyModeComplete[i] = moon;
            _session.DataStorage["trophies"] = new JArray(_trophyModeComplete);
            break;
        }
        string[] moons = { "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan" };
        if (moons.Any(m => Array.IndexOf(_trophyModeComplete, m) == -1)) return;
        Victory();
    }

    public void AddCollectathonScrap(int amount)
    {
        _scrapCollected += amount;
        _session.DataStorage["scrapCollected"] = _scrapCollected;
        if (_scrapCollected > _scrapGoal)
        {
            Victory();
        }
    }

    private void Victory()
    {
        StatusUpdatePacket victory = new()
        {
            Status = ArchipelagoClientState.ClientGoal
        };
        _session.Socket.SendPacket(victory);
    }

    public string GetCollectathonTracker()
    {
        return $"{_scrapCollected}/{_scrapGoal}";
    }

    public int GetGoal()
    {
        return _goal;
    }

    public void HandleDeathLink()
    {
        if (_deathLink)
            _dlService.SendDeathLink(new DeathLink(_session.Players.GetPlayerName(_slotInfo.Slot), "failed the company."));
    }
    
    private void ResetItems()
    {
        foreach (var item in _itemMap.Values)
        {
            item.Reset();
        }
    }
    
    public void TickItems()
    {
        foreach (var item in _itemMap.Values)
        {
            item.Tick();
        }
        
        RefreshItems();
    }

    private void RefreshItems()
    {
        _receivedItemNames.Clear();

        foreach (var item in _session.Items.AllItemsReceived)
        {
            _receivedItemNames.Add(_session.Items.GetItemName(item.Item));
        }
        
        ProcessItems(_receivedItemNames);
    }

    private void ProcessItems(Collection<string> names)
    {
        ResetItems();
        foreach (var name in names)
        {
            try
            {
                _itemMap[name].OnReceived();
            }
            catch (Exception e)
            {
                Plugin._instance.LogWarning($"{e.Message}\n{e.StackTrace}");
            }
        }
    }
}