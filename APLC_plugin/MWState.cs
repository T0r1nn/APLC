using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using GameNetcodeStuff;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace APLC;

/** 
 * Singleton class that manages the state of Archipelago items and locations within Lethal Company.
 * It handles item creation, location creation, item processing, and interaction with the multiworld through the MultiworldHandler.
 */
public class MwState
{
    public static MwState Instance;
    private Dictionary<String, Items> _itemMap = new();
    private Dictionary<String, Locations> _locationMap = new();
    private ConnectionInfo _connectionInfo;
    private MultiworldHandler _apConnection;
    private SelectableLevel[] _moons;
    private readonly Dictionary<string, Collection<Tuple<string, double>>> _bestiaryData;
    private readonly Dictionary<string, Collection<Tuple<string, double>>> _scrapData;
    private readonly Item[] _store;
    private readonly BuyableVehicle[] _vehicles;
    private int _goal;
    private object[] _trophyModeComplete;
    private int _scrapGoal;
    private int _scrapCollected;
    private bool _sentToMoon = true;
    public static bool WaitingForDeath;
    public static string DLMessage;
    public bool IgnoreDL;
    
    public MwState(ConnectionInfo connectionInfo)
    {
        _apConnection = new MultiworldHandler(connectionInfo);
        if (MultiworldHandler.Instance == null) return;
        _apConnection.ProcessItems += ProcessItems;
        _apConnection.RefreshItems += RefreshItems;
        _apConnection.ResetItems += ResetItems;
        _apConnection.TickItems += TickItems;
        _apConnection.GetDLService().OnDeathLinkReceived += KillRandom;
        _connectionInfo = connectionInfo;
        
        Instance = this;
        
        _goal = _apConnection.GetSlotSetting("goal");

        var logic = Plugin.Instance.GetGameLogic();
        
        _store = logic.Item1;
        _vehicles = logic.Item2;
        _moons = logic.Item3;
        _bestiaryData = logic.Item4;
        _scrapData = logic.Item5;
        
        _trophyModeComplete = new object[_moons.Length];
        
        CreateLocations();
        CreateItems();

        // foreach (var item in _apConnection.GetSession().Items.AllItemsReceived)
        // {
        //     _receivedItemNames.Add(_apConnection.GetSession().Items.GetItemName(item.Item));
        // }

        _scrapGoal = _apConnection.GetSlotSetting("collectathonGoal", 5);
        _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-scrapCollected"].Initialize(_scrapCollected);
        _scrapCollected = _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-scrapCollected"];
        _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-trophies"].Initialize(new JArray(_trophyModeComplete));
        _trophyModeComplete = _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-trophies"];

        _apConnection.Process(new AplcEventArgs(_apConnection.GetReceivedItems()));
        TerminalCommands.SetLogic();

        ES3.Save("ArchipelagoURL", _connectionInfo.URL, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save("ArchipelagoPort", _connectionInfo.Port, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save("ArchipelagoSlot", _connectionInfo.Slot, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save("ArchipelagoPassword", _connectionInfo.Password, GameNetworkManager.Instance.currentSaveFileName);

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            Config.SendChatMessagesAsAPChat = Plugin.BoundConfig.SendChatMessagesAsAPChat.Value;
            Config.ShowAPMessagesInChat = Plugin.BoundConfig.ShowAPMessagesInChat.Value;
            Config.MaxCharactersPerChatMessage = Plugin.BoundConfig.MaxCharactersPerChatMessage.Value;
            Config.FillerTriggersInstantly = Plugin.BoundConfig.FillerTriggersInstantly.Value;
            SaveManager.Startup();
        }
        else
        {
            APLCNetworking.Instance.SyncConfigServerRpc();
        }

    }


    private void CreateLocations()
    {
        try
        {
            int lowGrade;
            int medGrade;
            int highGrade;
            Terminal t = Plugin.Instance.GetTerminal();
            if (_apConnection.GetSlotSetting("splitgrades") == 1)
            {
                lowGrade = _apConnection.GetSlotSetting("lowMoon", 2);
                medGrade = _apConnection.GetSlotSetting("medMoon", 2);
                highGrade = _apConnection.GetSlotSetting("highMoon", 2);
            }
            else
            {
                lowGrade = medGrade = highGrade = _apConnection.GetSlotSetting("moonRank", 2);
            }

            //Moons
            foreach (var moon in _moons)
            {
                if (moon.PlanetName.Contains("Gordion") || moon.PlanetName.Contains("Liquidation")) continue;
                string moonName = moon.PlanetName;
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
                    if (String.Join("", moonName.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries))
                .Contains(t.terminalNodes.allKeywords[keywordIndex].compatibleNouns[j].noun.word.ToLower(), StringComparison.OrdinalIgnoreCase))
                    {
                        terminalIndex = j;
                    }
                }

                double cost = t.terminalNodes.allKeywords[keywordIndex].compatibleNouns[terminalIndex].result.itemCost;

                if (cost < 100 && moon.factorySizeMultiplier <= 1.15)
                {
                    _locationMap.Add(moonName,
                        new MoonLocations(moonName, lowGrade, _apConnection.GetSlotSetting("checksPerMoon", 3)));
                    Plugin.Instance.LogInfo($"Easy: {moonName}");
                }
                else if (cost < 120)
                {
                    _locationMap.Add(moonName,
                        new MoonLocations(moonName, medGrade, _apConnection.GetSlotSetting("checksPerMoon", 3)));
                    Plugin.Instance.LogInfo($"Medium: {moonName}");
                }
                else
                {
                    _locationMap.Add(moonName,
                        new MoonLocations(moonName, highGrade, _apConnection.GetSlotSetting("checksPerMoon", 3)));
                    Plugin.Instance.LogInfo($"Hard: {moonName}");
                }
            }

            //Quota
            _locationMap.Add("Quota",
                new Quota(_apConnection.GetSlotSetting("moneyPerQuotaCheck", 500), _apConnection.GetSlotSetting("numQuota", 20)));

            //Bestiary
            foreach (var key in _bestiaryData.Keys)
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
            _locationMap.Add("Mummy", new LogLocations(1, "Mummy"));
            _locationMap.Add("Swing of Things", new LogLocations(2, "Swing of Things"));
            _locationMap.Add("Autopilot", new LogLocations(3, "Autopilot"));
            _locationMap.Add("Sound Behind the Wall", new LogLocations(4, "Sound Behind the Wall"));
            _locationMap.Add("Goodbye", new LogLocations(5, "Goodbye"));
            _locationMap.Add("Screams", new LogLocations(6, "Screams"));
            _locationMap.Add("Golden Planet", new LogLocations(7, "Golden Planet"));
            _locationMap.Add("Idea", new LogLocations(8, "Idea"));
            _locationMap.Add("Nonsense", new LogLocations(9, "Nonsense"));
            _locationMap.Add("Hiding", new LogLocations(10, "Hiding"));
            _locationMap.Add("Real Job", new LogLocations(11, "Real Job"));
            _locationMap.Add("Desmond", new LogLocations(12, "Desmond"));
            _locationMap.Add("Team Synergy", new LogLocations(13, "Team Synergy"));
            _locationMap.Add("Letter of Resignation", new LogLocations(14, "Letter of Resignation"));

            //Scrap
            if (_apConnection.GetSlotSetting("fixscrapsanity") == 1)
            {

                Dictionary<string, string[]> scrapToMoonMap = _apConnection.GetScrapToMoonMap();

                Dictionary<string, SpawnableItemWithRarity> scrapNameToScrapMap =
                    new Dictionary<string, SpawnableItemWithRarity>();

                foreach (var moon in _moons)
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

                foreach (var moon in _moons)
                {
                    try
                    {
                        List<SpawnableItemWithRarity> scrap = moon.spawnableScrap;
                        scrap.Clear();
                        foreach (string scrapName in scrapToMoonMap.Keys)
                        {
                            if (scrapToMoonMap[scrapName].Any(moonName => moon.PlanetName.Contains(moonName)))
                            {
                                    string keyName = scrapName;
                                    if (scrapName.Contains("AP Apparatus"))
                                    {
                                        keyName = $"ap_apparatus_{moon.PlanetName.Split(' ')[1].ToLower()}";
                                    }

                                    //AP Apparatus - Artifice doesn't work
                                    Plugin.Instance.LogDebug(keyName);
                                if (scrapNameToScrapMap.TryGetValue(keyName, out SpawnableItemWithRarity item))
                                {
                                    scrap.Add(item);
                                }
                                else
                                {
                                    Plugin.Instance.LogWarning($"The given key '{keyName}' was not present in scrapNameToScrapMap when modifying scrap spawns for {moon.PlanetName}. Unless this is an AP Apparatus, it will not be added to the indoor scrap pool.");
                                    if (scrapName.Contains("AP Apparatus"))
                                    {
                                        item = scrapNameToScrapMap["ap_apparatus_custom"];
                                        scrap.Add(item);
                                    }
                                }
                            }
                            else if (scrapToMoonMap[scrapName].Any(moonName => "Common".Contains(moonName)))
                            {
                                SpawnableItemWithRarity item = scrapNameToScrapMap[scrapName];
                                scrap.Add(item);
                            }
                        }
                        // Adjust scrap amounts on Dine back to v72 values
                        if (moon.PlanetName.Contains("Dine"))
                        {
                            if (moon.minScrap == 200)
                            {
                                moon.minScrap = 22;
                            }
                            if (moon.maxScrap == 250 && moon.minScrap <= 26)
                            {
                                moon.maxScrap = 26;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Plugin.Instance.LogError($"Error modifying scrap spawns for moon '{moon.PlanetName}'. This is not likely to cause issues but we are logging it just in case.");
                    }
                }
            }

            string[] scrapNames = new string[_scrapData.Keys.Count];
            int ind = 0;
            foreach (var key in _scrapData.Keys)
            {
                scrapNames[ind] = key;
                ind++;
            }

            if (_apConnection.GetSlotSetting("scrapsanity") == 1)
            {
                _locationMap.Add("Scrap", new ScrapLocations(scrapNames));
            }
        }
        catch (Exception e)
        {
            Plugin.Instance.LogError($"{e.Message}\n{e.StackTrace}");
            _apConnection.Disconnect();
        }
    }

    /** 
     * Creates all AP items and adds them to the item map
     */
    private void CreateItems()
    {
        try
        {
            //Shop items
            for (int i = 0; i < _store.Length; i++)
            {
                _itemMap.Add(_store[i].itemName, new StoreItems(_store[i].itemName, i, false, _store[i]));
            }
            
            
            for (int i = 0; i < _vehicles.Length; i++)
            {
                _itemMap.Add(_vehicles[i].vehicleDisplayName, new StoreItems(_vehicles[i].vehicleDisplayName, i, true));
            }

            //Ship upgrades
            _itemMap.Add("Loud horn", new ShipUpgrades("Loud horn", 26));
            _itemMap.Add("Signal translator", new ShipUpgrades("Signal translator", 34));
            _itemMap.Add("Teleporter", new ShipUpgrades("Teleporter", 16));
            _itemMap.Add("Inverse Teleporter", new ShipUpgrades("Inverse Teleporter", 28));

            //Moons
            foreach (var moon in _moons)
            {
                string moonName = moon.PlanetName;
                if (moonName.Contains("Gordion") || moonName.Contains("Liquidation")) continue;
                _itemMap.Add(moonName, new MoonItems(moonName));
            }
            if (_apConnection.GetSlotSetting("randomizecompany") == 1)
            {
                _itemMap.Add("71 Gordion", new MoonItems("71 Gordion"));
            }

            //Player Upgrades
            _itemMap.Add("Inventory Slot", new PlayerUpgrades("Inventory Slot", _apConnection.GetSlotSetting("inventorySlots", 4)));
            _itemMap.Add("Stamina Bar", new PlayerUpgrades("Stamina Bar", _apConnection.GetSlotSetting("staminaBars", 4)));
            _itemMap.Add("Scanner", new PlayerUpgrades("Scanner", 1 - _apConnection.GetSlotSetting("scanner")));
            _itemMap.Add("Strength Training", new PlayerUpgrades("Strength Training", 0));
            if (_apConnection.GetSlotSetting("randomizeterminal") == 1)
            {
                _itemMap.Add("Terminal", new PlayerUpgrades("Terminal", 0));
            }
            _itemMap.Add("Company Credit", new PlayerUpgrades("Company Credit", 0));
            
            //Filler
            _itemMap.Add("Money", new FillerItems("Money", () =>
            {
                Plugin.Instance.GetTerminal().groupCredits += Random.RandomRangeInt(_apConnection.GetSlotSetting("minMoney", 100), _apConnection.GetSlotSetting("maxMoney", 100) + 1);
                return true;
            }, false));
            _itemMap.Add("HauntTrap", new FillerItems("HauntTrap", () => EnemyTrapHandler.SpawnEnemyByName(EnemyType.GhostGirl), true));
            _itemMap.Add("BrackenTrap",
                new FillerItems("BrackenTrap", () => EnemyTrapHandler.SpawnEnemyByName(EnemyType.Bracken), true));
            _itemMap.Add("More Time", new FillerItems("More Time", () =>
            {
                TimeOfDay.Instance.timeUntilDeadline += TimeOfDay.Instance.totalTime;
                TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
                APLCNetworking.Instance.SetTimeUntilDeadlineServerRpc(TimeOfDay.Instance.timeUntilDeadline);
                return true;
            }, false));
            _itemMap.Add("Less Time", new FillerItems("Less Time", () =>
            {
                TimeOfDay.Instance.timeUntilDeadline -= TimeOfDay.Instance.totalTime;
                if (TimeOfDay.Instance.timeUntilDeadline < TimeOfDay.Instance.totalTime)
                {
                    TimeOfDay.Instance.timeUntilDeadline += TimeOfDay.Instance.totalTime;
                }

                TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
                APLCNetworking.Instance.SetTimeUntilDeadlineServerRpc(TimeOfDay.Instance.timeUntilDeadline);
                return true;
            }, true));
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
            }, false));
            _itemMap.Add("Birthday Gift", new FillerItems("Birthday Gift", () =>
            {
                Item[] items = Plugin.Instance.GetTerminal().buyableItemsList;
                int i = Random.RandomRangeInt(0, items.Length);
                Plugin.Instance.GetTerminal().orderedItemsFromTerminal.Add(i);
                return true;
            }, false));
        }
        catch (Exception e)
        {
            Plugin.Instance.LogError($"{e.Message}\n{e.StackTrace}");
            _apConnection.Disconnect();
        }
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

    public T GetLocationMap<T>(string key) where T : Locations
    {
        return (T)_locationMap[key];
    }

    public bool CheckTrophy(string moon)
    {
        // Plugin._instance.LogWarning(_session.Locations.GetLocationIdFromName("Lethal Company", $"Scrap - AP Apparatus - {moon}").ToString());
        // Plugin._instance.LogWarning(_session.Locations.AllLocationsChecked.Count.ToString());
        // return _session.Locations.AllLocationsChecked.Contains(_session.Locations.GetLocationIdFromName("Lethal Company", $"Scrap - AP Apparatus - {moon}"));
        return Array.IndexOf(_trophyModeComplete, moon.ToLower()) != -1;
    }

    /** 
     * Marks the trophy for the given moon as complete by adding it's name to an array of collected trophies. If all trophies are complete, triggers victory.
     * It needs the scrap object to check if it came from a previous round, in which case it won't count towards trophy completion. This is important for custom moons because
     * they all share the same "AP Apparatus - Custom" scrap object.
     */
    public void CompleteTrophy(string moon, GrabbableObject scrap)
    {
        if (moon.ToLower().Contains("custom"))
        {
            if (scrap.scrapPersistedThroughRounds) return;
            moon = GetCurrentMoonName().ToLower();
        }

        foreach (var level in StartOfRound.Instance.levels)
        {
            if (level.PlanetName.ToLower().Contains(moon.ToLower()))
            {
                moon = level.PlanetName.ToLower();
            }
        }

        if (Array.IndexOf(_trophyModeComplete, moon) != -1) return;

        for (var i = 0; i < _moons.Length; i++)
        {
            if (_trophyModeComplete[i] is string) continue;
            _trophyModeComplete[i] = moon;
            _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-trophies"] = new JArray(_trophyModeComplete);
            break;
        }

        string[] moonNames = new string[_moons.Length];
        for (int i = 0; i < _moons.Length; i++)
        {
            moonNames[i] = _moons[i].PlanetName;
        }

        if (_trophyModeComplete[^1] is string)
        {
            string trophyList = "Game should be complete. The following moons had their trophies collected:\n";
            foreach (string trophy in _trophyModeComplete)
            {
                trophyList += $"{trophy}\n";
            }
            Plugin.Instance.LogInfo(trophyList);
        }

        if (moonNames.Any(m => Array.IndexOf(_trophyModeComplete, m.ToLower()) == -1)) return;
        _apConnection.Victory();
    }
    
    public string GetCurrentMoonName()
    {
        return StartOfRound.Instance.currentLevel.PlanetName;
    }

    /** 
     * Adds the given amount of scrap to the collectathon total. If the total meets or exceeds the goal, triggers victory.
     * Storing the total and checking the victory condition only happens on the host, but the total is synced to clients via a ClientRpc.
     * This is because clients can't see the scrapPersistedThroughRounds property of scrap, so they would count the same chest multiple times.
     */
    public void AddCollectathonScrap(int amount)
    {
        _scrapCollected += amount;
        _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-scrapCollected"] = _scrapCollected;
        if (_scrapCollected >= _scrapGoal)
        {
            _apConnection.Victory();
        }
        APLCNetworking.Instance.AddCollectathonScrapClientRpc(amount);  // we do this to sync _scrapCollected with the host
    }
    
    public void IncrementScrapCollected(int amount)
    {
        _scrapCollected += amount;
    }

    public string GetCollectathonTracker()
    {
        return $"{_scrapCollected}/{_scrapGoal}";
    }

    public string GetCreditTracker()
    {
        return $"{GetItemMap("Company Credit").GetTotal()}/{_apConnection.GetSlotSetting("companycreditsgoal")}";
    }

    public int GetGoal()
    {
        return _goal;
    }
    
    private void ResetItems(object source, AplcEventArgs args)
    {
        foreach (var item in _itemMap.Values)
        {
            item.Reset();
        }
    }

    /**
     * Called every tick (usually every few seconds) to update item states, check for connection issues, handle death link, and redirect the ship away from locked moons.
     */
    public void TickItems(object source, AplcEventArgs args)
    {
        if (!_apConnection.GetSession().Socket.Connected)
        {
            ChatHandler.SendMessage("AP: Lost connection to Archipelago server. Please reconnect before getting any new checks.");
            _apConnection.Disconnect();
        }
        
        string planetName = StartOfRound.Instance.currentLevel.PlanetName;

        if (planetName != "71 Gordion" || _apConnection.GetSlotSetting("randomizecompany") == 1)
        {
            if (GetItemMap<MoonItems>(planetName).GetTotal() < 1 || (GetStartingMoon() != planetName && _apConnection.GetSlotSetting("randomizeterminal")==1 && GetItemMap<PlayerUpgrades>("Terminal").GetNum() < 1))
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

        if (WaitingForDeath && (GameNetworkManager.Instance.localPlayerController.IsHost || GameNetworkManager.Instance.localPlayerController.IsServer))
        {

            int selected = Random.Range(0, StartOfRound.Instance.livingPlayers);   // player controllers without a connected player are considered dead, so this works
            PlayerControllerB[] players = [.. StartOfRound.Instance.allPlayerScripts.Where(player => !player.isPlayerDead)];
            var steamIds = new ulong[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                steamIds[i] = players[i].playerSteamId;
            }
            Plugin.Instance.LogInfo($"Attempting to kill player \"{players[selected].playerUsername}\"");

            if (GameNetworkManager.Instance.localPlayerController == players[selected])
                GameNetworkManager.Instance.localPlayerController.KillPlayer(Vector3.forward, true, CauseOfDeath.Blast);
            else
            {
                if (GameNetworkManager.Instance.disableSteam)   // all players have steamID 0 in LAN mode, so we have to use the index instead
                    APLCNetworking.Instance.KillPlayerClientRpc((ulong)selected);
                else
                    APLCNetworking.Instance.KillPlayerClientRpc(steamIds[selected]);
            }
            ChatHandler.SendMessage($"AP: {DLMessage}");
            WaitingForDeath = false;
            DLMessage = "";
            if (StartOfRound.Instance.allPlayersDead)
            {
                IgnoreDL = true;
            }
        }

        if (GetGoal() == 2)
        {
            if(GetItemMap("Company Credit").GetTotal() >= _apConnection.GetSlotSetting("companycreditsgoal"))
            {
                _apConnection.Victory();
            }
        }
        
        foreach (var item in _itemMap.Values)
        {
            item.Tick();
        }

        //RefreshItems();
    }

    public void RefreshItems(object source, AplcEventArgs args)
    {
        args.GetReceivedItemNames().Clear();

        foreach (var item in _apConnection.GetSession().Items.AllItemsReceived)
        {
            args.GetReceivedItemNames().Add(_apConnection.GetSession().Items.GetItemName(item.ItemId));
        }

        _apConnection.Process(args);
    }

    private void ProcessItems(object source, AplcEventArgs args)
    {
        ResetItems(source, args);
        int flashlights = 0;
        string[] flashlightNames = { "Flashlight", "Pro-flashlight" };
        foreach (var name in args.GetReceivedItemNames())
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
                    Plugin.Instance.LogWarning("Error processing Progressive Flashlight. This is not likely to cause issues but we are logging it just in case.");
                }
            }
            else if (name == "Company Building")
            {
                try
                {
                    _itemMap["71 Gordion"].OnReceived();
                }
                catch (Exception)
                {
                    Plugin.Instance.LogWarning("Error processing Company Building. This is not likely to cause issues but we are logging it just in case.");
                }
            }
            else if (name == "LoudHorn")
            {
                try
                {
                    _itemMap["Loud horn"].OnReceived();
                }
                catch (Exception)
                {
                    Plugin.Instance.LogWarning("Error processing Loud Horn. This is not likely to cause issues but we are logging it just in case.");
                }
            }
            else if (name == "SignalTranslator")
            {
                try
                {
                    _itemMap["Signal translator"].OnReceived();
                }
                catch (Exception)
                {
                    Plugin.Instance.LogWarning("Error processing Signal translator. This is not likely to cause issues but we are logging it just in case.");
                }
            }
            else if (name == "InverseTeleporter")
            {
                try
                {
                    _itemMap["Inverse Teleporter"].OnReceived();
                }
                catch (Exception)
                {
                    Plugin.Instance.LogWarning("Error processing Inverse Teleporter. This is not likely to cause issues but we are logging it just in case.");
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
                    Plugin.Instance.LogWarning($"Error processing {name}. This is not likely to cause issues but we are logging it just in case.");
                }
            }
        }
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

        StartOfRound.Instance.ChangeLevelServerRpc(moonInd, Plugin.Instance.GetTerminal().groupCredits);
        return true;
    }
    
    public string GetStartingMoon()
    {
        foreach (var itemName in _apConnection.GetReceivedItems())
        {
            Items item = GetItemMap(itemName);
            Plugin.Instance.LogIfDebugBuild(itemName);   
            Plugin.Instance.LogIfDebugBuild(item.GetType().FullName);
            if (item.GetType() == typeof(MoonItems))
            {
                return itemName;
            }
        }

        return null;
    }

    /** 
     * Handles incoming DeathLink events by randomly selecting a player to kill based on the timestamp of the death event.
     * If the local player is selected, sets a flag to trigger the death on the next tick.
     */
    private static void KillRandom(DeathLink link)
    {
        if (!(GameNetworkManager.Instance.localPlayerController.IsHost || GameNetworkManager.Instance.localPlayerController.IsServer)) return;
        Plugin.Instance.LogInfo("Received death link");

        Random.InitState(link.Timestamp.Millisecond);

        WaitingForDeath = true;
        DLMessage = link.Cause;
    }

    public Dictionary<string,Collection<Tuple<string,double>>> GetScrapData()
    {
        return _scrapData;
    }
}