using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Dawn;
using GameNetcodeStuff;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using Unity.Profiling;
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
    private readonly Dictionary<string, Collection<ValueTuple<string, double>>> _bestiaryData;
    private readonly Dictionary<string, Collection<ValueTuple<string, double>>> _scrapData;
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
    public static Dictionary<string, int> NormalMoonPrices {  get; private set; }
    
    private static readonly ProfilerMarker s_CreateLocations = new("APLC.MwState.CreateLocations");
    private static readonly ProfilerMarker s_CreateItems = new("APLC.MwState.CreateItems");
    private static readonly ProfilerMarker s_ProcessItems = new("APLC.MwState.ProcessItems");

    public MwState(ConnectionInfo connectionInfo)
    {
        _apConnection = new MultiworldHandler(connectionInfo);
        if (MultiworldHandler.Instance == null) return;

        var logic = Plugin.Instance.GetGameLogic();

        _store = logic.Item1;
        _vehicles = logic.Item2;
        _moons = logic.Item3;
        _bestiaryData = logic.Item4;
        _scrapData = logic.Item5;

        _trophyModeComplete = new object[_moons.Length];

        NormalMoonPrices = new Dictionary<string, int>();
        if (ES3.KeyExists("APNormalMoonPrices", GameNetworkManager.Instance.currentSaveFileName))
        {
            NormalMoonPrices = ES3.Load<Dictionary<string, int>>("APNormalMoonPrices", GameNetworkManager.Instance.currentSaveFileName);
        }
        foreach (SelectableLevel level in StartOfRound.Instance.levels)
        {
            if (!NormalMoonPrices.TryGetValue(level.PlanetName, out _))
                NormalMoonPrices[level.PlanetName] = level.GetDawnInfo().DawnPurchaseInfo.Cost.Provide();
        }
        ES3.Save("APNormalMoonPrices", NormalMoonPrices, GameNetworkManager.Instance.currentSaveFileName);

        _ = PrepareMwState(connectionInfo);

    }

    private async Task PrepareMwState(ConnectionInfo connectionInfo)
    {
        Task createLocations = CreateLocations();  // this and CreateItems() need to run before any of the handlers are set up, otherwise we can have a scenario where an item comes in before the item map knows what it is
        Task createItems = CreateItems();

        await Task.WhenAll([createLocations, createItems]);
        if (MultiworldHandler.Instance == null) return;

        _apConnection.ProcessItems += ProcessItems;
        _apConnection.RefreshItems += RefreshItems;
        _apConnection.ResetItems += ResetItems;
        _apConnection.TickItems += TickItems;
        _apConnection.GetDLService().OnDeathLinkReceived += KillRandom;
        _connectionInfo = connectionInfo;
        
        Instance = this;
        
        _goal = _apConnection.GetSlotSettingInt("goal");

        // foreach (var item in _apConnection.GetSession().Items.AllItemsReceived)
        // {
        //     _receivedItemNames.Add(_apConnection.GetSession().Items.GetItemName(item.Item));
        // }

        _scrapGoal = _apConnection.GetSlotSettingInt("collectathonGoal", 5);
        _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-scrapCollected"].Initialize(_scrapCollected);
        _scrapCollected = await _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-scrapCollected"].GetAsync<int>();
        _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-trophies"].Initialize(new JArray(_trophyModeComplete));
        _trophyModeComplete = await _apConnection.GetSession().DataStorage[$"Lethal Company-{_apConnection.GetSession().Players.GetPlayerName(_apConnection.GetSession().ConnectionInfo.Slot)}-trophies"].GetAsync <object[]>();

        _apConnection.Process(new AplcEventArgs(_apConnection.GetReceivedItems()));
        TerminalCommands.SetLogic();

        ES3.Save("ArchipelagoURL", _connectionInfo.URL, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save("ArchipelagoPort", _connectionInfo.Port, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save("ArchipelagoSlot", _connectionInfo.Slot, GameNetworkManager.Instance.currentSaveFileName);
        ES3.Save("ArchipelagoPassword", _connectionInfo.Password, GameNetworkManager.Instance.currentSaveFileName);
        
        if (GetStartingMoon() != null) ES3.Save("APStartingMoon", GetStartingMoon(), GameNetworkManager.Instance.currentSaveFileName);
        StartOfRound.Instance.defaultPlanet = StartOfRound.Instance.levels.FirstOrDefault(l => l.PlanetName.ToLower().Contains(GetStartingMoon().ToLower()))?.levelID ?? 0;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            Config.SendChatMessagesAsAPChat = Plugin.BoundConfig.SendChatMessagesAsAPChat.Value;
            Config.ShowAPMessagesInChat = Plugin.BoundConfig.ShowAPMessagesInChat.Value;
            Config.MaxCharactersPerChatMessage = Plugin.BoundConfig.MaxCharactersPerChatMessage.Value;
            Config.FillerTriggersInstantly = Plugin.BoundConfig.FillerTriggersInstantly.Value;
            Config.DeathLink = Plugin.BoundConfig.DeathLink.Value;
            SaveManager.Startup();
        }
        else
        {
            APLCNetworking.Instance.SyncConfigServerRpc();
        }
        Plugin.Logger.LogInfo("MwState setup done!");
    }

    private async Task CreateLocations()
    {
#if ENABLE_PROFILER
        using var automarker = s_CreateLocations.Auto();
#endif
        try
        {
            int easyGrade;
            int mediumGrade;
            int hardGrade;
            Terminal t = Plugin.Instance.GetTerminal();
            if (_apConnection.GetSlotSettingInt("splitgrades") == 1)
            {
                easyGrade = _apConnection.GetSlotSettingInt("easyMoonRequiredGrade", 2);
                mediumGrade = _apConnection.GetSlotSettingInt("mediumMoonRequiredGrade", 2);
                hardGrade = _apConnection.GetSlotSettingInt("hardMoonRequiredGrade", 2);
            }
            else
            {
                easyGrade = mediumGrade = hardGrade = _apConnection.GetSlotSettingInt("allMoonRequiredGrade", 2);
            }

            string[] vanillaMoonNames = ["experimentation", "assurance", "vow", "adamance", "offense", "march", "rend", "dine", "titan", "artifice", "embrion"];
            Dictionary<string, int> moonDifficulties = [];
            List<int> vanillaMoonDifficulties = [];
            List<Task> locationsToCreate = new();

            //Moons
            foreach (var moon in _moons)
            {
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

                // data to use: difficulty = (routePrice + maxTotalScrapValue + sum((item.maxValue - item.minValue)*10/item.rarity) + 30(maxEnemyPowerCount + maxOutsideEnemyPowerCount + maxDaytimeEnemyPowerCount)
                // + sum(enemy.enemyType.powerLevel * 1000 / enemy.rarity)) * factorySizeMultiplier * 0.5

                //int difficulty = CalculateMoonDifficultyRating(moon);

                if (Plugin.BoundConfig.DifficultyCalculation.Value == PluginConfig.DifficultyCalculationMethod.CostBased)
                {
                    double cost = NormalMoonPrices[moon.PlanetName];

                    if (cost < 100 && moon.factorySizeMultiplier <= 1.15)
                    {
                        locationsToCreate.Add(LocationCreator.CreateMoonLocationAsync(moonName, easyGrade, _apConnection.GetSlotSettingInt("gradeLocationsPerMoon", 3)));
                        Plugin.Logger.LogInfo($"Easy: {moonName}");
                    }
                    else if (cost < 400)
                    {
                        locationsToCreate.Add(LocationCreator.CreateMoonLocationAsync(moonName, mediumGrade, _apConnection.GetSlotSettingInt("gradeLocationsPerMoon", 3)));
                        Plugin.Logger.LogInfo($"Medium: {moonName}");
                    }
                    else
                    {
                        locationsToCreate.Add(LocationCreator.CreateMoonLocationAsync(moonName, hardGrade, _apConnection.GetSlotSettingInt("gradeLocationsPerMoon", 3)));
                        Plugin.Logger.LogInfo($"Hard: {moonName}");
                    }
                }
                else
                {
                    moonDifficulties[moonName] = CalculateMoonDifficultyRating(moon);
                    if (vanillaMoonNames.Contains(moon.GetDawnInfo().GetNumberlessPlanetName().ToLower()))
                    {
                        vanillaMoonDifficulties.Add(moonDifficulties[moonName]);
                    }
                }
            }
            if (Plugin.BoundConfig.DifficultyCalculation.Value == PluginConfig.DifficultyCalculationMethod.Complex)
            {
                vanillaMoonDifficulties.Sort();
                foreach (var kvp in moonDifficulties)
                {
                    if (kvp.Value <= vanillaMoonDifficulties[vanillaMoonDifficulties.Count / 3])            // should be vanillaMoonDifficulties[3], or Vow
                    {
                        locationsToCreate.Add(LocationCreator.CreateMoonLocationAsync(kvp.Key, easyGrade, _apConnection.GetSlotSettingInt("gradeLocationsPerMoon", 3)));
                        Plugin.Logger.LogInfo($"Easy: {kvp.Key}");
                    }
                    else if (kvp.Value < vanillaMoonDifficulties[vanillaMoonDifficulties.Count * 2 / 3])    // should be vanillaMoonDifficulties[7], or Dine
                    {
                        locationsToCreate.Add(LocationCreator.CreateMoonLocationAsync(kvp.Key, mediumGrade, _apConnection.GetSlotSettingInt("gradeLocationsPerMoon", 3)));
                        Plugin.Logger.LogInfo($"Medium: {kvp.Key}");
                    }
                    else
                    {
                        locationsToCreate.Add(LocationCreator.CreateMoonLocationAsync(kvp.Key, hardGrade, _apConnection.GetSlotSettingInt("gradeLocationsPerMoon", 3)));
                        Plugin.Logger.LogInfo($"Hard: {kvp.Key}");
                    }
                }
            }

            //Quota
            locationsToCreate.Add(LocationCreator.CreateQuotaLocationAsync(_apConnection.GetSlotSettingInt("moneyPerQuotaLocation", 500), _apConnection.GetSlotSettingInt("numQuota", 20)));

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

                _locationMap.Add(key, LocationCreator.CreateBestiaryLocation(id, key));
            }

            //Logs
            _locationMap.Add("Mummy", LocationCreator.CreateLogLocation(1, "Mummy"));
            _locationMap.Add("Swing of Things", LocationCreator.CreateLogLocation(2, "Swing of Things"));
            _locationMap.Add("Autopilot", LocationCreator.CreateLogLocation(3, "Autopilot"));
            _locationMap.Add("Behind the Wall", LocationCreator.CreateLogLocation(4, "Behind the Wall"));
            _locationMap.Add("Goodbye", LocationCreator.CreateLogLocation(5, "Goodbye"));
            _locationMap.Add("Screams", LocationCreator.CreateLogLocation(6, "Screams"));
            _locationMap.Add("Golden Planet", LocationCreator.CreateLogLocation(7, "Golden Planet"));
            _locationMap.Add("Idea", LocationCreator.CreateLogLocation(8, "Idea"));
            _locationMap.Add("Nonsense", LocationCreator.CreateLogLocation(9, "Nonsense"));
            _locationMap.Add("Hiding", LocationCreator.CreateLogLocation(10, "Hiding"));
            _locationMap.Add("Real Job", LocationCreator.CreateLogLocation(11, "Real Job"));
            _locationMap.Add("Desmond", LocationCreator.CreateLogLocation(12, "Desmond"));
            _locationMap.Add("Team Synergy", LocationCreator.CreateLogLocation(13, "Team Synergy"));
            _locationMap.Add("Letter of Resignation", LocationCreator.CreateLogLocation(14, "Letter of Resignation"));
            _locationMap.Add("Work", LocationCreator.CreateLogLocation(15, "Work"));

            //Scrap
            if (_apConnection.GetSlotSettingInt("fixscrapsanity") == 1)
            {

                Dictionary<string, string[]> scrapToMoonMap = _apConnection.GetScrapToMoonMap();

                Dictionary<string, SpawnableItemWithRarity> scrapNameToScrapMap =
                    new Dictionary<string, SpawnableItemWithRarity>();

                foreach (DawnItemInfo itemInfo in LethalContent.Items.Values)
                {
                    string itemName = null;
                    if (itemInfo.ScrapInfo != null && itemInfo.Item.isScrap)     // isDefensiveWeapon seems like a convenient solution, but some scrap like the Whack-a-Noodle have this property set to true
                    {
                        if (itemInfo.Item.itemName.Equals("Egg") || itemInfo.Item.itemName.Equals("Hive") || itemInfo.Item.itemName.Equals("Apparatus") || 
                            itemInfo.Item.itemName.Equals("Shotgun") || itemInfo.Item.itemName.Equals("Kitchen knife")) continue;
                        itemName = itemInfo.Item.name.Contains("ap_apparatus_")
                                ? itemInfo.Item.name
                                : itemInfo.Item.itemName;
                        scrapNameToScrapMap.TryAdd(itemName, new SpawnableItemWithRarity(itemInfo.Item, 30));
                    }
                }
                if (LLLCompat.IsLethalLevelLoaderInstalled)
                {
                    foreach (Item item in LethalContent.Items.Values.Where(item => item.ShopInfo == null && item.ScrapInfo == null && item.Item.isScrap).Select(itemInfo => itemInfo.Item))
                    {
                        if (item.itemName.Equals("Egg") || item.itemName.Equals("Hive") || item.itemName.Equals("Apparatus") || item.itemName.Equals("Shotgun") || item.itemName.Equals("Kitchen knife")) continue;
                        foreach (var moon in _moons)
                        {
                            double rarity = LLLCompat.GetDynamicRarityForAllDungeons(item, moon, 1);
                            if (rarity > 0)
                            {
                                scrapNameToScrapMap.TryAdd(item.itemName, new SpawnableItemWithRarity(item, 30));
                            }
                        }
                    }
                }

                Dictionary<SpawnableItemWithRarity, List<SelectableLevel>> commonScrapToMoonMap = [];

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
                                    keyName = $"ap_apparatus_{moon.GetDawnInfo().GetNumberlessPlanetName().ToLower()}";
                                }

                                //AP Apparatus - Artifice doesn't work
                                Plugin.Logger.LogDebug(keyName);
                                if (scrapNameToScrapMap.TryGetValue(keyName, out SpawnableItemWithRarity item))
                                {
                                    DawnItemInfo itemInfo = item.spawnableItem.GetDawnInfo();
                                    scrap.Add(item);
                                    if (itemInfo.ScrapInfo != null)
                                        itemInfo.ScrapInfo.Weights = new ProviderTable<int?, DawnMoonInfo, SpawnWeightContext>([new MatchingKeyWeightContextualProvider<DawnMoonInfo, SpawnWeightContext>(moon.GetDawnInfo().Key.AsTyped<DawnMoonInfo>(), new SimpleWeighted(30))]);
                                    else if (LLLCompat.IsLethalLevelLoaderInstalled && !LLLCompat.OverrideScrapRarity(item.spawnableItem, [moon]))
                                    {     
                                        Plugin.Logger.LogWarning($"Failed to override scrap rarity for {item.spawnableItem.itemName} on {moon.PlanetName}. It will not be added to the indoor scrap pool.");
                                    }
                                }
                                else
                                {
                                    if (scrapName.Contains("AP Apparatus"))
                                    {
                                        item = scrapNameToScrapMap["ap_apparatus_custom"];
                                        scrap.Add(item);
                                    }
                                    else
                                        Plugin.Logger.LogWarning($"The given key '{keyName}' was not present in scrapNameToScrapMap when modifying scrap spawns for {moon.PlanetName}. It will not be added to the indoor scrap pool.");
                                }
                            }
                            else if (scrapToMoonMap[scrapName].Any(moonName => "Common".Contains(moonName)))
                            {
                                if (!scrapNameToScrapMap.TryGetValue(scrapName, out SpawnableItemWithRarity item))
                                {
                                    Plugin.Logger.LogWarning($"The given key '{scrapName}' was not present in scrapNameToScrapMap when modifying scrap spawns for {moon.PlanetName}. It will not be added to the indoor scrap pool.");
                                    continue;
                                }
                                if (!commonScrapToMoonMap.ContainsKey(item)) commonScrapToMoonMap[item] = [];
                                commonScrapToMoonMap[item].Add(moon);
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
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError($"Error modifying scrap spawns for moon '{moon.PlanetName}'. Its scrap has not been modified.\n{ex}");
                    }
                }

                // for Dawn and LLL compat
                foreach (KeyValuePair<SpawnableItemWithRarity, List<SelectableLevel>> kvp in commonScrapToMoonMap)
                {
                    DawnItemInfo itemInfo = kvp.Key.spawnableItem.GetDawnInfo();
                    if (itemInfo.ScrapInfo != null)
                        itemInfo.ScrapInfo.Weights = new ProviderTable<int?, DawnMoonInfo, SpawnWeightContext>([new HasTagWeightContextualProvider<DawnMoonInfo, SpawnWeightContext>(Tags.All, new SimpleWeighted(30))]);
                    else if (LLLCompat.IsLethalLevelLoaderInstalled && !LLLCompat.OverrideScrapRarity(kvp.Key.spawnableItem, kvp.Value))
                    {
                        Plugin.Logger.LogWarning($"Failed to override scrap rarity for {kvp.Key.spawnableItem.itemName}. It will not be added to the common indoor scrap pool.");
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

            if (_apConnection.GetSlotSettingInt("scrapsanity") == 1)
            {
                locationsToCreate.Add(LocationCreator.CreateScrapLocationAsync(scrapNames));
            }

            await Task.WhenAll(locationsToCreate);
            foreach (Task<Locations> finishedTask in locationsToCreate.Cast<Task<Locations>>())
            {
                Locations loc = finishedTask.Result;
                if (loc is MoonLocations locations)
                    _locationMap.Add(locations.Name, loc);
                else
                    _locationMap.Add(loc.Type, loc);
            }
            Plugin.Logger.LogDebug("Finished creating locations");
        }
        catch (Exception e)     // this is kind of justified but I hope there's a better way to do it than nested try-catch blocks
        {
            Plugin.Logger.LogError($"{e.Message}\n{e.StackTrace}");
            _apConnection.Disconnect();
        }
    }

    /** 
     * Creates all AP items and adds them to the item map
     */
    private async Task CreateItems()
    {
#if ENABLE_PROFILER
        using var automarker = s_CreateItems.Auto();
#endif
        Terminal terminal = Plugin.Instance.GetTerminal();
        bool randomizeCompany = false;
        int inventorySlots = 4;
        int staminaBars = 4;
        int scanner = 1;
        bool randomizeTerminal = false;
        int minMoney = 100;
        int maxMoney = 101;
        try
        {
            randomizeCompany = _apConnection.GetSlotSettingInt("randomizecompany") == 1;
            inventorySlots = _apConnection.GetSlotSettingInt("inventorySlots", 4);
            staminaBars = _apConnection.GetSlotSettingInt("staminaBars", 4);
            scanner = 1 - _apConnection.GetSlotSettingInt("scanner");
            randomizeTerminal = _apConnection.GetSlotSettingInt("randomizeterminal") == 1;
            minMoney = _apConnection.GetSlotSettingInt("minMoney", 100);
            maxMoney = _apConnection.GetSlotSettingInt("maxMoney", 100);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"{e.Message}\n{e.StackTrace}");
            _apConnection.Disconnect();
            return;
        }

        List<Task> itemsToCreate = new();

        //Shop items
        for (int i = 0; i < _store.Length; i++)
        {
            Items item = ItemCreator.CreateStoreItem(_store[i]);
            _itemMap.Add(item._name, item);
        }


        for (int i = 0; i < _vehicles.Length; i++)
        {
            Items item = ItemCreator.CreateVehicleItem(_vehicles[i]);
            _itemMap.Add(item._name, item);
        }

        //Ship upgrades
        foreach (UnlockableItem unlockable in StartOfRound.Instance.unlockablesList.unlockables)
        {
            DawnUnlockableItemInfo unlockableInfo = unlockable.GetDawnInfo();
            if (unlockableInfo.SuitInfo != null) continue;  // don't randomize suits
            //_itemMap.Add(unlockable.unlockableName, new ShipUpgrades(unlockable));
            if (unlockable.unlockableName.Contains("Loud horn") ||
                unlockable.unlockableName.Contains("Signal translator") ||
                unlockable.unlockableName.Contains("Teleporter"))
            {
                Items item = ItemCreator.CreateUnlockableItem(unlockable);
                _itemMap.Add(item._name, item);
            }
        }

        //Moons
        foreach (var moon in _moons)
        {
            Items item = ItemCreator.CreateMoonItem(moon);
            _itemMap.Add(item._name, item);
        }
        if (randomizeCompany)
        {
            foreach (SelectableLevel moon in StartOfRound.Instance.levels)
            {
                if (moon.PlanetName.Contains("Gordion"))
                {
                    Items item = ItemCreator.CreateMoonItem(moon);
                    _itemMap.Add(item._name, item);
                }
                else if (!moon.spawnEnemiesAndScrap) DawnCompat.AssignPurchasePredicate(moon);
            }
        }

        //Player Upgrades
        _itemMap.Add("Inventory Slot", ItemCreator.CreateUpgradeItem("Inventory Slot", inventorySlots));
        _itemMap.Add("Stamina Bar", ItemCreator.CreateUpgradeItem("Stamina Bar", staminaBars));
        _itemMap.Add("Scanner", ItemCreator.CreateUpgradeItem("Scanner", scanner));
        _itemMap.Add("Strength Training", ItemCreator.CreateUpgradeItem("Strength Training", 0));
        if (randomizeTerminal)
        {
            _itemMap.Add("Terminal", ItemCreator.CreateUpgradeItem("Terminal", 0));
        }
        _itemMap.Add("Company Credit", ItemCreator.CreateUpgradeItem("Company Credit", 0));

        //Filler
        itemsToCreate.Add(ItemCreator.CreateFillerItemAsync("Money", () =>
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                terminal.groupCredits += Random.RandomRangeInt(minMoney, maxMoney);
                terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
                return true;
            }
            return false;
        }, false));
        itemsToCreate.Add(ItemCreator.CreateFillerItemAsync("HauntTrap", () => EnemyTrapHandler.SpawnEnemyByName(EnemyType.GhostGirl), true));
        itemsToCreate.Add(ItemCreator.CreateFillerItemAsync("BrackenTrap", () => EnemyTrapHandler.SpawnEnemyByName(EnemyType.Bracken), true));
        itemsToCreate.Add(ItemCreator.CreateFillerItemAsync("More Time", () =>
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                TimeOfDay.Instance.timeUntilDeadline += TimeOfDay.Instance.totalTime;
                APLCNetworking.Instance.SetTimeUntilDeadlineRpc(TimeOfDay.Instance.timeUntilDeadline);
                return true;
            }
            return false;
        }, false));
        itemsToCreate.Add(ItemCreator.CreateFillerItemAsync("Less Time", () =>
        {
            TimeOfDay.Instance.timeUntilDeadline -= TimeOfDay.Instance.totalTime;
            if (TimeOfDay.Instance.timeUntilDeadline < TimeOfDay.Instance.totalTime)
            {
                TimeOfDay.Instance.timeUntilDeadline += TimeOfDay.Instance.totalTime;
            }
            APLCNetworking.Instance.SetTimeUntilDeadlineRpc(TimeOfDay.Instance.timeUntilDeadline);
            return true;
        }, true));
        itemsToCreate.Add(ItemCreator.CreateFillerItemAsync("Clone Scrap", () =>
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
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
            }
            return false;
        }, false));
        itemsToCreate.Add(ItemCreator.CreateFillerItemAsync("Birthday Gift", () =>
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost) { 
                Item[] items = Plugin.Instance.GetTerminal().buyableItemsList;
                int i = Random.RandomRangeInt(0, items.Length);
                Plugin.Instance.GetTerminal().orderedItemsFromTerminal.Add(i);
                terminal.SyncGroupCreditsClientRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
                return true;
            }
            return false;
        }, false));

        await Task.WhenAll(itemsToCreate);
        foreach (Task<Items> finishedTask in itemsToCreate.Cast<Task<Items>>())
        {
            _itemMap.Add(finishedTask.Result._name, finishedTask.Result);
        }
        Plugin.Logger.LogDebug("Finished creating items");

    }
    
    public void CheckLogs()
    {
        foreach (var location in _locationMap.Values)
        {
            switch (location.Type)
            {
                case "logB":
                    ((BestiaryLocations)location).LocationComplete();
                    break;
                case "logF":
                    ((LogLocations)location).LocationComplete();
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

    public bool TryGetItemMap<T>(string key, out T itemMap) where T : Items
    {
        if (_itemMap.TryGetValue(key, out Items map))
        {
            itemMap = (T)map;
            return true;
        }
        itemMap = default;
        return false;
    }

    public Locations GetLocationMap(string key)
    {
        return _locationMap[key];
    }

    public T GetLocationMap<T>(string key) where T : Locations
    {
        return (T)_locationMap[key];
    }

    public bool TryGetLocationMap<T>(string key, out T locationMap) where T : Locations
    {
        if (_locationMap.TryGetValue(key, out Locations map))
        {
            locationMap = (T)map;
            return true;
        }
        locationMap = default;
        return false;
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
            APLCNetworking.Instance.DisplayTipToAllRPC("Archipelago", $"Trophy collected for '{moon}'. Progress: {i+1}/{_trophyModeComplete.Length}");
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
            Plugin.Logger.LogInfo(trophyList);
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
        HUDManager.Instance.DisplayTip("Archipelago", $"Collected {amount} apchests. Progress: {_scrapCollected}/{_scrapGoal}");
        if (_scrapCollected >= _scrapGoal)
        {
            _apConnection.Victory();
        }
        APLCNetworking.Instance.AddCollectathonScrapClientRpc(amount);  // we do this to sync _scrapCollected with the host
    }
    
    public void IncrementScrapCollected(int amount)
    {
        _scrapCollected += amount;
        HUDManager.Instance.DisplayTip("Archipelago", $"Collected {amount} apchests. Progress: {_scrapCollected}/{_scrapGoal}");
    }

    public string GetCollectathonTracker()
    {
        return $"{_scrapCollected}/{_scrapGoal}";
    }

    public string GetCreditTracker()
    {
        return $"{GetItemMap("Company Credit").GetTotal()}/{_apConnection.GetSlotSettingInt("companycreditsgoal")}";
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
            ChatHandler.SendMessage("AP: Lost connection to Archipelago server. Please reconnect before getting any new locations.");
            _apConnection.Disconnect();
        }
        
        string planetName = StartOfRound.Instance.currentLevel.PlanetName;

        if (StartOfRound.Instance.currentLevel.spawnEnemiesAndScrap) 
        {
            if (GetItemMap<MoonItems>(planetName).GetTotal() < 1 || (GetStartingMoon() != planetName && _apConnection.GetSlotSettingInt("randomizeterminal") == 1 && GetItemMap<PlayerUpgrades>("Terminal").GetNum() < 1))
            {
                _sentToMoon = false;
            }
        }
        else 
        {
            if (_apConnection.GetSlotSettingInt("randomizecompany") == 1 && GetItemMap<MoonItems>("71 Gordion").GetTotal() < 1)
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
            
            PlayerControllerB[] players = [.. StartOfRound.Instance.allPlayerScripts.Where(player => player.isPlayerControlled && !player.isPlayerDead)];
            if (players.Length > 0)
            {
                int selected = Random.Range(0, players.Length);
                Plugin.Logger.LogInfo($"Attempting to kill player \"{players[selected].playerUsername}\" with id {players[selected].playerClientId}");
                if (GameNetworkManager.Instance.localPlayerController == players[selected])
                    GameNetworkManager.Instance.localPlayerController.KillPlayer(default, causeOfDeath: CauseOfDeath.Unknown);
                else
                {
                    APLCNetworking.Instance.KillPlayerClientRpc(players[selected].playerClientId);
                }
                ChatHandler.SendMessage($"AP: {DLMessage}");
            }
            WaitingForDeath = false;
            DLMessage = "";
            if (StartOfRound.Instance.allPlayersDead)
            {
                IgnoreDL = true;
            }
        }

        if (GetGoal() == 2)
        {
            if(GetItemMap("Company Credit").GetTotal() >= _apConnection.GetSlotSettingInt("companycreditsgoal") && _apConnection.IsConnected())
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
#if ENABLE_PROFILER
        using var automarker = s_ProcessItems.Auto();
#endif
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
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"Error processing Progressive Flashlight. This is not likely to cause issues but we are logging it just in case. {e}");
                }
            }
            else if (name == "Company Building")
            {
                try
                {
                    _itemMap["71 Gordion"].OnReceived();
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"Error processing Company Building. This is not likely to cause issues but we are logging it just in case. {e}");
                }
            }
            else if (name == "LoudHorn")
            {
                try
                {
                    _itemMap["Loud horn"].OnReceived();
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"Error processing Loud Horn. This is not likely to cause issues but we are logging it just in case. {e}");
                }
            }
            else if (name == "SignalTranslator")
            {
                try
                {
                    _itemMap["Signal translator"].OnReceived();
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"Error processing Signal translator. This is not likely to cause issues but we are logging it just in case. {e}");
                }
            }
            else if (name == "InverseTeleporter")
            {
                try
                {
                    _itemMap["Inverse Teleporter"].OnReceived();
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"Error processing Inverse Teleporter. This is not likely to cause issues but we are logging it just in case. {e}");
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
                    Plugin.Logger.LogError($"Error processing {name}. {e}");
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
#if DEBUG
            Plugin.Logger.LogDebug(itemName);   
            Plugin.Logger.LogDebug(item.GetType().FullName);
# endif
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
        Plugin.Logger.LogInfo("Received death link");

        Random.InitState(link.Timestamp.Millisecond);

        WaitingForDeath = true;
        DLMessage = link.Cause;
    }

    public Dictionary<string,Collection<ValueTuple<string,double>>> GetScrapData()
    {
        return _scrapData;
    }

    internal void ResetTrophyList()
    {
        _trophyModeComplete = new object[_moons.Length];
    }
    internal object[] GetTrophyList()
    {
        return _trophyModeComplete;
    }

    // This is a modified version of LLL's difficulty calculation
    public static int CalculateMoonDifficultyRating(SelectableLevel level, bool debugResults = false)
    {
        int calculatedDifficulty = 0;
        string debugtext = "Calculated Difficulty Rating For Level: " + level.PlanetName + "(" + level.riskLevel + ") ----- ";
        int routePrice = NormalMoonPrices[level.PlanetName];
        //int scrapValue = level.maxTotalScrapValue;
        calculatedDifficulty += routePrice;
        debugtext = debugtext + "Baseline Route Price: " + routePrice + ", ";
        float num2 = 0;
        int totalScrapWeight = level.spawnableScrap.Sum(item => item.rarity);
        foreach (SpawnableItemWithRarity item in level.spawnableScrap)
        {
            if (item.spawnableItem != null && item.rarity != 0 && (item.spawnableItem.maxValue - item.spawnableItem.minValue > 0))
            {
                float spawnChance = item.rarity / (float)totalScrapWeight;
                float average = (item.spawnableItem.minValue + item.spawnableItem.maxValue) / 2.0f;
                num2 += 3000 / (average * (spawnChance < 1 ? (float)Math.Pow(1 - spawnChance, 2) : 1)); // favors low worth high weight scrap
            }
        }
        num2 *= 0.03125f * (float)Math.Sqrt(Math.Abs(35 - (level.maxScrap + level.minScrap) / 2.0f)) + 1;  // favors moons with very low or absurdly high scrap amount
        calculatedDifficulty += Mathf.RoundToInt(num2);
        debugtext = debugtext + "Scrap Value: " + num2 + ", ";

        float totalEnemyValue = 30 * CalculateEnemyGroupDifficulty(level.Enemies, level.maxEnemyPowerCount) +
            30 * CalculateEnemyGroupDifficulty(level.OutsideEnemies, level.maxOutsideEnemyPowerCount) +
            (level.DaytimeEnemies.Any(enemy => !enemy.enemyType.isDaytimeEnemy) ? 15 : 5) * CalculateEnemyGroupDifficulty(level.DaytimeEnemies, level.maxDaytimeEnemyPowerCount);

        calculatedDifficulty += Mathf.RoundToInt(totalEnemyValue);
        debugtext = debugtext + "Enemy Value: " + totalEnemyValue + ", ";
        debugtext = debugtext + "Calculated Difficulty Value: " + calculatedDifficulty + ", ";
        calculatedDifficulty += Mathf.RoundToInt((float)calculatedDifficulty * (level.factorySizeMultiplier * 0.5f));
        debugtext = debugtext + "Factory Size Multiplier: " + level.factorySizeMultiplier + ", ";
        debugtext = debugtext + "Multiplied Calculated Difficulty Value: " + calculatedDifficulty;
        if (debugResults)
        {
            Plugin.Logger.LogDebug(debugtext);
        }

        return calculatedDifficulty;
    }

    public static int CalculateEnemyGroupDifficulty(List<SpawnableEnemyWithRarity> enemyGroup, int maxEnemyGroupPower)
    {
        if (enemyGroup.Any(enemy => enemy.rarity > 0))
        {
            int totalEnemyRarity = 0;
            float totalEnemyValue = 0f;
            foreach (SpawnableEnemyWithRarity enemy in enemyGroup)
            {
                if (enemy.rarity != 0 && enemy.enemyType != null)
                {
                    totalEnemyValue += enemy.enemyType.PowerLevel * enemy.rarity;
                    totalEnemyRarity += enemy.rarity;
                }
            }
            totalEnemyValue = maxEnemyGroupPower * totalEnemyValue / totalEnemyRarity;
            return Mathf.RoundToInt(totalEnemyValue);
        }
        return 0;
    }
}