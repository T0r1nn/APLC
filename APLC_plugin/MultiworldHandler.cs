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
    
    //Store items to randomize
    private readonly Item[] store;
    
    //Moons to randomize
    private readonly SelectableLevel[] moons;
    
    //Bestiary data
    private readonly Dictionary<string, Collection<Tuple<string, double>>> bestiaryData;
    
    //Scrap data
    private readonly Dictionary<string, Collection<Tuple<string, double>>> scrapData;

    public MultiworldHandler(string url, int port, string slot, string password)
    {
        Plugin._instance.LogWarning($"URL: {url}, PORT: {port}, SLOT: {slot}, PASSWORD: {password}");
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

        _goal = GetSlotSetting("goal");

        var logic = Plugin._instance.GetGameLogic();

        store = logic.Item1;
        moons = logic.Item2;
        bestiaryData = logic.Item3;
        scrapData = logic.Item4;

        _trophyModeComplete = new object[moons.Length];

        CreateLocations();
        CreateItems();

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
        _deathLink = GetSlotSetting("deathLink") == 1;
        _dlService = _session.CreateDeathLinkService();
        if (_deathLink)
        {
            _dlService.OnDeathLinkReceived += KillRandom;
            _dlService.EnableDeathLink();
        }
        ProcessItems(_receivedItemNames);
        TerminalCommands.SetLogic();
                    
        ES3.Save<string>("ArchipelagoURL", url, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save<int>("ArchipelagoPort", port, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save<string>("ArchipelagoSlot", slot, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save<string>("ArchipelagoPassword", password, GameNetworkManager.Instance.currentSaveFileName);
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
        _session.Socket.DisconnectAsync();
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
            for (int i = 0; i < store.Length; i++)
            {
                _itemMap.Add(store[i].itemName, new StoreItems(store[i].itemName, i));
            }

            //Ship upgrades
            _itemMap.Add("LoudHorn", new ShipUpgrades("LoudHorn", 26));
            _itemMap.Add("SignalTranslator", new ShipUpgrades("SignalTranslator", 34));
            _itemMap.Add("Teleporter", new ShipUpgrades("Teleporter", 16));
            _itemMap.Add("InverseTeleporter", new ShipUpgrades("InverseTeleporter", 28));

            //Moons
            for (int i = 0; i < moons.Length; i++)
            {
                string moonName = moons[i].PlanetName;
                moonName = moonName.Substring(moonName.IndexOf(" ") + 1, moonName.Length - moonName.IndexOf(" ") - 1);
                if (moonName.Contains("Gordion") || moonName.Contains("Liquidation")) continue;
                _itemMap.Add(moonName, new MoonItems(moonName));
            }
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
            _itemMap.Add("Company Credit", new PlayerUpgrades("Company Credit", 0));

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
                    TimeOfDay.Instance.timeUntilDeadline += TimeOfDay.Instance.totalTime;
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
        Terminal t = Plugin._instance.getTerminal();
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
        for (int i = 0; i < moons.Length; i++)
        {
            if (moons[i].PlanetName.Contains("Gordion") || moons[i].PlanetName.Contains("Liquidation")) continue;
            string moonName = moons[i].PlanetName;
            moonName = moonName.Substring(moonName.IndexOf(" ") + 1, moonName.Length - moonName.IndexOf(" ") - 1);
            int keywordIndex = 0;
            int terminalIndex = 0;
            for (int j = 0; j < t.terminalNodes.allKeywords.Length; j++)
            {
                if (t.terminalNodes.allKeywords[j].name == "Route")
                {
                    keywordIndex = j;
                }
            }
            for (var j = 0; j < t.terminalNodes.allKeywords[keywordIndex].compatibleNouns.Length; j++)
            {
                if (t.terminalNodes.allKeywords[keywordIndex].compatibleNouns[j].noun.word.ToLower()
                    .Contains(moonName.ToLower()))
                {
                    terminalIndex = j;
                }
            }

            double cost = t.terminalNodes.allKeywords[keywordIndex].compatibleNouns[terminalIndex].result.itemCost;

            if (cost < 100 && moons[i].factorySizeMultiplier <= 1.15)
            {
                _locationMap.Add(moonName, new MoonLocations(moonName, lowGrade, GetSlotSetting("checksPerMoon", 3)));
                Plugin._instance.LogWarning($"Easy: {moonName}");
            }
            else if (cost < 120)
            {
                _locationMap.Add(moonName, new MoonLocations(moonName, medGrade, GetSlotSetting("checksPerMoon", 3)));
                Plugin._instance.LogWarning($"Medium: {moonName}");
            }
            else
            {
                _locationMap.Add(moonName, new MoonLocations(moonName, highGrade, GetSlotSetting("checksPerMoon", 3)));
                Plugin._instance.LogWarning($"Hard: {moonName}");
            }
        }

        //Quota
        _locationMap.Add("Quota", new Quota(GetSlotSetting("moneyPerQuotaCheck", 500), GetSlotSetting("numQuota", 20)));

        //Bestiary
        foreach (var key in bestiaryData.Keys)
        {
            int id = 0;
            for (int i = 0; i < t.enemyFiles.Count; i++)
            {
                if (t.enemyFiles[i].creatureName.Contains(key))
                {
                    id = i;
                    break;
                }
            }
            _locationMap.Add(key, new BestiaryLocations(id, key));
        }

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
        if (GetSlotSetting("fixscrapsanity") == 1)
        {

            Dictionary<string, string[]> scrapToMoonMap = GetScrapToMoonMap();

            Dictionary<string, SpawnableItemWithRarity> scrapNameToScrapMap = new Dictionary<string, SpawnableItemWithRarity>();

            foreach (var moon in moons)
            {
                List<SpawnableItemWithRarity> scrap = moon.spawnableScrap;
                foreach (SpawnableItemWithRarity item in scrap)
                {
                    scrapNameToScrapMap.TryAdd(
                        item.spawnableItem.name.Contains("ap_apparatus_")
                            ? item.spawnableItem.name
                            : item.spawnableItem.itemName, item);

                    item.rarity = item.spawnableItem.itemName == "Archipelago Chest" ? 45 : 30;
                }
            }
            
            foreach (var moon in moons)
            {
                try
                {
                    List<SpawnableItemWithRarity> scrap = moon.spawnableScrap;
                    scrap.Clear();
                    foreach (string scrapName in scrapToMoonMap.Keys)
                    {
                        if (scrapToMoonMap[scrapName].Any(moonName=>moon.PlanetName.Contains(moonName)))
                        {
                            try
                            {
                                string keyName = scrapName;
                                if (scrapName.Contains("AP Apparatus"))
                                {
                                    keyName = $"ap_apparatus_{moon.PlanetName.Split(' ')[1].ToLower()}";
                                }
                                Plugin._instance.LogWarning(keyName);
                                SpawnableItemWithRarity item = scrapNameToScrapMap[keyName];
                                scrap.Add(item);
                            }
                            catch (Exception e)
                            {
                                if (scrapName.Contains("AP Apparatus"))
                                {
                                    Plugin._instance.LogWarning(e.Message + "\n" + e.StackTrace);
                                    SpawnableItemWithRarity item = scrapNameToScrapMap["ap_apparatus_custom"];
                                    scrap.Add(item);
                                }
                            }
                        }
                        else if (scrapToMoonMap[scrapName].Any(moonName=>"Common".Contains(moonName)))
                        {                            
                            SpawnableItemWithRarity item = scrapNameToScrapMap[scrapName];
                            scrap.Add(item);
                        }
                    }

                    // if (GetGoal() == 1)
                    // {
                    //     scrap.Add(scrapNameToScrapMap["Archipelago Chest"]);
                    // }
                    // else if (GetGoal() == 0)
                    // {
                    //     try
                    //     {
                    //         scrap.Add(scrapNameToScrapMap[$"ap_apparatus_{moon.PlanetName.Split(' ')[1].ToLower()}"]);
                    //     }
                    //     catch (Exception)
                    //     {
                    //         /*
                    //          * Create a new apparatus item for this moon.
                    //          */
                    //     }
                    // }
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        string[] scrapNames = new string[scrapData.Keys.Count];
        int ind = 0;
        foreach (var key in scrapData.Keys)
        {
            scrapNames[ind] = key;
            ind++;
        }

        _locationMap.Add("Scrap", new ScrapLocations(scrapNames, scrapNames));
    }

    public bool IsConnected()
    {
        return _slotInfo != null;
    }

    public bool CheckComplete(string locationName)
    {
        return _session.Locations.AllLocationsChecked.Contains(_session.Locations.GetLocationIdFromName("Lethal Company", locationName));
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
    
    public double GetSlotSettingDouble(string settingName, double def = 0.0)
    {
        if (_slotInfo == null) return def;

        try
        {
            return double.Parse(_slotInfo.SlotData[settingName].ToString());
        }
        catch (Exception)
        {
            return def;
        }
    }

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
]
""Homemade flashbang"": [
""Experimentation""
]
""Dust pan"": [
""Experimentation""
]
""Steering wheel"": [
""Experimentation""
]
""Yield sign"": [
""Experimentation""
]
""Apparatus"": [
""Experimentation"",
]
""Assurance"",
]
""Vow"",
]
""Offense"",
]
""March"",
]
""Titan""
]
""Hive"": [
""Experimentation"",
]
""Assurance"",
]
""Vow"",
]
""March""
]
""Big bolt"": [
""Assurance""
]
""Bottles"": [
""Assurance""
]
""Cookie mold pan"": [
""Assurance""
]
""Red soda"": [
""Assurance""
]
""Stop sign"": [
""Assurance""
]
""Egg beater"": [
""Vow""
]
""Chemical jug"": [
""Vow""
]
""Flask"": [
""Vow""
]
""Brush"": [
""Vow""
]
""Rubber Ducky"": [
""Vow""
]
""Metal sheet"": [
""Offense""
]
""Gift"": [
""Offense""
]
""Magnifying glass"": [
""Offense""
]
""Remote"": [
""Offense""
]
""Toy robot"": [
""Offense""
]
"" Whoopie cushion"": [
""March""
]
""Airhorn"": [
""March""
]
""Clown horn"": [
""March""
]
""Gold bar"": [
""March""
]
""Toy cube"": [
""March""
]
""Painting"": [
""Rend""
]
""Ring"": [
""Rend""
]
""Fancy lamp"": [
""Rend""
]
""Candy"": [
""Rend""
]
""Bell"": [
""Rend""
]
""Shotgun"": [
""Rend"",
]
""Dine"",
]
""Titan""
]
""Tragedy"": [
""Dine""
]
""Jar of pickles"": [
""Dine""
]
""Cash register"": [
""Dine""
]
""Mug"": [
""Dine""
]
""Hairdryer"": [
""Dine""
]
""Comedy"": [
""Titan""
]
""Golden cup"": [
""Titan""
]
""Old phone"": [
""Titan""
]
""Perfume bottle"": [
""Titan""
]
""Pill bottle"": [
""Titan""
]
""Large axle"": [
""Common""
]
""Laser pointer"": [
""Common""
]
""Magic 7 ball"": [
""Common""
]
""Plastic fish"": [
""Common""
]
""Tea kettle"": [
""Common""
]
""Teeth"": [
""Common""
]
""Toothpaste"": [
""Common""
]
}";
        }

        input = input.Substring(2, input.Length - 5);
        string[] slots = input.Split("],");
        Dictionary<string, string[]> result = new();
        foreach (string slot in slots)
        {
            string[] data = slot.Split("[");
            string scrapName = data[0].Trim();
            string[] scrapMoons = data[1].Split(",");
            for (int i = 0; i < scrapMoons.Length; i++)
            {
                scrapMoons[i] = scrapMoons[i].Trim();
                scrapMoons[i] = scrapMoons[i].Substring(1, scrapMoons[i].Length - 2);
            }
            
            scrapName = scrapName.Substring(1, scrapName.Length - 3);
            
            result.Add(scrapName, scrapMoons);
        }

        return result;
        
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

    public string GetCurrentMoonName()
    {
        string moon = StartOfRound.Instance.currentLevel.PlanetName;
        string[] parts = moon.Split(" ");
        parts = parts.Skip(1).Take(parts.Length - 1).ToArray();
        moon = String.Join(" ", parts);
        return moon;
    }
    
    public void CompleteTrophy(string moon, GrabbableObject scrap)
    {
        if (Array.IndexOf(_trophyModeComplete, moon) != -1) return;
        if (moon.ToLower().Contains("custom") && !scrap.scrapPersistedThroughRounds)
        {
            moon = GetCurrentMoonName();
        }
        for (var i = 0; i < moons.Length; i++)
        {
            if (_trophyModeComplete[i] is string) continue;
            _trophyModeComplete[i] = moon;
            _session.DataStorage[$"Lethal Company-{_session.Players.GetPlayerName(_session.ConnectionInfo.Slot)}-trophies"] = new JArray(_trophyModeComplete);
            break;
        }

        string[] moonNames = new string[moons.Length];
        for (int i = 0; i < moons.Length; i++)
        {
            moonNames[i] = String.Join(" ", moons[i].PlanetName.Split(" ").Skip(1)
                .Take(moons[i].PlanetName.Split(" ").Length - 1).ToArray());
        }

        if (moonNames.Any(m => Array.IndexOf(_trophyModeComplete, m) == -1)) return;
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

    public string GetCreditTracker()
    {
        return $"{GetItemMap("Company Credit").GetTotal()}/{GetSlotSetting("companycreditsgoal")}";
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
        if (!_session.Socket.Connected)
        {
            ChatHandler.SendMessage("AP: Lost connection to Archipelago server. Please reconnect before getting any new checks.");
            Disconnect();
        }
        
        string planetName = StartOfRound.Instance.currentLevel.PlanetName;
        planetName = String.Join(" ", planetName.Split(" ").Skip(1)
            .Take(planetName.Split(" ").Length - 1).ToArray());
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

        if (GetGoal() == 2)
        {
            if(GetItemMap("Company Credit").GetTotal() >= GetSlotSetting("companycreditsgoal"))
            {
                Victory();
            }
        }
        
        foreach (var item in _itemMap.Values)
        {
            item.Tick();
        }

        //RefreshItems();
    }

    public void RefreshItems()
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
                catch (Exception e)
                { 
                    //Ignore exception
                    Plugin._instance.LogError($"{e.Message}\n{e.StackTrace}");
                }
            }
        }
    }
}