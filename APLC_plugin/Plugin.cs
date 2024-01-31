using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements.Collections;
using Random = System.Random;

namespace APLC;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("Lethal Company.exe")]
public class Plugin : BaseUnityPlugin
{
    //Instance of the plugin for other classes to access
    public static Plugin _instance;

    //Harmony for patching
    private readonly Harmony _harmony = new(PluginInfo.PLUGIN_GUID);

    private int brackenItemsWaiting;

    //Same as above but for logs
    private readonly bool[] checkedLogs = new bool[13];

    //Checks if a bestiary entry has already been checked so we don't spam the server with useless info.
    private readonly bool[] checkedMonsters = new bool[18];
    private int checksPerMoon = 3;
    private int collectathonGoal = 20;

    private readonly Dictionary<string, bool> collectedMoonMap = new();

    //These store settings from the yaml file that are stored in the slot.
    private bool deathLink;

    //Death link service for when death link is enabled
    private DeathLinkService dlService;

    //Some code should only run once, like defining the itemMap, this makes that happen.
    private bool firstTimeSetup = true;

    //You only want to use things if they exist, this makes sure every object we need exists before messing with them
    private bool gameStarted;
    private int goal;
    private int hauntItemsWaiting;

    //The amount of unlocked inventory slots
    private int invSlots = 4;
    private int startingInvSlots = 4;

    //Maps the item names to a array of three integers
    //  0: The index in the array the item is(for example, 0 for walkie-talkie)
    //  1: The initial price of the item(since we overwrite it we want to store it somewhere so we can replace the insanely high price with this when the item is unlocked
    //  2: The type of item it is. 0 for shop items, 1 for ship upgrades, 2 for moons
    private readonly Dictionary<string, int[]> itemMap = new();
    private string lastChatMessagePost = "";
    private string lastChatMessagePre = "";
    private int maxMoney = 1000;
    private int minMoney = 100;

    //Sometimes we aren't ready to receive certain items, so we mark them as received and que them by incrementing these variables
    private int moneyItemsWaiting;
    private int moneyPerQuotaCheck = 1000;

    //Stores how many times each moon has been completed on a B or higher
    private int[] moonChecks = { 0, 0, 0, 0, 0, 0, 0, 0 };

    //Maps the name of the moon to its number because the terminal nodes that refer to moons are named based off of number and not their actual name 
    private readonly Dictionary<string, string> moonNameMap = new();
    private int moonRank;

    //Stores all received items
    private Collection<string> newItems = new();
    private int numQuota = 20;
    private string password = "";

    private int port;

    //The number of quota checks that have been met
    private int quotaChecksMet;
    private bool randomizeScanner;
    private bool scannerUnlocked;

    //Checks if an item has already been received, if it hasn't we set its index to true and do whatever is required to receive it
    private int receivedBrackenItems;
    private int receivedHauntItems;

    //Checks how many money items were received, if this ever gets higher than totalMoneyItems than 
    private int receivedMoneyItems;
    //private double apparatusChance = 0.1;

    //Tracks progress towards the collectathon goal
    private int scrapCollected;

    //The archipelago sesion
    private ArchipelagoSession session;

    //The slot name as set from the .cfg
    private string slotName = "";
    private int staminaChecks = 4;
    private int startingStaminaChecks = 4;

    //Bool that checks if the player has successfully connected to the server
    private bool successfullyConnected;

    private float time = 0;
    private float time2 = 0;
    private int totalBrackenItems;
    private int totalHauntItems;

    //Useful for when a client disconnects, then rejoins the APworld. We don't want them to receive all of the money items again because that would essentially lead to an infinite money glitch. 
    private int totalMoneyItems;

    //The total received quota so far
    private int totalQuota;
    private string[] trophyModeComplete = new string[8];
    private string url = "";
    private bool waitingForPassword;
    private bool waitingForSlot;

    /// <summary>
    ///     Runs when the game first boots up, connects to AP and sets up all the variables that need setup
    /// </summary>
    private void Awake()
    {
        if (_instance == null) _instance = this;

        for (var i = 0; i < checkedMonsters.Length; i++) checkedMonsters[i] = false;

        for (var i = 0; i < checkedLogs.Length; i++) checkedLogs[i] = false;
        moonNameMap.Add("Experimentation", "41");
        moonNameMap.Add("Assurance", "220");
        moonNameMap.Add("Vow", "56");
        moonNameMap.Add("Offense", "21");
        moonNameMap.Add("March", "61");
        moonNameMap.Add("Rend", "85");
        moonNameMap.Add("Dine", "7");
        moonNameMap.Add("Titan", "8");


        _harmony.PatchAll(typeof(Plugin));

        // Plugin startup logic
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    /// <summary>
    ///     Adds spaces before captial letters in a string
    /// </summary>
    /// <param name="text">The string to add spaces to</param>
    /// <returns>The modified string</returns>
    private string AddSpacesToSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var newText = new StringBuilder(text.Length * 2);
        newText.Append(text[0]);
        for (var i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]))
                if (text[i - 1] != ' ' && !char.IsUpper(text[i - 1]))
                    newText.Append(' ');
            newText.Append(text[i]);
        }

        return newText.ToString();
    }

    /// <summary>
    ///     Completes an archipelago location
    /// </summary>
    /// <param name="lName">The name of the location to complete</param>
    private void CompleteLocation(string lName)
    {
        var id = _instance.session.Locations.GetLocationIdFromName("Lethal Company", lName);
        _instance.session.Locations.CompleteLocationChecks(id);
    }

    /// <summary>
    ///     Handles the reception of items from AP
    /// </summary>
    /// <param name="helper">The helped</param>
    private void ReceivedItem(ReceivedItemsHelper helper)
    {
        Logger.LogWarning($"Received Item {helper.PeekItemName()}");
        newItems.Add(helper.PeekItemName());
        CheckItems();
        helper.DequeueItem();
    }

    private void CheckTraps()
    {
        Logger.LogInfo("Checking Traps");
        if (brackenItemsWaiting > 0)
            if (SpawnEnemyByName("flower"))
            {
                brackenItemsWaiting--;
                totalBrackenItems++;
                session.DataStorage["brackenTrapsReceived"] = totalBrackenItems;
            }

        if (hauntItemsWaiting > 0)
            if (SpawnEnemyByName("dress"))
            {
                hauntItemsWaiting--;
                totalHauntItems++;
                session.DataStorage["hauntTrapsReceived"] = totalHauntItems;
            }
    }

    /// <summary>
    ///     Probably should split this into two functions, as of right now what it does is it checks if certain locations have
    ///     been completed and if certain checks have been received, then processes them.
    /// </summary>
    private void CheckItems()
    {
        if (!gameStarted || !successfullyConnected) return;
        try
        {
            invSlots = startingInvSlots;
            staminaChecks = startingStaminaChecks;
            scannerUnlocked = !randomizeScanner;
            for (var i = 0; i < newItems.Count; i++)
            {
                if (newItems[i] == "Inventory Slot")
                {
                    invSlots++;
                    Logger.LogWarning("Received Inventory Slot");
                }
                else if (newItems[i] == "Stamina Bar")
                {
                    staminaChecks++;
                    Logger.LogWarning("Received Stamina Bar");
                }
                else if (newItems[i] == "Scanner")
                {
                    scannerUnlocked = true;
                    Logger.LogWarning("Received Scanner");
                }
            }
        }
        catch (Exception err)
        {
            Logger.LogError(err.Message);
            Logger.LogError(err.StackTrace);
            return;
        }
        
        if (!GameNetworkManager.Instance.isHostingGame) return;

        try
        {
            var t = FindObjectOfType<Terminal>();
            var items = t.buyableItemsList;

            foreach (var mID in t.scannedEnemyIDs)
                if (!checkedMonsters[mID])
                {
                    var eName = t.enemyFiles[mID].name;
                    eName = eName.Substring(0, eName.Length - 4);
                    string formatted;
                    if (eName == "CoilHead")
                        formatted = "Coil-Head";
                    else if (eName == "Snareflea")
                        formatted = "Snare Flea";
                    else if (eName == "Locust")
                        formatted = "Roaming Locust";
                    else if (eName == "Puffer")
                        formatted = "Spore Lizard";
                    else
                        formatted = AddSpacesToSentence(eName);

                    CompleteLocation($"Bestiary Entry - {formatted}");
                    checkedMonsters[mID] = true;
                }

            foreach (var mID in t.unlockedStoryLogs)
                if (!checkedLogs[mID])
                {
                    var logName = "";

                    switch (mID)
                    {
                        case 0:
                            continue;
                        case 1:
                            logName = "Smells Here!";
                            break;
                        case 2:
                            logName = "Swing of Things";
                            break;
                        case 3:
                            logName = "Shady";
                            break;
                        case 4:
                            logName = "Sound Behind the Wall";
                            break;
                        case 5:
                            logName = "Goodbye";
                            break;
                        case 6:
                            logName = "Screams";
                            break;
                        case 7:
                            logName = "Golden Planet";
                            break;
                        case 8:
                            logName = "Idea";
                            break;
                        case 9:
                            logName = "Nonsense";
                            break;
                        case 10:
                            logName = "Hiding";
                            break;
                        case 11:
                            logName = "Real Job";
                            break;
                        case 12:
                            logName = "Desmond";
                            break;
                    }


                    if (logName == "")
                    {
                        checkedLogs[mID] = true;
                        continue;
                    }

                    CompleteLocation($"Log - {logName}");

                    checkedLogs[mID] = true;
                }

            receivedMoneyItems = 0;
            receivedHauntItems = 0;
            receivedBrackenItems = 0;
            for (var i = 0; i < newItems.Count; i++)
            {
                var itemName = newItems[i];
                if (itemName == "Money")
                {
                    if (t != null && totalMoneyItems <= receivedMoneyItems)
                    {
                        t.groupCredits += new Random().Next(minMoney, maxMoney);
                    }
                    else
                    {
                        if (totalMoneyItems <= receivedMoneyItems) moneyItemsWaiting++;
                    }

                    if (totalMoneyItems >= receivedMoneyItems) totalMoneyItems++;

                    receivedMoneyItems++;
                    session.DataStorage["moneyChecksReceived"] = totalMoneyItems;
                }

                if (itemName == "HauntTrap")
                {
                    receivedHauntItems++;
                    if (receivedHauntItems > totalHauntItems)
                    {
                        if (!SpawnEnemyByName("dress"))
                            hauntItemsWaiting++;
                        else
                            totalHauntItems++;
                        session.DataStorage["hauntTrapsReceived"] = totalHauntItems;
                    }
                }

                if (itemName == "BrackenTrap")
                {
                    receivedBrackenItems++;
                    if (receivedBrackenItems > totalBrackenItems)
                    {
                        if (!SpawnEnemyByName("flower"))
                            brackenItemsWaiting++;
                        else
                            totalBrackenItems++;
                        session.DataStorage["brackenTrapsReceived"] = totalBrackenItems;
                    }
                }
            }
        }
        catch (Exception err)
        {
            Logger.LogError(err.StackTrace);
        }
    }

    [HarmonyPatch(typeof(StartOfRound), "Update")]
    [HarmonyPrefix]
    private static void TrapUpdate()
    {
        _instance.time += Time.deltaTime;
        while (_instance.time > 5f)
        {
            _instance.time -= 5f;
            _instance.CheckTraps();
        }
        _instance.time2 += Time.deltaTime;
        while (_instance.time2 > 15f)
        {
            _instance.time2 -= 15f;
            _instance.fixStuff();
        }
    }

    private void MessageReceived(LogMessage message)
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
                if (chatLogMessage.Player.Name == session.Players.GetPlayerAlias(session.ConnectionInfo.Slot)) return;

                break;
        }

        HUDManager.Instance.AddTextToChatOnServer(chat);
    }

    public static bool SpawnEnemy(SpawnableEnemyWithRarity enemy, int amount, bool inside, PlayerControllerB player)
    {
        try
        {
            if (inside ^ player.isInsideFactory) return false;

            var gameObject = Instantiate(enemy.enemyType.enemyPrefab,
                player.transform.position + new Vector3(0f, 0.5f, 0f), Quaternion.identity);
            gameObject.GetComponentInChildren<NetworkObject>().Spawn(true);
            //gameObject.gameObject.GetComponentInChildren<EnemyAI>().stunNormalizedTimer = 1f;
            return true;
        }
        catch (Exception e)
        {
            _instance.Logger.LogError(e.Message + "\n" + e.StackTrace);
            return false;
        }
    }

    public static bool SpawnEnemyByName(string name)
    {
        _instance.Logger.LogInfo($"Attempting to spawn creature {name}");
        if (!StartOfRound.Instance.shipHasLanded)
        {
            _instance.Logger.LogInfo("Failed spawn");
            return false;
        }

        var allPlayers = StartOfRound.Instance.allPlayerScripts;
        var i = UnityEngine.Random.RandomRangeInt(0, allPlayers.Length);
        var startI = i;

        var spawnPlayer = allPlayers[i];
        while (spawnPlayer.isPlayerDead || !spawnPlayer.isPlayerControlled ||
               spawnPlayer.playerUsername == "Player #0" || spawnPlayer.playerUsername == "Player #1" ||
               spawnPlayer.playerUsername == "Player #2" || spawnPlayer.playerUsername == "Player #3" ||
               spawnPlayer.playerUsername == "Player #4" || spawnPlayer.playerUsername == "Player #5" ||
               spawnPlayer.playerUsername == "Player #6" || spawnPlayer.playerUsername == "Player #7")
        {
            i++;
            i %= allPlayers.Length;
            spawnPlayer = allPlayers[i];
            if (i == startI)
            {
                _instance.Logger.LogInfo("Failed spawn");
                return false;
            }
        }

        foreach (var enemy in StartOfRound.Instance.currentLevel.Enemies)
            if (enemy.enemyType.enemyName.ToLower().Contains(name))
            {
                _instance.Logger.LogInfo("Attempting spawn");
                return SpawnEnemy(enemy, 1, true, spawnPlayer);
            }

        foreach (var enemy in StartOfRound.Instance.currentLevel.OutsideEnemies)
            if (enemy.enemyType.enemyName.ToLower().Contains(name))
            {
                _instance.Logger.LogInfo("Attempting spawn");
                return SpawnEnemy(enemy, 1, false, spawnPlayer);
            }

        foreach (var enemy in StartOfRound.Instance.currentLevel.DaytimeEnemies)
            if (enemy.enemyType.enemyName.ToLower().Contains(name))
            {
                _instance.Logger.LogInfo("Attempting spawn");
                return SpawnEnemy(enemy, 1, false, spawnPlayer);
            }

        _instance.Logger.LogInfo("Failed spawn");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "UpdateScanNodes")]
    private static bool CancelScan()
    {
        return !_instance.randomizeScanner || _instance.scannerUnlocked;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "PingScan_performed")]
    private static bool CancelScanAnimation()
    {
        return !_instance.randomizeScanner || _instance.scannerUnlocked;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    private static bool Sprint(PlayerControllerB __instance)
    {
        if (_instance.staminaChecks == 1)
        {
            __instance.sprintMeter = Mathf.Min(__instance.sprintMeter, 0.4f);
            return true;
        }
        __instance.sprintMeter = Mathf.Min(__instance.sprintMeter, _instance.staminaChecks * 0.25f);
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    private static bool CheckConnections(ref string chatMessage)
    {
        if (_instance.lastChatMessagePre == chatMessage) return false;
        if (_instance.lastChatMessagePost == chatMessage) return false;

        _instance.lastChatMessagePre = chatMessage;
        var tokens = chatMessage.Split(" ");
        if (tokens[0] == "APConnection:" && !GameNetworkManager.Instance.isHostingGame &&
            !_instance.successfullyConnected)
        {
            _instance.url = tokens[1];
            _instance.port = int.Parse(tokens[2]);
            _instance.slotName = "";
            for (var i = 3; i < tokens.Length-1; i++)
            {
                _instance.slotName += tokens[i];
                if (i < tokens.Length - 2) _instance.slotName += " ";
            }

            _instance.password = tokens[tokens.Length - 1];
            _instance.ConnectToAP();
        }

        if (tokens[0] == "RequestAPConnection:" && GameNetworkManager.Instance.isHostingGame &&
            _instance.successfullyConnected)
            HUDManager.Instance.AddTextToChatOnServer(
                $"APConnection: {_instance.url} {_instance.port} {_instance.slotName} {_instance.password}");

        if (tokens[0] != "APConnection:" && tokens[0] != "RequestAPConnection:")
            _instance.Logger.LogWarning("Passed Through Pre: " + chatMessage);

        return tokens[0] != "APConnection:" && tokens[0] != "RequestAPConnection:";
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    private static void OnMessageSent(ref string chatMessage)
    {
        var tokens = chatMessage.Split(" ");
        if (_instance.lastChatMessagePost == chatMessage) return;
        if (tokens[0] == "AP:" || tokens[0] == "APConnection:" || tokens[0] == "RequestAPConnection:") return;

        _instance.lastChatMessagePost = chatMessage;

        if (tokens[0][0] == '/')
        {
            if (tokens[0] == "/connect")
            {
                var parts = tokens[1].Split(":");
                if (GameNetworkManager.Instance.isHostingGame && !_instance.successfullyConnected &&
                    !_instance.waitingForSlot)
                {
                    _instance.url = parts[0];
                    _instance.port = int.Parse(parts[1]);
                    _instance.waitingForSlot = true;
                    HUDManager.Instance.AddTextToChatOnServer("AP: Please enter your slot name:");
                }
                else if (!GameNetworkManager.Instance.isHostingGame && !_instance.successfullyConnected)
                {
                    HUDManager.Instance.AddTextToChatOnServer("RequestAPConnection:");
                }
            }
            else if (_instance.successfullyConnected)
            {
                var msg = new SayPacket { Text = chatMessage };
                _instance.session.Socket.SendPacket(msg);
            }
        }
        else
        {
            try
            {
                if (_instance.waitingForSlot)
                {
                    _instance.slotName = chatMessage;
                    _instance.waitingForSlot = false;
                    _instance.waitingForPassword = true;
                    HUDManager.Instance.AddTextToChatOnServer(
                        "AP: Please enter your password(Enter the letter n if there isnt a password):");
                }
                else if (_instance.waitingForPassword)
                {
                    _instance.waitingForPassword = false;
                    _instance.password = chatMessage;
                    if (chatMessage == "n") _instance.password = "";

                    _instance.ConnectToAP();
                }
                else if (_instance.successfullyConnected)
                {
                    var msg = new SayPacket { Text = chatMessage };
                    _instance.session.Socket.SendPacket(msg);
                }
            }
            catch (Exception e)
            {
                _instance.Logger.LogWarning(e.Message + "\n" + e.StackTrace);
            }
        }
    }

    public void ConnectToAP()
    {
        try
        {
            session = ArchipelagoSessionFactory.CreateSession(url, port);
            if (password == "") password = null;

            var result =
                session.TryConnectAndLogin("Lethal Company", slotName, ItemsHandlingFlags.AllItems,
                    new Version(0, 4, 4), new[] { "Death Link" }, password: password);

            if (!result.Successful)
            {
                var failure = (LoginFailure)result;
                var errorMessage =
                    $"Failed to Connect to {url + ":" + port} as {slotName}:";
                foreach (var error in failure.Errors) errorMessage += $"\n    {error}";

                foreach (var error in failure.ErrorCodes) errorMessage += $"\n    {error}";

                HUDManager.Instance.AddTextToChatOnServer($"AP: <color=red>{errorMessage}</color>");
                return;
            }

            var successful = (LoginSuccessful)result;

            invSlots = int.Parse(successful.SlotData["inventorySlots"].ToString());
            startingInvSlots = invSlots;
            staminaChecks = int.Parse(successful.SlotData["staminaBars"].ToString());
            startingStaminaChecks = staminaChecks;
            randomizeScanner = int.Parse(successful.SlotData["scanner"].ToString()) == 1;
            scannerUnlocked = !randomizeScanner;
            moneyPerQuotaCheck = int.Parse(successful.SlotData["moneyPerQuotaCheck"].ToString());
            numQuota = int.Parse(successful.SlotData["numQuota"].ToString());
            checksPerMoon = int.Parse(successful.SlotData["checksPerMoon"].ToString());
            goal = int.Parse(successful.SlotData["goal"].ToString());
            minMoney = int.Parse(successful.SlotData["minMoney"].ToString());
            maxMoney = int.Parse(successful.SlotData["maxMoney"].ToString());
            moonRank = int.Parse(successful.SlotData["moonRank"].ToString());
            collectathonGoal = int.Parse(successful.SlotData["collectathonGoal"].ToString());
            deathLink = int.Parse(successful.SlotData["deathLink"].ToString()) == 1;
            HUDManager.Instance.AddTextToChatOnServer("AP: <color=green>Successfully connected to archipelago</color>");
            session.MessageLog.OnMessageReceived += MessageReceived;
        }

        catch (Exception err)
        {
            successfullyConnected = false;
            HUDManager.Instance.AddTextToChatOnServer(
                "AP: <color=red>Couldn't connect to Archipelago. Are you sure your info is correct?</color>");
            HUDManager.Instance.AddTextToChatOnServer($"AP: <color=red>{err.Message}\n{err.StackTrace}</color>");
            return;
        }

        if (deathLink)
        {
            dlService = session.CreateDeathLinkService();
            dlService.EnableDeathLink();
            dlService.OnDeathLinkReceived += link =>
            {
                var rng = new Random((int)link.Timestamp.Ticks);
                var selected = rng.Next(0, GameNetworkManager.Instance.connectedPlayers);
                if (GameNetworkManager.Instance.steamIdsInLobby[selected].Value ==
                    GameNetworkManager.Instance.localPlayerController.playerSteamId)
                    GameNetworkManager.Instance.localPlayerController.health = 0;
            };
        }

        session.DataStorage["moonChecks"].Initialize(new JArray(moonChecks));
        session.DataStorage["totalQuota"].Initialize(totalQuota);
        session.DataStorage["quotaChecksMet"].Initialize(quotaChecksMet);
        session.DataStorage["moneyChecksReceived"].Initialize(totalMoneyItems);
        session.DataStorage["scrapCollected"].Initialize(scrapCollected);
        session.DataStorage["hauntTrapsReceived"].Initialize(totalHauntItems);
        session.DataStorage["brackenTrapsReceived"].Initialize(totalBrackenItems);
        session.DataStorage["trophyScrap"].Initialize(new JArray(trophyModeComplete));

        moonChecks = session.DataStorage["moonChecks"];
        totalQuota = session.DataStorage["totalQuota"];
        quotaChecksMet = session.DataStorage["quotaChecksMet"];
        totalMoneyItems = session.DataStorage["moneyChecksReceived"];
        scrapCollected = session.DataStorage["scrapCollected"];
        totalHauntItems = session.DataStorage["hauntTrapsReceived"];
        totalBrackenItems = session.DataStorage["brackenTrapsReceived"];
        trophyModeComplete = session.DataStorage["trophyScrap"];

        session.Items.ItemReceived += ReceivedItem;

        gameStarted = true;

        if (GameNetworkManager.Instance.isHostingGame)
        {
            if (password == null) password = "";
            HUDManager.Instance.AddTextToChatOnServer($"APConnection: {url} {port} {slotName} {password}");
        }

        successfullyConnected = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "DisplayGlobalNotification")]
    public static bool CheckIfLogOrBestiary(string displayText)
    {
        if (displayText == "New creature data sent to terminal!" ||
            displayText.Substring(0, 19) == "Found journal entry") _instance.CheckItems();

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Terminal), "BeginUsingTerminal")]
    private static void TerminalStartPrefix(Terminal __instance)
    {
        if (!_instance.successfullyConnected) return;
        _instance.Setup(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), "FillEndGameStats")]
    private static void GradingPostfix()
    {
        var grade = HUDManager.Instance.statsUIElements.gradeLetter.text;
        var gradeInt = Array.IndexOf(new[] { "S", "A", "B", "C", "D", "F" }, grade);
        var dead = StartOfRound.Instance.allPlayersDead;
        _instance.Logger.LogWarning(dead);
        if (dead && _instance.deathLink)
            _instance.dlService.SendDeathLink(new DeathLink(_instance.slotName, "failed the company."));
        _instance.Logger.LogWarning(
            $"Completed planet {StartOfRound.Instance.currentLevel.PlanetName} with grade {grade}");
        if (gradeInt <= _instance.moonRank)
        {
            var moon = StartOfRound.Instance.currentLevel.PlanetName.Split(" ")[1];
            var checkNum = 0;
            string[] moonNames = { "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan" };
            var i = Array.IndexOf(moonNames, moon);
            checkNum = _instance.moonChecks[i];
            if (checkNum < _instance.checksPerMoon)
            {
                checkNum++;
                _instance.moonChecks[i]++;
                _instance.session.DataStorage["moonChecks"] = new JArray(_instance.moonChecks);
                _instance.CompleteLocation($"{moon} check {checkNum}");
            }
        }

        var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
            where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
            select obj).ToList();
        foreach (var scrap in list)
            if (scrap.name == "ap_chest(Clone)" && _instance.goal == 1)
            {
                Destroy(scrap.gameObject);
                _instance.scrapCollected++;
                _instance.session.DataStorage["scrapCollected"] = _instance.scrapCollected;
            }
            else if (_instance.goal == 0)
            {
                int trophyModeIndex = 0;
                while (_instance.trophyModeComplete[trophyModeIndex] != null)
                {
                    trophyModeIndex++;
                }
                switch (scrap.name)
                {
                    case "ap_apparatus_experimentation(Clone)":
                        if (Array.IndexOf(_instance.trophyModeComplete,"Experimentation") == -1)
                            _instance.trophyModeComplete[trophyModeIndex] = "Experimentation";
                        break;
                    case "ap_apparatus_assurance(Clone)":
                        if (Array.IndexOf(_instance.trophyModeComplete,"Assurance") == -1)
                            _instance.trophyModeComplete[trophyModeIndex] = "Assurance";
                        break;
                    case "ap_apparatus_vow(Clone)":
                        if (Array.IndexOf(_instance.trophyModeComplete,"Vow") == -1)
                            _instance.trophyModeComplete[trophyModeIndex] = "Vow";
                        break;
                    case "ap_apparatus_offense(Clone)":
                        if (Array.IndexOf(_instance.trophyModeComplete,"Offense") == -1)
                            _instance.trophyModeComplete[trophyModeIndex] = "Offense";
                        break;
                    case "ap_apparatus_march(Clone)":
                        if (Array.IndexOf(_instance.trophyModeComplete,"March") == -1)
                            _instance.trophyModeComplete[trophyModeIndex] = "March";
                        break;
                    case "ap_apparatus_rend(Clone)":
                        if (Array.IndexOf(_instance.trophyModeComplete,"Rend") == -1)
                            _instance.trophyModeComplete[trophyModeIndex] = "Rend";
                        break;
                    case "ap_apparatus_dine(Clone)":
                        if (Array.IndexOf(_instance.trophyModeComplete,"Dine") == -1)
                            _instance.trophyModeComplete[trophyModeIndex] = "Dine";
                        break;
                    case "ap_apparatus_titan(Clone)":
                        if (Array.IndexOf(_instance.trophyModeComplete,"Titan") == -1)
                            _instance.trophyModeComplete[trophyModeIndex] = "Titan";
                        break;
                }

                _instance.session.DataStorage["trophyScrap"] = new JArray(_instance.trophyModeComplete);
            }

        if (_instance.scrapCollected >= _instance.collectathonGoal && _instance.goal == 1)
        {
            var victory = new StatusUpdatePacket();
            victory.Status = ArchipelagoClientState.ClientGoal;
            _instance.session.Socket.SendPacket(victory);
        }
        else if (_instance.goal == 0)
        {
            string[] moons = { "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan" };
            var win = true;
            foreach (var moon in moons)
                if (Array.IndexOf(_instance.trophyModeComplete, moon) == -1)
                {
                    win = false;
                    break;
                }

            if (win)
            {
                var victory = new StatusUpdatePacket();
                victory.Status = ArchipelagoClientState.ClientGoal;
                _instance.session.Socket.SendPacket(victory);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
    private static void RoundEndPrefix(StartOfRound __instance)
    {
        if (GameNetworkManager.Instance.isHostingGame && _instance.quotaChecksMet < _instance.numQuota)
            if (TimeOfDay.Instance.profitQuota - TimeOfDay.Instance.quotaFulfilled <= 0f)
            {
                _instance.totalQuota += TimeOfDay.Instance.profitQuota;
                _instance.session.DataStorage["totalQuota"] = _instance.totalQuota;
                while ((_instance.quotaChecksMet + 1) * _instance.moneyPerQuotaCheck <= _instance.totalQuota)
                {
                    _instance.quotaChecksMet++;
                    _instance.session.DataStorage["quotaChecksMet"] = _instance.quotaChecksMet;
                    _instance.CompleteLocation($"Quota check {_instance.quotaChecksMet}");
                }
            }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), "FirstEmptyItemSlot")]
    private static void LimitGrabbing(PlayerControllerB __instance, ref int __result)
    {
        if (__result >= _instance.invSlots)
        {
            __result = -1;
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), "NextItemSlot")]
    private static void LimitInventory(PlayerControllerB __instance, ref int __result, ref bool forward)
    {
        if (__result >= _instance.invSlots)
        {
            if (forward)
                __result = 0;
            else
                __result = _instance.invSlots - 1;
        }
    }

    private void Setup(Terminal t)
    {
        var items = t.buyableItemsList;

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (firstTimeSetup && !itemMap.ContainsKey(item.itemName))
                itemMap.Add(item.itemName, new[] { i, item.creditsWorth, 0 });

            item.creditsWorth = 10000000;
        }

        var nouns = t.terminalNodes.allKeywords[26].compatibleNouns;

        for (var i = 0; i < nouns.Length; i++)
        {
            var noun = nouns[i];
            if (noun.result.name == "CompanyMoonroute") continue;

            if (firstTimeSetup && !itemMap.ContainsKey(noun.result.name.Substring(0, noun.result.name.Length - 5)))
                itemMap.Add(noun.result.name.Substring(0, noun.result.name.Length - 5),
                    new[] { i, noun.result.itemCost, 2 });

            noun.result.itemCost = 10000000;
            noun.result.terminalOptions[1].result.itemCost = 10000000;
        }

        nouns = t.terminalNodes.allKeywords[0].compatibleNouns;

        for (var i = 0; i < nouns.Length; i++)
        {
            var noun = nouns[i];
            var ind = Array.IndexOf(new[] { "SignalTranslatorBuy", "InverseTeleporterBuy" },
                noun.result.name);
            if (ind != -1)
            {
                if (firstTimeSetup)
                    if (!itemMap.ContainsKey(noun.result.name.Substring(0, noun.result.name.Length - 3)))
                        itemMap.Add(noun.result.name.Substring(0, noun.result.name.Length - 3),
                            new[] { i, noun.result.itemCost, 1 });

                noun.result.itemCost = 10000000;
            }

            ind = Array.IndexOf(new[] { "TeleporterBuy1", "LoudHornBuy1" },
                noun.result.name);
            if (ind != -1)
            {
                if (firstTimeSetup &&
                    !itemMap.ContainsKey(noun.result.name.Substring(0, noun.result.name.Length - 4)))
                    itemMap.Add(noun.result.name.Substring(0, noun.result.name.Length - 4),
                        new[] { i, noun.result.itemCost, 1 });

                noun.result.itemCost = 10000000;
            }
        }

        //Runs through each item received
        foreach (var item in session.Items.AllItemsReceived)
        {
            //Gets the name
            var itemName = session.Items.GetItemName(item.Item);
            
            //If the item name is a moon, it needs to become the moon's number to apply it to the game 
            if (moonNameMap.ContainsKey(itemName))
            {
                itemName = moonNameMap.Get(itemName);
                if (!collectedMoonMap.ContainsKey(session.Items.GetItemName(item.Item)))
                    collectedMoonMap.Add(session.Items.GetItemName(item.Item), true);
            }

            //If it is in the item map, move to the next step
            if (itemMap.ContainsKey(itemName))
            {
                //Get the data from the item map
                var data = itemMap.Get(itemName);
                //If the type is item
                if (data[2] == 0)
                    //Unlock it
                    items[data[0]].creditsWorth = data[1];

                //If the type is ship upgrade
                if (data[2] == 1)
                    //Unlock it
                    t.terminalNodes.allKeywords[0].compatibleNouns[data[0]].result.itemCost = data[1];

                //If the type is moon
                if (data[2] == 2)
                {
                    //Unlock it
                    t.terminalNodes.allKeywords[26].compatibleNouns[data[0]].result.itemCost = 0;
                    t.terminalNodes.allKeywords[26].compatibleNouns[data[0]].result.terminalOptions[1].result
                        .itemCost = 0;
                }
            }
        }

        firstTimeSetup = false;
        CheckItems();

        var moons = t.terminalNodes.allKeywords[21].specialKeywordResult;
        moons.displayText = $@"Welcome to the exomoons catalogue.
To route the autopilot to a moon, use the word ROUTE.
To learn about any moon, use the word INFO.{(goal == 1 ? $"\nCollectathon progress: {scrapCollected}/{collectathonGoal}" : "")}
____________________________

* The Company building   //   Buying at [companyBuyingPercent].

* Experimentation [planetTime] ({moonChecks[0]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Experimentation") ? Array.IndexOf(trophyModeComplete,"Experimentation")!=-1 ? "Trophy Found!" : "" : "Locked!")}
* Assurance [planetTime] ({moonChecks[1]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Assurance") ? Array.IndexOf(trophyModeComplete,"Assurance")!=-1 ? "Trophy Found!" : "" : "Locked!")}
* Vow [planetTime] ({moonChecks[2]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Vow") ? Array.IndexOf(trophyModeComplete,"Vow")!=-1 ? "Trophy Found!" : "" : "Locked!")}

* Offense [planetTime] ({moonChecks[3]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Offense") ? Array.IndexOf(trophyModeComplete,"Offense")!=-1 ? "Trophy Found!" : "" : "Locked!")}
* March [planetTime] ({moonChecks[4]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("March") ? Array.IndexOf(trophyModeComplete,"March")!=-1 ? "Trophy Found!" : "" : "Locked!")}

* Rend [planetTime] ({moonChecks[5]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Rend") ? Array.IndexOf(trophyModeComplete,"Rend")!=-1 ? "Trophy Found!" : "" : "Locked!")}
* Dine [planetTime] ({moonChecks[6]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Dine") ? Array.IndexOf(trophyModeComplete,"Dine")!=-1 ? "Trophy Found!" : "" : "Locked!")}
* Titan [planetTime] ({moonChecks[7]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Titan") ? Array.IndexOf(trophyModeComplete,"Titan")!=-1 ? "Trophy Found!" : "" : "Locked!")}

";
        var bestiary = t.terminalNodes.allKeywords[16].specialKeywordResult;
        bestiary.displayText = $@"BESTIARY  ({t.scannedEnemyIDs.Count}/{t.enemyFiles.Count - 1})

To access a creature file, type ""INFO"" after its name.
---------------------------------

[currentScannedEnemiesList]


";

        var logs = t.terminalNodes.allKeywords[61].specialKeywordResult;
        logs.displayText = $@"SIGURD'S LOG ENTRIES  ({t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1})

To read a log, use keyword ""VIEW"" before its name.
---------------------------------

[currentUnlockedLogsList]


";
    }

    private void fixStuff()
    {
        if (!successfullyConnected)
        {
            return;
        }
        newItems = new Collection<string>();
        foreach(NetworkItem item in session.Items.AllItemsReceived)
        {
            string itemName = session.Items.GetItemName(item.Item);
            newItems.Add(itemName);
        }
        CheckItems();
    }

    [HarmonyPatch(typeof(Terminal), "Update")]
    [HarmonyPostfix]
    public static void SetCreditCheckUI(Terminal __instance)
    {
        if (!_instance.successfullyConnected) return;
        __instance.topRightText.text =
            $"${__instance.groupCredits}  ({_instance.quotaChecksMet}/{_instance.numQuota})";
    }
}