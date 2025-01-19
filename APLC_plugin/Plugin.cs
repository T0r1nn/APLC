using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.UIElements.Collections;

namespace APLC;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("Lethal Company.exe")]
public class Plugin : BaseUnityPlugin
{
    //Instance of the plugin for other classes to access
    public static Plugin Instance;
    
    /**
     * Patches the game on startup, injecting the code into the game.
     */
    private void Awake()
    {
        if (Instance == null) Instance = this;
        
        NetcodePatch();
        Patches.Patch();
        TerminalCommands.Patch();
    
        LogInfo("Plugin APLC Loaded");
    }

    /**
     * Gets the terminal object for editing(needs to be here because only monobehaviors can findobjectoftype)
     */
    public Terminal GetTerminal()
    {
        return FindObjectOfType<Terminal>();
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
     * Logs an error to the console
     */
    public void LogError(string message)
    {
        Logger.LogError(message);
    }

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
                    ""chance"": {moon.Item2}
                }}";
            if (index < scrapData.Count - 1)
            {
                str += ",";
            }

            str += "\n";
        }

        return str;
    }
    
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

        var moons = new SelectableLevel[allMoons.Length - 2];

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
            { "Knife", new Collection<Tuple<string, double>>() },
            { "Hive", new Collection<Tuple<string, double>>() }
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
                scrapMap.TryAdd(item.spawnableItem.itemName, new Collection<Tuple<string, double>>());
                var checkMoons = scrapMap.Get(item.spawnableItem.itemName);
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
                    scrapMap.Get(item.spawnableItem.itemName)
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
            }
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
                        scrapMap.Get("Knife").Add(new Tuple<string, double>(moon.PlanetName, (double)item.rarity / totalRarity[2]));
                    }
                }
            }
        }

        return new Tuple<Item[], BuyableVehicle[], SelectableLevel[], Dictionary<string, Collection<Tuple<string, double>>>, Dictionary<string, Collection<Tuple<string, double>>>>(store, vehicles, moons, bestiaryMap, scrapMap);
    }
    
    private void NetcodePatch()
    {
        try
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
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
        catch
        {
            LogError("NetcodePatcher Failed! This Is Very Bad.");
        }
    }
}