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
using GameNetcodeStuff;
using HarmonyLib;
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

    //Whether the player has been sent to their starting moon this save.
    private bool _sentToMoon = true;
    
    //Whether the player has been chosen to be killed by deathlink
    public static bool _waitingForDeath;
    public static string _dlMessage;

    //Shows which trophies are collected
    private readonly object[] _trophyModeComplete = new object[8];

    //Shows how much scrap is collected, and the scrap goal
    private int _scrapCollected;
    private readonly int _scrapGoal;

    //0 - trophy mode, 1 - collectathon
    private readonly int _goal;

    //true if death link is enabled
    private readonly bool _deathLink;

    //Stores all received hints
    private readonly Collection<string> _hints = new();

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
                new Version(0, 4, 4), new String[] {}, password: password);

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

        // foreach (var item in _session.Items.AllItemsReceived)
        // {
        //     _receivedItemNames.Add(_session.Items.GetItemName(item.Item));
        // }

        _minMoney = GetSlotSetting("minMoney", 100);
        _maxMoney = GetSlotSetting("maxMoney", 100);
        _scrapGoal = GetSlotSetting("collectathonGoal", 5);
        _session.DataStorage[$"Lethal Company-{_session.Players.GetPlayerName(_session.ConnectionInfo.Slot)}-scrapCollected"].Initialize(_scrapCollected);
        _scrapCollected = _session.DataStorage[$"Lethal Company-{_session.Players.GetPlayerName(_session.ConnectionInfo.Slot)}-scrapCollected"];
        _session.DataStorage[$"Lethal Company-{_session.Players.GetPlayerName(_session.ConnectionInfo.Slot)}-trophies"].Initialize(new JArray(_trophyModeComplete));
        _trophyModeComplete = _session.DataStorage[$"Lethal Company-{_session.Players.GetPlayerName(_session.ConnectionInfo.Slot)}-trophies"];
        _goal = GetSlotSetting("goal");
        _deathLink = GetSlotSetting("deathLink") == 1;
        _dlService = _session.CreateDeathLinkService();
        if (_deathLink)
        {
            _dlService.OnDeathLinkReceived += KillRandom;
            _dlService.EnableDeathLink();
        }
        ProcessItems(_receivedItemNames);
    }

    public string GetStartingMoon()
    {
        foreach (var itemName in _receivedItemNames)
        {
            Items item = GetItemMap(itemName);
            if (item.GetType() == typeof(MoonItems))
            {
                return itemName;
            }
        }

        return null;
    }
    
    private static bool GoToMoon()
    {
        if (!StartOfRound.Instance.localPlayerController.IsHost) return true;
        var moonInd = 0;
        var moonName = Instance.GetStartingMoon();
        if (moonName == null) return false;

        for ( var i = 0; i < StartOfRound.Instance.levels.Length; i++)
        {
            var level = StartOfRound.Instance.levels[i];
            if (level.PlanetName.ToLower().Contains(moonName.ToLower()))
            {
                moonInd = i;
            }
        }

        StartOfRound.Instance.ChangeLevelServerRpc(moonInd, Plugin._instance.getTerminal().groupCredits);
        return true;
    }

    private static void KillRandom(DeathLink link)
    {
        Plugin._instance.LogWarning("Received death link");
        try
        {
            //ChatHandler.SendMessage($"AP: {link.Cause}");

            int selected = Random.Range(0, GameNetworkManager.Instance.connectedPlayers);
            PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
            ulong[] steamIds = new ulong[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                steamIds[i] = players[i].playerSteamId;
            }

            Array.Sort(steamIds);
            Array.Reverse(steamIds);

            Plugin._instance.LogWarning("Attempting to kill player with steam id " + steamIds[selected]);

            if (StartOfRound.Instance.localPlayerController.playerSteamId == steamIds[selected])
            {
                _waitingForDeath = true;
                _dlMessage = link.Cause;
            }
        }
        catch (Exception e)
        {
            Plugin._instance.LogError(e.Message+"\n"+e.StackTrace);
        }
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
            _itemMap.Add("Experimentation", new MoonItems("Experimentation"));
            _itemMap.Add("Assurance", new MoonItems("Assurance"));
            _itemMap.Add("Vow", new MoonItems("Vow"));
            _itemMap.Add("Offense", new MoonItems("Offense"));
            _itemMap.Add("March", new MoonItems("March"));
            _itemMap.Add("Rend", new MoonItems("Rend"));
            _itemMap.Add("Dine", new MoonItems("Dine"));
            _itemMap.Add("Titan", new MoonItems("Titan"));
            if (GetSlotSetting("randomizecompany") == 1)
            {
                _itemMap.Add("Company", new MoonItems("Company"));
            }

            //Player Upgrades
            _itemMap.Add("Inventory Slot", new PlayerUpgrades("Inventory Slot", GetSlotSetting("inventorySlots", 4)));
            _itemMap.Add("Stamina Bar", new PlayerUpgrades("Stamina Bar", GetSlotSetting("staminaBars", 4)));
            _itemMap.Add("Scanner", new PlayerUpgrades("Scanner", 1 - GetSlotSetting("scanner")));
            _itemMap.Add("Strength Training", new PlayerUpgrades("Strength Training", 0));
            if (GetSlotSetting("randomizeterminal") == 1)
            {
                _itemMap.Add("Terminal", new PlayerUpgrades("Terminal", 0));
            }

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
                TimeOfDay.Instance.timeUntilDeadline += TimeOfDay.Instance.totalTime;
                TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
                ChatHandler.SendMessage("__updateTime "+TimeOfDay.Instance.timeUntilDeadline);
                return true;
            }));
            _itemMap.Add("Less Time", new FillerItems("Less Time", () =>
            {
                TimeOfDay.Instance.timeUntilDeadline -= TimeOfDay.Instance.totalTime;
                if (TimeOfDay.Instance.timeUntilDeadline < TimeOfDay.Instance.totalTime)
                {
                    TimeOfDay.Instance.timeUntilDeadline = TimeOfDay.Instance.totalTime;
                }

                TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
                ChatHandler.SendMessage("__updateTime "+TimeOfDay.Instance.timeUntilDeadline);
                return true;
            }));
            _itemMap.Add("Clone Scrap", new FillerItems("Clone Scrap", () =>
            {
                var list = (from obj in GameObject.Find("/Environment/HangarShip")
                        .GetComponentsInChildren<GrabbableObject>()
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
                gameObject.GetComponent<GrabbableObject>().SetScrapValue(objects[i].scrapValue);
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
        int lowGrade;
        int medGrade;
        int highGrade;
        if (GetSlotSetting("splitgrades") == 1)
        {
            lowGrade = GetSlotSetting("lowMoon", 2);
            medGrade = GetSlotSetting("medMoon", 2);
            highGrade = GetSlotSetting("highMoon", 2);
        }
        else
        {
            lowGrade = medGrade = highGrade = GetSlotSetting("moonRank", 2);
        }
        //Moons
        _locationMap.Add("Experimentation",
            new MoonLocations("Experimentation", lowGrade, GetSlotSetting("checksPerMoon", 3)));
        _locationMap.Add("Assurance",
            new MoonLocations("Assurance", lowGrade, GetSlotSetting("checksPerMoon", 3)));
        _locationMap.Add("Vow",
            new MoonLocations("Vow", lowGrade, GetSlotSetting("checksPerMoon", 3)));
        _locationMap.Add("Offense",
            new MoonLocations("Offense", medGrade, GetSlotSetting("checksPerMoon", 3)));
        _locationMap.Add("March",
            new MoonLocations("March", medGrade, GetSlotSetting("checksPerMoon", 3)));
        _locationMap.Add("Rend",
            new MoonLocations("Rend", highGrade, GetSlotSetting("checksPerMoon", 3)));
        _locationMap.Add("Dine",
            new MoonLocations("Dine", highGrade, GetSlotSetting("checksPerMoon", 3)));
        _locationMap.Add("Titan",
            new MoonLocations("Titan", highGrade, GetSlotSetting("checksPerMoon", 3)));

        //Quota
        _locationMap.Add("Quota", new Quota(GetSlotSetting("moneyPerQuotaCheck", 500), GetSlotSetting("numQuota", 20)));

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
        _locationMap.Add("Smells Here!", new LogLocations(1, "Smells Here!"));
        _locationMap.Add("Swing of Things", new LogLocations(2, "Swing of Things"));
        _locationMap.Add("Shady", new LogLocations(3, "Shady"));
        _locationMap.Add("Sound Behind the Wall", new LogLocations(4, "Sound Behind the Wall"));
        _locationMap.Add("Goodbye", new LogLocations(5, "Goodbye"));
        _locationMap.Add("Screams", new LogLocations(6, "Screams"));
        _locationMap.Add("Golden Planet", new LogLocations(7, "Golden Planet"));
        _locationMap.Add("Idea", new LogLocations(8, "Idea"));
        _locationMap.Add("Nonsense", new LogLocations(9, "Nonsense"));
        _locationMap.Add("Hiding", new LogLocations(10, "Hiding"));
        _locationMap.Add("Real Job", new LogLocations(11, "Real Job"));
        _locationMap.Add("Desmond", new LogLocations(12, "Desmond"));

        //Scrap
        string[] checkNames =
        {
            "Airhorn", "Apparatice", "Bee Hive", "Big bolt", "Bottles", "Brass bell", "Candy", "Cash register",
            "Chemical jug", "Clown horn", "Coffee mug", "Comedy", "Cookie mold pan", "DIY-Flashbang", "Double-barrel", "Dust pan",
            "Egg beater", "Fancy lamp", "Flask", "Gift Box", "Gold bar", "Golden cup", "Hair brush", "Hairdryer",
            "Jar of pickles", "Large axle", "Laser pointer", "Magic 7 ball", "Magnifying glass", "Old phone",
            "Painting", "Perfume bottle", "Pill bottle", "Plastic fish", "Red soda", "Remote", "Ring", "Robot toy",
            "Rubber Ducky", "Steering wheel", "Stop sign", "Tattered metal sheet", "Tea kettle", "Teeth", "Toothpaste",
            "Toy cube", "Tragedy", "V-type engine", "Whoopie-Cushion", "Yield sign"
        };

        string[] scrapNames =
        {
            "Airhorn", "Apparatus", "Hive", "Big bolt", "Bottles", "Bell", "Candy", "Cash register",
            "Chemical jug", "Clown horn", "Mug", "Comedy", "Cookie mold pan", "Homemade flashbang", "Shotgun", "Dust pan",
            "Egg beater", "Fancy lamp", "Flask", "Gift", "Gold bar", "Golden cup", "Brush", "Hairdryer",
            "Jar of pickles", "Large axle", "Laser pointer", "Magic 7 ball", "Magnifying glass", "Old phone",
            "Painting", "Perfume bottle", "Pill bottle", "Plastic fish", "Red soda", "Remote", "Ring", "Toy robot",
            "Rubber Ducky", "Steering wheel", "Stop sign", "Metal sheet", "Tea kettle", "Teeth", "Toothpaste",
            "Toy cube", "Tragedy", "V-type engine", "Whoopie cushion", "Yield sign"
        };
        _locationMap.Add("Scrap", new ScrapLocations(scrapNames, checkNames));

        // Terminal t = Plugin._instance.getTerminal();
        // TerminalKeyword ap = ScriptableObject.CreateInstance<TerminalKeyword>();
        // ap.word = "ap";
        // CompatibleNoun hints = new CompatibleNoun
        // {
        //     noun = ScriptableObject.CreateInstance<TerminalKeyword>()
        // };
        // hints.noun.word = "hints";
        // hints.result = ScriptableObject.CreateInstance<TerminalNode>();
        // hints.result.displayText = "This is a test";
        // ap.compatibleNouns = new[]
        // {
        //     hints
        // };
        // t.terminalNodes.allKeywords.AddItem(ap);
    }

    public bool IsConnected()
    {
        return _slotInfo != null;
    }

    public ArchipelagoSession GetSession()
    {
        return _session;
    }

    public int GetSlotSetting(string settingName, int def = 0)
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
            // case HintItemSendLogMessage chatLogMessage:
            //     string hint = chatLogMessage.Parts.Aggregate("", (current, part) => current + part.Text);
            //     if (!_hints.Contains(hint))
            //     {
            //         _hints.Add(hint);
            //     }
            //     break;
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

    public Collection<string> GetHints()
    {
        return _hints;
    }

    public void CompleteLocation(string name)
    {
        var id = _session.Locations.GetLocationIdFromName("Lethal Company", name);
        if (_session.Locations.AllLocationsChecked.IndexOf(id) == -1)
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
    
    public T GetItemMap<T>(string key) where T : Items
    {
        return (T)_itemMap[key];
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
            _session.DataStorage[$"Lethal Company-{_session.Players.GetPlayerName(_session.ConnectionInfo.Slot)}-trophies"] = new JArray(_trophyModeComplete);
            break;
        }

        string[] moons = { "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan" };
        if (moons.Any(m => Array.IndexOf(_trophyModeComplete, m) == -1)) return;
        Victory();
    }

    public void AddCollectathonScrap(int amount)
    {
        _scrapCollected += amount;
        _session.DataStorage[$"Lethal Company-{_session.Players.GetPlayerName(_session.ConnectionInfo.Slot)}-scrapCollected"] = _scrapCollected;
        if (_scrapCollected >= _scrapGoal)
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
            _dlService.SendDeathLink(new DeathLink(_session.Players.GetPlayerName(_slotInfo.Slot),
                "failed the company."));
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
        string planetName = StartOfRound.Instance.currentLevel.PlanetName.Split(" ")[1];
        if (planetName == "Gordion")
        {
            planetName = "Company";
        }

        if (planetName != "Company" || GetSlotSetting("randomizecompany") == 1)
        {
            if (GetItemMap<MoonItems>(planetName).GetTotal() < 1 || (GetStartingMoon() != planetName && GetSlotSetting("randomizeterminal")==1 && GetItemMap<PlayerUpgrades>("Terminal").GetNum() < 1))
            {
                _sentToMoon = false;
            }
        }
        
        if (!_sentToMoon)
        {
            if (GoToMoon())
            {
                _sentToMoon = true;
            }
        }

        if (_waitingForDeath)
        {
            GameNetworkManager.Instance.localPlayerController.KillPlayer(Vector3.forward, true, CauseOfDeath.Blast);
            ChatHandler.SendMessage($"AP: {_dlMessage}");
            _waitingForDeath = false;
            _dlMessage = "";
        }
        
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
        int flashlights = 0;
        string[] flashlightNames = { "Flashlight", "Pro-flashlight" };
        foreach (var name in names)
        {
            if (name == "Progressive Flashlight")
            {
                try
                {
                    _itemMap[flashlightNames[flashlights]].OnReceived();
                    flashlights++;
                }
                catch (Exception)
                {
                    //Ignore exception
                }
            }
            else if (name == "Company Building")
            {
                try
                {
                    _itemMap["Company"].OnReceived();
                }
                catch (Exception)
                {
                    //Ignore exception
                }
            }
            else
            {
                try
                {
                    _itemMap[name].OnReceived();
                }
                catch (Exception)
                { 
                    //Ignore exception
                }
            }
        }
    }
}