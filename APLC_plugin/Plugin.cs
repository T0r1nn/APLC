using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using Dawn;
using UnityEngine;
using UnityEngine.UIElements.Collections;

namespace APLC;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(LethalLevelLoader.Plugin.ModGUID, Flags: BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(LethalAPI.LibTerminal.PluginInfo.PLUGIN_GUID, Flags: BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(DawnLib.PLUGIN_GUID, Flags: BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("Lethal Company.exe")]
public class Plugin : BaseUnityPlugin
{
    //Instance of the plugin for other classes to access
    public static Plugin Instance;
    public static bool IsDawnLibInstalled => Chainloader.PluginInfos.ContainsKey(DawnLib.PLUGIN_GUID);
    public static bool IsLethalExpansionInstalled => Chainloader.PluginInfos.ContainsKey("LethalExpansion") || Chainloader.PluginInfos.ContainsKey("LethalExpansionCore");
    private Terminal terminal = null;
    internal static PluginConfig BoundConfig { get; private set; } = null!;

    /**
     * Patches the game on startup, injecting the code into the game.
     */
    private void Awake()
    {
        if (Instance == null) Instance = this;

        BoundConfig = new PluginConfig(base.Config);
        NetcodePatch();
        Patches.Patch();
        TerminalCommands.Patch();

        LogInfo($"Plugin APLC Loaded - Version {PluginInfo.PLUGIN_VERSION}");
    }

    /**
     * Gets the terminal object for editing(needs to be here because only monobehaviors can findobjectoftype).
     * The terminal is cached after the first time it is found to avoid repeated calls to findobjectoftype.
     */
    public Terminal GetTerminal()
    {
        if (terminal == null)
        {
            LogInfo("Terminal is not known. Finding terminal...");
            terminal = FindAnyObjectByType<Terminal>();
            if (terminal == null)
            {
                LogError("Could not find object of type Terminal! This is very bad.");
            }
        }
        return terminal;
    }

    /**
     * Logs a message to the console, but only in debug builds
     */
    [System.Diagnostics.Conditional("DEBUG")]
    public void LogIfDebugBuild(string message)
    {
        Logger.LogFatal(message);   // using Fatal so we can clearly see these when testing
    }

    /**
     * Logs a warning to the console
     */
    public void LogWarning(string message)
    {
        Logger.LogWarning(message);
    }
    
    /**
     * Logs info to the console
     */
    public void LogInfo(string message)
    {
        Logger.LogInfo(message);
    }

    /**
     * Logs info to the output file but not the console
     */
    public void LogDebug(string message)
    {
        Logger.LogDebug(message);
    }

    /**
     * Logs an error to the console
     */
    public void LogError(string message)
    {
        Logger.LogError(message);
    }

    /**
     * Formats the game logic as a JSON string in the order of moons, store, vehicles, scrap, bestiary
     */
    public string GetGameLogicString()
    {
        var logic = GetGameLogic();
        
        /*
         * {
         *      moons: [],
         *      logs: [
         *          {
         *              log_name: name,
         *              moons: [moon1, moon2]
         *          }
         *      ],
         *      bestiary: [
         *          {
         *              monster_name: name,
         *              moons: [
         *                  {
         *                      moon_name: moon
         *                      spawn_prob: prob
         *                  }
         *              ]
         *          }
         *      ],
         *      store: [
         *          item1
         *          item2
         *      ],
         *      scrap: [
         *          {
         *              scrap_name: name,
         *              moons: [
         *                  {
         *                      moon_name: moon
         *                      spawn_prob: prob
         *                  }
         *              ]
         *          }
         *      ]
         * }
         */
        var store = logic.Item1;

        var vehicles = logic.Item2;
        
        string json = @"{
    ""moons"": [
";
        var moons = logic.Item3;
        
        foreach (SelectableLevel moon in moons)
        {
            if (moon.PlanetName.Contains("Gordion") || moon.PlanetName.Contains("Liquidation")) continue;
            json += "        \"" + moon.PlanetName + "\",\n";
        }
        
        json += @"    ],
    ""store"": [
";
        foreach (Item item in store)
        {
            json += "        \"" + item.itemName + "\",\n";
        }
        
        json += @"    ],
    ""vehicles"": [
";
        foreach (BuyableVehicle item in vehicles)
        {
            json += "        \"" + item.vehicleDisplayName + "\",\n";
        }
        
        json += @"    ],
    ""scrap"": [
";

        var scrapMap = logic.Item5;

        var bestiaryMap = logic.Item4;
        
        foreach (string key in scrapMap.Keys)
        {
            json += @$"        {{
            ""{key}"": [
{CreateScrapJSON(scrapMap.Get(key))}
            ]
        }},
";
        }

        json += @"    ],
    ""bestiary"": [
";
        
        foreach (string key in bestiaryMap.Keys)
        {
            json += @$"        {{
            ""{key}"": [
{CreateScrapJSON(bestiaryMap.Get(key))}
            ]
        }},
";
        }


        json += "    ]";

        return json + "\n}";
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private string CreateScrapJSON(Collection<Tuple<string, double>> scrapData)
    {
        var str = "";
        for (var index = 0; index < scrapData.Count; index++)
        {
            var moon = scrapData[index];
            str += $@"                {{
                    ""moon_name"": ""{moon.Item1}"",
                    ""chance"": {moon.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture)}
                }}";
            if (index < scrapData.Count - 1)
            {
                str += ",";
            }

            str += "\n";
        }

        return str;
    }

    /**
     * Gets the game logic as a tuple of store items, vehicles, moons, enemy spawn chances, and scrap spawn chances
     * The spawn chance for a piece of scrap or an enemy on a moon is actually the chance of the enemy/scrap being picked for each spawn roll, not the chance of it spawning overall
     */
    public Tuple<Item[], BuyableVehicle[], SelectableLevel[], Dictionary<string, Collection<Tuple<string, double>>>, Dictionary<string, Collection<Tuple<string, double>>>> GetGameLogic()
    {
        Terminal t = GetTerminal();
        
        String[] vanillaMoonNames = ["experimentation", "assurance", "vow", "adamance", "offense", "march", "embrion", "rend", "dine", "titan", "artifice", "liquidation"];
        foreach (var moon in StartOfRound.Instance.levels)
        {
            if(!vanillaMoonNames.Contains(moon.PlanetName))
            {
                int totalRarity = 0;
                foreach (var scrap in moon.spawnableScrap)
                {
                    totalRarity += scrap.rarity;
                }
                foreach (var scrap in moon.spawnableScrap)
                {
                    if (scrap.spawnableItem.name.Equals("ap_apparatus_custom"))
                    {
                        scrap.rarity = (int)(0.03626943005 * totalRarity);
                        scrap.spawnableItem.itemName = "AP Apparatus - Custom";
                    }
                }
            }
        }
        
        /*
         * {
         *      moons: [],
         *      logs: [
         *          {
         *              log_name: name,
         *              moons: [moon1, moon2]
         *          }
         *      ],
         *      bestiary: [
         *          {
         *              monster_name: name,
         *              moons: [
         *                  {
         *                      moon_name: moon
         *                      spawn_prob: prob
         *                  }
         *              ]
         *          }
         *      ],
         *      store: [
         *          item1
         *          item2
         *      ],
         *      scrap: [
         *          {
         *              scrap_name: name,
         *              moons: [
         *                  {
         *                      moon_name: moon
         *                      spawn_prob: prob
         *                  }
         *              ]
         *          }
         *      ]
         * }
         */
        var store = t.buyableItemsList;

        var vehicles = t.buyableVehicles;
        
        var allMoons = StartOfRound.Instance.levels;

        var moons = new SelectableLevel[allMoons.Length - 2];   // todo: change this to use ExtendedLevels instead of SelectableLevels

        int skipped = 0;
        for (int i = 0; i < allMoons.Length; i++)
        {
            if (allMoons[i].PlanetName.Contains("Liquidation") || allMoons[i].PlanetName.Contains("Gordion"))
            {
                skipped++;
                continue;
            }
            moons[i - skipped] = allMoons[i];
        }
        
        var scrapMap = new Dictionary<string, Collection<Tuple<string, double>>>
        {
            { "Apparatus", new Collection<Tuple<string, double>>() },
            { "Shotgun", new Collection<Tuple<string, double>>() },
            { "Kitchen knife", new Collection<Tuple<string, double>>() },
            { "Hive", new Collection<Tuple<string, double>>() },
            { "Sapsucker Egg", new Collection<Tuple<string, double>>() }
        };

        foreach (SelectableLevel moon in moons)
        {
            if (moon.PlanetName.Contains("Gordion") || moon.PlanetName.Contains("Liquidation")) continue;

            var scrap = moon.spawnableScrap;
            int totalRarity = 0;
            foreach (var item in scrap)
            {
                    totalRarity += item.rarity;
            }
            foreach (var item in scrap)
            {
                if (item.spawnableItem.itemName.Contains("AP Apparatus - ") &&
                    moon.PlanetName.Contains(
                        item.spawnableItem.itemName[new Range(15, item.spawnableItem.itemName.Length)]))    
                {
                    if (item.spawnableItem.itemName.Contains("Adamance"))   // Adamance has two apparatuses, and the second one seems to replace the one in moon.spawnableScrap.
                                                                            // This is an expensive and hacky way to fix it, but I don't have a better one right now
                    {
                        var spawningItems = Resources.FindObjectsOfTypeAll<Item>().Where(item => item.spawnPrefab && item.itemName.Contains("AP Apparatus - Adamance")).ToHashSet();

                        foreach (var appy in spawningItems)
                        {
                            appy.itemName = $"AP Apparatus - {moon.PlanetName}";
                        }
                    }
                    else
                        item.spawnableItem.itemName = $"AP Apparatus - {moon.PlanetName}";
                }else if (item.spawnableItem.itemName.Contains("AP Apparatus - ") && !item.spawnableItem.itemName.Contains("Custom"))
                {
                    continue;
                }

                string scrapName = item.spawnableItem.itemName.Equals("AP Apparatus - Custom") ? $"AP Apparatus - {moon.PlanetName}" : item.spawnableItem.itemName;
                scrapMap.TryAdd(scrapName, new Collection<Tuple<string, double>>());
                var checkMoons = scrapMap.Get(scrapName);
                bool existsAlready = false;
                
                for (var index = 0; index < checkMoons.Count; index++)
                {
                    var entry = checkMoons[index];
                    if (entry.Item1 == moon.PlanetName)
                    {
                        checkMoons[index] = new Tuple<string, double>(entry.Item1,
                            entry.Item2 + (double)item.rarity / totalRarity);
                        existsAlready = true;
                    }
                }

                if (!existsAlready)
                {
                    scrapMap.Get(scrapName)
                        .Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity));
                }
            }

            int totalIntRarity = 0;
            int facilityRarity = 0;
            foreach (var interior in moon.dungeonFlowTypes)
            {
                totalIntRarity += interior.rarity;
                if (interior.id == 0)
                {
                    facilityRarity = interior.rarity;
                }
            }

            if (Double.IsNaN((double)facilityRarity / totalIntRarity))
            {
                totalIntRarity = 1;
                facilityRarity = 1;
            }
            scrapMap.Get("Apparatus").Add(new Tuple<string, double>(moon.PlanetName, (double)facilityRarity/totalIntRarity));
        }
        
        var bestiaryMap = new Dictionary<string, Collection<Tuple<string, double>>> { };

        foreach (SelectableLevel moon in moons)
        {
            if (moon.PlanetName.Contains("Gordion") || moon.PlanetName.Contains("Liquidation")) continue;
            

            var daytime = moon.DaytimeEnemies;
            var outside = moon.OutsideEnemies;
            var inside = moon.Enemies;
            int[] totalRarity = new int[]{0,0,0};
            foreach (var item in daytime)
            {
                totalRarity[0] += item.rarity;
            }
            foreach (var item in outside)
            {
                totalRarity[1] += item.rarity;
            }
            foreach (var item in inside)
            {
                totalRarity[2] += item.rarity;
            }

            if (totalRarity[0] > 0)
                foreach (var item in daytime)
                {
                    try
                    {
                        string creatureName = t.enemyFiles[
                                item.enemyType.enemyPrefab.GetComponentInChildren<ScanNodeProperties>()
                                    .creatureScanID]
                            .creatureName;
                        if (creatureName[^1] == 's')
                        {
                            creatureName = creatureName.Substring(0, creatureName.Length - 1);
                        }
                        bestiaryMap.TryAdd(creatureName, new Collection<Tuple<string, double>>());
                        bool existsAlready = false;
                        var checkMoons = bestiaryMap.Get(creatureName);
                        for (var index = 0; index < checkMoons.Count; index++)
                        {
                            var entry = checkMoons[index];
                            if (entry.Item1 == moon.PlanetName)
                            {
                                checkMoons[index] = new Tuple<string, double>(entry.Item1,
                                    entry.Item2 + (double)item.rarity / totalRarity[0]);
                                existsAlready = true;
                            }
                        }

                        if (!existsAlready)
                        {
                            bestiaryMap.Get(creatureName)
                                .Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity[0]));
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore exception
                    }

                    if (item.enemyType.enemyName.Contains("Red Locust"))
                    {
                        scrapMap.Get("Hive").Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity[0]));
                    }
                    else if (item.enemyType.enemyName.Contains("GiantKiwi"))
                    {
                        scrapMap.Get("Sapsucker Egg").Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity[0]));
                    }
                }
            if (totalRarity[1] > 0)
                foreach (var item in outside)
                {
                    try
                    {
                        string creatureName = t.enemyFiles[
                                item.enemyType.enemyPrefab.GetComponentInChildren<ScanNodeProperties>()
                                    .creatureScanID]
                            .creatureName;
                        if (creatureName[^1] == 's')
                        {
                            creatureName = creatureName[..^1];
                        }
                        bestiaryMap.TryAdd(creatureName, []);
                        bool existsAlready = false;
                        var checkMoons = bestiaryMap.Get(creatureName);
                        for (var index = 0; index < checkMoons.Count; index++)
                        {
                            var entry = checkMoons[index];
                            if (entry.Item1 == moon.PlanetName)
                            {
                                checkMoons[index] = new Tuple<string, double>(entry.Item1,
                                    entry.Item2 + (double)item.rarity / totalRarity[1]);
                                existsAlready = true;
                            }
                        }

                        if (!existsAlready)
                        {
                            bestiaryMap.Get(creatureName)
                                .Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity[1]));
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore exception
                    }
                }
            if (totalRarity[2] > 0)
                foreach (var item in inside)
                {
                    if (!item.enemyType.enemyName.Contains("Lasso"))
                    {
                        try
                        {
                            string creatureName = t.enemyFiles[
                                    item.enemyType.enemyPrefab.GetComponentInChildren<ScanNodeProperties>()
                                        .creatureScanID]
                                .creatureName;
                            if (creatureName[^1] == 's')
                            {
                                creatureName = creatureName[..^1];
                            }
                            bestiaryMap.TryAdd(creatureName, []);
                            bool existsAlready = false;
                            var checkMoons = bestiaryMap.Get(creatureName);
                            for (var index = 0; index < checkMoons.Count; index++)
                            {
                                var entry = checkMoons[index];
                                if (entry.Item1 == moon.PlanetName)
                                {
                                    checkMoons[index] = new Tuple<string, double>(entry.Item1,
                                        entry.Item2 + (double)item.rarity / totalRarity[2]);
                                    existsAlready = true;
                                }
                            }

                            if (!existsAlready)
                            {
                                bestiaryMap.Get(creatureName)
                                    .Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity[2]));
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore exception
                        }

                        if (item.enemyType.enemyName.Contains("Nutcracker"))
                        {
                            scrapMap.Get("Shotgun").Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity[2]));
                        }
                        if (item.enemyType.enemyName.Contains("Butler"))
                        {
                            scrapMap.Get("Kitchen knife").Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity[2]));
                        }
                    }
                }
        }

        return new Tuple<Item[], BuyableVehicle[], SelectableLevel[], Dictionary<string, Collection<Tuple<string, double>>>, Dictionary<string, Collection<Tuple<string, double>>>>(store, vehicles, moons, bestiaryMap, scrapMap);
    }
    
    private void NetcodePatch()
    {
        Type[] types = null!;
        try
        {
            types = Assembly.GetExecutingAssembly().GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = [.. ex.Types.Where(type => type is not null)!];
        }
        try
        {
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }
        catch (Exception ex) 
        {
            LogError($"NetcodePatcher Failed! This Is Very Bad. \n{ex}");
        }
    }
}