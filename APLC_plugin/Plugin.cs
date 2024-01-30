using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using BepInEx;
using HarmonyLib;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Timers;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.MessageLog.Parts;
using Archipelago.MultiClient.Net.Packets;
using GameNetcodeStuff;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Collections;
using Color = UnityEngine.Color;
using Object = System.Object;
using Random = System.Random;

namespace APLC
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Lethal Company.exe")]
    public class Plugin : BaseUnityPlugin
    {
        //Death link service for when death link is enabled
        private DeathLinkService dlService;
        
        //Harmony for patching
        private readonly Harmony _harmony = new(PluginInfo.PLUGIN_GUID);

        //Instance of the plugin for other classes to access
        public static Plugin _instance;

        //Maps the item names to a array of three integers
        //  0: The index in the array the item is(for example, 0 for walkie-talkie)
        //  1: The initial price of the item(since we overwrite it we want to store it somewhere so we can replace the insanely high price with this when the item is unlocked
        //  2: The type of item it is. 0 for shop items, 1 for ship upgrades, 2 for moons
        private Dictionary<String, int[]> itemMap = new Dictionary<string, int[]>();

        //Maps the name of the moon to its number because the terminal nodes that refer to moons are named based off of number and not their actual name 
        private Dictionary<String, String> moonNameMap = new Dictionary<String, String>();

        //Some code should only run once, like defining the itemMap, this makes that happen.
        private bool firstTimeSetup = true;

        //You only want to use things if they exist, this makes sure every object we need exists before messing with them
        private bool gameStarted = false;

        //Checks if an item has already been received, if it hasn't we set its index to true and do whatever is required to receive it
        private bool[] received = new bool[100];
        //Checks if a bestiary entry has already been checked so we don't spam the server with useless info.
        private bool[] checkedMonsters = new bool[18];
        //Same as above but for logs
        private bool[] checkedLogs = new bool[13];

        //The archipelago sesion
        private ArchipelagoSession session;
        
        //Sometimes we aren't ready to receive certain items, so we mark them as received and que them by incrementing these variables
        private int moneyItemsWaiting = 0;
        private int hauntItemsWaiting = 0;
        private int brackenItemsWaiting = 0;

        //Useful for when a client disconnects, then rejoins the APworld. We don't want them to receive all of the money items again because that would essentially lead to an infinite money glitch. 
        private int totalMoneyItems = 0;
        private int totalHauntItems = 0;
        private int totalBrackenItems = 0;

        //Bool that checks if the player has successfully connected to the server
        private bool successfullyConnected = false;

        //Checks how many money items were received, if this ever gets higher than totalMoneyItems than 
        private int receivedMoneyItems = 0;
        private int receivedHauntItems = 0;
        private int receivedBrackenItems = 0;

        //The amount of unlocked inventory slots
        private int invSlots = 4;

        //The total received quota so far
        private int totalQuota = 0;

        //The number of quota checks that have been met
        private int quotaChecksMet = 0;

        //These store settings from the yaml file that are stored in the slot.
        private bool deathLink = false;
        private int moneyPerQuotaCheck = 1000;
        private int numQuota = 20;
        private int checksPerMoon = 3;
        private int goal = 0;
        private int minMoney = 100;
        private int maxMoney = 1000;
        private int moonRank = 0;
        private int collectathonGoal = 20;
        private int staminaChecks = 4;
        private bool randomizeScanner = false;
        //private double apparatusChance = 0.1;
        
        //Tracks progress towards the collectathon goal
        private int scrapCollected = 0;

        //The slot name as set from the .cfg
        private string slotName = "";

        //Stores all received items
        private Collection<string> newItems = new Collection<string>();

        //Stores how many times each moon has been completed on a B or higher
        private int[] moonChecks = new[] { 0, 0, 0, 0, 0, 0, 0, 0 };
        
        /// <summary>
        /// Adds spaces before captial letters in a string
        /// </summary>
        /// <param name="text">The string to add spaces to</param>
        /// <returns>The modified string</returns>
        string AddSpacesToSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            StringBuilder newText = new StringBuilder(text.Length * 2);
            newText.Append(text[0]);
            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]))
                    if ((text[i - 1] != ' ' && !char.IsUpper(text[i - 1])))
                        newText.Append(' ');
                newText.Append(text[i]);
            }
            return newText.ToString();
        }

        /// <summary>
        /// Completes an archipelago location
        /// </summary>
        /// <param name="lName">The name of the location to complete</param>
        private void CompleteLocation(string lName)
        {
            long id = _instance.session.Locations.GetLocationIdFromName("Lethal Company", lName);
            _instance.session.Locations.CompleteLocationChecks(id);
        }

        /// <summary>
        /// Handles the reception of items from AP
        /// </summary>
        /// <param name="helper">The helped</param>
        private void ReceivedItem(ReceivedItemsHelper helper)
        {
            newItems.Add(helper.PeekItemName());
            CheckItems();
            helper.DequeueItem();
        }

        private void CheckTraps()
        {
            Logger.LogInfo("Checking Traps");
            if (brackenItemsWaiting > 0)
            {
                if (SpawnEnemyByName("flower"))
                {
                    brackenItemsWaiting--;
                    totalBrackenItems++;
                    session.DataStorage["brackenTrapsReceived"] = totalBrackenItems;
                }
            }

            if (hauntItemsWaiting > 0)
            {
                if (SpawnEnemyByName("dress"))
                {
                    hauntItemsWaiting--;
                    totalHauntItems++;
                    session.DataStorage["hauntTrapsReceived"] = totalHauntItems;
                }
            }
        }
        
        /// <summary>
        /// Probably should split this into two functions, as of right now what it does is it checks if certain locations have been completed and if certain checks have been received, then processes them.
        /// </summary>
        private void CheckItems()
        {
            if (!gameStarted || !successfullyConnected)
            {
                return;
            }
            try
            {
                for (int i = 0; i < newItems.Count; i++)
                {
                    if (newItems[i] == "Inventory Slot" && !received[i])
                    {
                        received[i] = true;
                        invSlots++;
                    }
                    else if (newItems[i] == "Stamina Bar" && !received[i])
                    {
                        received[i] = true;
                        staminaChecks++;
                    }
                    else if (newItems[i] == "Scanner" && !received[i])
                    {
                        received[i] = true;
                        randomizeScanner = false;
                    }
                }

                if (!GameNetworkManager.Instance.isHostingGame)
                {
                    return;
                }
            }
            catch (Exception err)
            {
                Logger.LogError(err.Message);
                Logger.LogError(err.StackTrace);
                return;
            }

            try
            {
                Terminal t = FindObjectOfType<Terminal>();
                Item[] items = t.buyableItemsList;
                
                foreach (int mID in t.scannedEnemyIDs)
                {
                    if (!checkedMonsters[mID])
                    {
                        string eName = t.enemyFiles[mID].name;
                        eName = eName.Substring(0, eName.Length - 4);
                        string formatted;
                        if (eName == "CoilHead")
                        {
                            formatted = "Coil-Head";
                        }
                        else if (eName == "Snareflea")
                        {
                            formatted = "Snare Flea";
                        }
                        else if (eName == "Locust")
                        {
                            formatted = "Roaming Locust";
                        }
                        else if (eName == "Puffer")
                        {
                            formatted = "Spore Lizard";
                        }
                        else
                        {
                            formatted = AddSpacesToSentence(eName);
                        }

                        CompleteLocation($"Bestiary Entry - {formatted}");
                        checkedMonsters[mID] = true;
                    }
                }

                foreach (int mID in t.unlockedStoryLogs)
                {
                    if (!checkedLogs[mID])
                    {

                        string logName = "";

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
                            default:
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
                }

                for (int i = 0; i < newItems.Count; i++)
                {
                    if (!received[i])
                    {
                        received[i] = true;
                        string itemName = newItems[i];
                        if (itemName == "Money")
                        {
                            if (t != null && totalMoneyItems <= receivedMoneyItems)
                            {
                                t.groupCredits += new Random().Next(minMoney, maxMoney);
                            }
                            else
                            {
                                if (totalMoneyItems <= receivedMoneyItems)
                                {
                                    moneyItemsWaiting++;
                                }
                            }

                            if (totalMoneyItems >= receivedMoneyItems)
                            {
                                totalMoneyItems++;
                            }

                            receivedMoneyItems++;
                            session.DataStorage["moneyChecksReceived"] = totalMoneyItems;
                        }

                        if (itemName == "HauntTrap")
                        {
                            receivedHauntItems++;
                            if(receivedHauntItems > totalHauntItems){
                                if (!SpawnEnemyByName("dress"))
                                {
                                    hauntItemsWaiting++;
                                }
                                else
                                {
                                    totalHauntItems++;
                                }
                                session.DataStorage["hauntTrapsReceived"] = totalHauntItems;
                            }
                        }

                        if (itemName == "BrackenTrap")
                        {
                            receivedBrackenItems++;
                            if(receivedBrackenItems > totalBrackenItems){
                                if (!SpawnEnemyByName("flower"))
                                {
                                    brackenItemsWaiting++;
                                }
                                else
                                {
                                    totalBrackenItems++;   
                                }
                                session.DataStorage["brackenTrapsReceived"] = totalBrackenItems;
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Logger.LogError(err.StackTrace);
            }
        }

        /// <summary>
        /// Runs when the game first boots up, connects to AP and sets up all the variables that need setup
        /// </summary>
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            for (int i = 0; i < received.Length; i++)
            {
                received[i] = false;
            }

            for (int i = 0; i < checkedMonsters.Length; i++)
            {
                checkedMonsters[i] = false;
            }
            
            for (int i = 0; i < checkedLogs.Length; i++)
            {
                checkedLogs[i] = false;
            }
            moonNameMap.Add("Experimentation","41");
            moonNameMap.Add("Assurance","220");
            moonNameMap.Add("Vow","56");
            moonNameMap.Add("Offense","21");
            moonNameMap.Add("March","61");
            moonNameMap.Add("Rend","85");
            moonNameMap.Add("Dine","7");
            moonNameMap.Add("Titan","8");
            
            
            _harmony.PatchAll(typeof(Plugin));
            
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private float time = 0f;
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
        }

        private void MessageReceived(LogMessage message)
        {
            string chat = "AP: ";
            foreach (MessagePart part in message.Parts)
            {
                string hexCode = BitConverter.ToString(new[] { part.Color.R, part.Color.G, part.Color.B }).Replace("-", "");
                chat += $"<color=#{hexCode}>{part.Text}</color>";
            }

            switch (message)
            {
                case ChatLogMessage chatLogMessage:
                    if (chatLogMessage.Player.Name == session.Players.GetPlayerAlias(session.ConnectionInfo.Slot))
                    {
                        return;
                    }

                    break;
            }
            HUDManager.Instance.AddTextToChatOnServer(chat);
        }

        private int port = 0;
        private string url = "";
        private string password = "";
        private bool waitingForSlot = false;
        private bool waitingForPassword = false;
        private string lastChatMessagePre = "";
        private string lastChatMessagePost = "";
        
        public static bool SpawnEnemy(SpawnableEnemyWithRarity enemy, int amount, bool inside, PlayerControllerB player)
        {
            try
            {
                if (inside ^ player.isInsideFactory)
                {
                    return false;
                }
                
                GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(enemy.enemyType.enemyPrefab,
                    player.transform.position + new Vector3(0f, 0.5f, 0f), Quaternion.identity);
                gameObject.GetComponentInChildren<NetworkObject>().Spawn(true);
                //gameObject.gameObject.GetComponentInChildren<EnemyAI>().stunNormalizedTimer = 1f;
                return true;
            }
            catch (Exception e)
            {
                _instance.Logger.LogError(e.Message+"\n"+e.StackTrace);
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

            PlayerControllerB[] allPlayers = StartOfRound.Instance.allPlayerScripts;
            int i = UnityEngine.Random.RandomRangeInt(0, allPlayers.Length);
            int startI = i;

            PlayerControllerB spawnPlayer = allPlayers[i];
            while (spawnPlayer.isPlayerDead || !spawnPlayer.isPlayerControlled || spawnPlayer.playerUsername == "Player #0" || spawnPlayer.playerUsername == "Player #1" || spawnPlayer.playerUsername == "Player #2" || spawnPlayer.playerUsername == "Player #3" || spawnPlayer.playerUsername == "Player #4" || spawnPlayer.playerUsername == "Player #5" || spawnPlayer.playerUsername == "Player #6" || spawnPlayer.playerUsername == "Player #7")
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
            
            foreach (SpawnableEnemyWithRarity enemy in StartOfRound.Instance.currentLevel.Enemies)
            {
                if (enemy.enemyType.enemyName.ToLower().Contains(name))
                {
                    _instance.Logger.LogInfo("Attempting spawn");
                    return SpawnEnemy(enemy, 1, true, spawnPlayer);
                }
            }
            foreach (SpawnableEnemyWithRarity enemy in StartOfRound.Instance.currentLevel.OutsideEnemies)
            {
                if (enemy.enemyType.enemyName.ToLower().Contains(name))
                {
                    _instance.Logger.LogInfo("Attempting spawn");
                    return SpawnEnemy(enemy, 1, false, spawnPlayer);
                }
            }
            foreach (SpawnableEnemyWithRarity enemy in StartOfRound.Instance.currentLevel.DaytimeEnemies)
            {
                if (enemy.enemyType.enemyName.ToLower().Contains(name))
                {
                    _instance.Logger.LogInfo("Attempting spawn");
                    return SpawnEnemy(enemy, 1, false, spawnPlayer);
                }
            }
            _instance.Logger.LogInfo("Failed spawn");
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), "UpdateScanNodes")]
        private static bool CancelScan()
        {
            return !_instance.randomizeScanner;
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), "PingScan_performed")]
        private static bool CancelScanAnimation()
        {
            return !_instance.randomizeScanner;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        private static bool Sprint(PlayerControllerB __instance)
        {
            __instance.sprintMeter = Mathf.Min(__instance.sprintMeter, _instance.staminaChecks * 0.25f);
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
        private static bool CheckConnections(ref string chatMessage)
        {
            if (_instance.lastChatMessagePre == chatMessage)
            {
                return false;
            }
            if (_instance.lastChatMessagePost == chatMessage)
            {
                return false;
            }
            
            _instance.lastChatMessagePre = chatMessage;
            string[] tokens = chatMessage.Split(" ");
            if (tokens[0] == "APConnection:" && !GameNetworkManager.Instance.isHostingGame && !_instance.successfullyConnected)
            {
                _instance.url = tokens[1];
                _instance.port = Int32.Parse(tokens[2]);
                _instance.slotName = "";
                for (int i = 3; i < tokens.Length - 1; i++)
                {
                    _instance.slotName += tokens[i];
                    if (i < tokens.Length - 2)
                    {
                        _instance.slotName += " ";
                    }
                }
                _instance.password = tokens[tokens.Length-1];
                _instance.ConnectToAP();
            }
            if (tokens[0] == "RequestAPConnection:" && GameNetworkManager.Instance.isHostingGame && _instance.successfullyConnected)
            {
                HUDManager.Instance.AddTextToChatOnServer($"APConnection: {_instance.url} {_instance.port} {_instance.slotName} {_instance.password}");
            }

            if (tokens[0] != "APConnection:" && tokens[0] != "RequestAPConnection:")
            {
                _instance.Logger.LogWarning("Passed Through Pre: " + chatMessage);
            }

            return tokens[0] != "APConnection:" && tokens[0] != "RequestAPConnection:";
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HUDManager),"AddChatMessage")]
        private static void OnMessageSent(ref string chatMessage)
        {
            string[] tokens = chatMessage.Split(" ");
            if (_instance.lastChatMessagePost == chatMessage)
            {
                return;
            }
            if (tokens[0] == "AP:" || tokens[0] == "APConnection:" || tokens[0] == "RequestAPConnection:")
            {
                return;
            }
            
            _instance.lastChatMessagePost = chatMessage;
            
            if (tokens[0][0] == '/')
            {
                if (tokens[0] == "/connect")
                {
                    string[] parts = tokens[1].Split(":");
                    if (GameNetworkManager.Instance.isHostingGame && !_instance.successfullyConnected && !_instance.waitingForSlot)
                    {
                        _instance.url = parts[0];
                        _instance.port = Int32.Parse(parts[1]);
                        _instance.waitingForSlot = true;
                        HUDManager.Instance.AddTextToChatOnServer("AP: Please enter your slot name:");
                    }
                    else if(!GameNetworkManager.Instance.isHostingGame && !_instance.successfullyConnected)
                    {
                        HUDManager.Instance.AddTextToChatOnServer("RequestAPConnection:");
                    }
                }
                else if(_instance.successfullyConnected)
                {
                    SayPacket msg = new SayPacket(){Text = chatMessage};
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
                        if (chatMessage == "n")
                        {
                            _instance.password = "";
                        }

                        _instance.ConnectToAP();
                    }
                    else if (_instance.successfullyConnected)
                    {
                        SayPacket msg = new SayPacket() { Text = chatMessage };
                        _instance.session.Socket.SendPacket(msg);
                    }
                }
                catch (Exception e)
                {
                    _instance.Logger.LogWarning(e.Message+"\n"+e.StackTrace);
                }
            }
        }

        public void ConnectToAP()
        {
            try
            {
                session = ArchipelagoSessionFactory.CreateSession(url, port);
                if (password == "")
                {
                    password = null;
                }

                LoginResult result =
                    session.TryConnectAndLogin("Lethal Company", slotName, ItemsHandlingFlags.AllItems,
                        new Version(0, 4, 4), new[] { "Death Link" }, password: password);

                if (!result.Successful)
                {
                    LoginFailure failure = (LoginFailure)result;
                    string errorMessage =
                        $"Failed to Connect to {url + ":" + port} as {slotName}:";
                    foreach (string error in failure.Errors)
                    {
                        errorMessage += $"\n    {error}";
                    }

                    foreach (ConnectionRefusedError error in failure.ErrorCodes)
                    {
                        errorMessage += $"\n    {error}";
                    }

                    HUDManager.Instance.AddTextToChatOnServer($"AP: <color=red>{errorMessage}</color>");
                    return;
                }

                LoginSuccessful successful = (LoginSuccessful)result;

                invSlots = Int32.Parse(successful.SlotData["inventorySlots"].ToString());
                staminaChecks = Int32.Parse(successful.SlotData["staminaBars"].ToString());
                randomizeScanner = Int32.Parse(successful.SlotData["scanner"].ToString()) == 1;
                moneyPerQuotaCheck = Int32.Parse(successful.SlotData["moneyPerQuotaCheck"].ToString());
                numQuota = Int32.Parse(successful.SlotData["numQuota"].ToString());
                checksPerMoon = Int32.Parse(successful.SlotData["checksPerMoon"].ToString());
                goal = Int32.Parse(successful.SlotData["goal"].ToString());
                minMoney = Int32.Parse(successful.SlotData["minMoney"].ToString());
                maxMoney = Int32.Parse(successful.SlotData["maxMoney"].ToString());
                moonRank = Int32.Parse(successful.SlotData["moonRank"].ToString());
                collectathonGoal = Int32.Parse(successful.SlotData["collectathonGoal"].ToString());
                deathLink = Int32.Parse(successful.SlotData["deathLink"].ToString()) == 1;
                HUDManager.Instance.AddTextToChatOnServer("AP: <color=green>Successfully connected to archipelago</color>");
                session.MessageLog.OnMessageReceived += MessageReceived;
            }
            
            catch (Exception err)
            {
                successfullyConnected = false;
                HUDManager.Instance.AddTextToChatOnServer("AP: <color=red>Couldn't connect to Archipelago. Are you sure your info is correct?</color>");
                HUDManager.Instance.AddTextToChatOnServer($"AP: <color=red>{err.Message}\n{err.StackTrace}</color>");
                return;
            }

            if (deathLink)
            {
                dlService = session.CreateDeathLinkService();
                dlService.EnableDeathLink();
                dlService.OnDeathLinkReceived += link =>
                {
                    Random rng = new Random((int)link.Timestamp.Ticks);
                    int selected = rng.Next(0, GameNetworkManager.Instance.connectedPlayers);
                    if (GameNetworkManager.Instance.steamIdsInLobby[selected].Value ==
                        GameNetworkManager.Instance.localPlayerController.playerSteamId)
                    {
                        GameNetworkManager.Instance.localPlayerController.health = 0;
                    }
                };
            }
            
            session.DataStorage["moonChecks"].Initialize(new JArray(moonChecks));
            session.DataStorage["totalQuota"].Initialize(totalQuota);
            session.DataStorage["quotaChecksMet"].Initialize(quotaChecksMet);
            session.DataStorage["moneyChecksReceived"].Initialize(totalMoneyItems);
            session.DataStorage["scrapCollected"].Initialize(scrapCollected);
            session.DataStorage["hauntTrapsReceived"].Initialize(totalHauntItems);
            session.DataStorage["brackenTrapsReceived"].Initialize(totalBrackenItems);
            session.DataStorage["trophyScrap"].Initialize(new JObject(trophyModeComplete));

            moonChecks = session.DataStorage["moonChecks"];
            totalQuota = session.DataStorage["totalQuota"];
            quotaChecksMet = session.DataStorage["quotaChecksMet"];
            totalMoneyItems = session.DataStorage["moneyChecksReceived"];
            scrapCollected = session.DataStorage["scrapCollected"];
            totalHauntItems = session.DataStorage["hauntTrapsReceived"];
            totalBrackenItems = session.DataStorage["brackenTrapsReceived"];
            trophyModeComplete = session.DataStorage["trophyScrap"].To<Dictionary<string, bool>>();

            session.Items.ItemReceived += ReceivedItem;

            gameStarted = true;

            if (GameNetworkManager.Instance.isHostingGame)
            {
                if (password == null)
                {
                    password = "";
                }
                HUDManager.Instance.AddTextToChatOnServer($"APConnection: {url} {port} {slotName} {password}");
            }

            successfullyConnected = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), "DisplayGlobalNotification")]
        public static bool CheckIfLogOrBestiary(string displayText)
        {
            if (displayText == "New creature data sent to terminal!" || displayText.Substring(0,19) == "Found journal entry")
            {
                _instance.CheckItems();
            }

            return true;
        }
        
        // static void OnLand()
        // {
        //     GameObject apparatus;
        //     GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);
        //     foreach(GameObject gameObj in objects)
        //     {
        //         if (gameObj.name == "LungApparatus(Clone)")
        //         {
        //             apparatus = gameObj;
        //         }
        //     }
        //
        //     if (_instance.apparatusChance >= UnityEngine.Random.value)
        //     {
        //         int part = UnityEngine.Random.Range(0, 6);
        //         switch (part)
        //         {
        //             case 0:
        //                 apparatus.GetComponent<MeshRenderer>().material.SetColor(new Color(1, 0, 0));
        //                 break;
        //             case 1:
        //                 apparatus.GetComponent<MeshRenderer>().material.SetColor(new Color(0, 1, 0));
        //                 break;
        //             case 2:
        //                 apparatus.GetComponent<MeshRenderer>().material.SetColor(new Color(1, 0.55, 0.55));
        //                 break;
        //             case 3:
        //                 apparatus.GetComponent<MeshRenderer>().material.SetColor(new Color(1, 0.5, 0));
        //                 break;
        //             case 4:
        //                 apparatus.GetComponent<MeshRenderer>().material.SetColor(new Color(0, 0, 1));
        //                 break;
        //             case 5:
        //                 apparatus.GetComponent<MeshRenderer>().material.SetColor(new Color(1, 1, 0));
        //                 break;
        //         }
        //     }
        // }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Terminal), "BeginUsingTerminal")]
        static void TerminalStartPrefix(Terminal __instance)
        {
            if (!_instance.successfullyConnected)
            {
                return;
            }
            _instance.Setup(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HUDManager), "FillEndGameStats")]
        static void GradingPostfix()
        {
            string grade = HUDManager.Instance.statsUIElements.gradeLetter.text;
            int gradeInt = Array.IndexOf(new string[] { "S", "A", "B", "C", "D", "F" }, grade);
            bool dead = StartOfRound.Instance.allPlayersDead;
            _instance.Logger.LogWarning(dead);
            if (dead && _instance.deathLink)
            {
                _instance.dlService.SendDeathLink(new DeathLink(_instance.slotName, "failed the company."));
            }
            _instance.Logger.LogWarning($"Completed planet {StartOfRound.Instance.currentLevel.PlanetName} with grade {grade}");
            if (gradeInt <= _instance.moonRank)
            {
                string moon = StartOfRound.Instance.currentLevel.PlanetName.Split(" ")[1];
                int checkNum = 0;
                string[] moonNames = new[]
                    { "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan" };
                int i = Array.IndexOf(moonNames, moon);
                checkNum = _instance.moonChecks[i];
                if (checkNum < _instance.checksPerMoon)
                {
                    checkNum++;
                    _instance.moonChecks[i]++;
                    _instance.session.DataStorage["moonChecks"] = new JArray(_instance.moonChecks);
                    _instance.CompleteLocation($"{moon} check {checkNum}");
                }
            }

            List<GrabbableObject> list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
                where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                select obj).ToList<GrabbableObject>();
            foreach (GrabbableObject scrap in list)
            {
                if (scrap.name == "ap_chest(Clone)" && _instance.goal == 1)
                {
                    GameObject.Destroy(scrap.gameObject);
                    _instance.scrapCollected++;
                    _instance.session.DataStorage["scrapCollected"] = _instance.scrapCollected;
                }
                else if (_instance.goal == 0)
                {
                    switch (scrap.name)
                    {
                        case "ap_apparatus_experimentation(Clone)":
                            if (!_instance.trophyModeComplete.ContainsKey("Experimentation"))
                            {
                                _instance.trophyModeComplete.Add("Experimentation", true);
                            }
                            break;
                        case "ap_apparatus_assurance(Clone)":
                            if (!_instance.trophyModeComplete.ContainsKey("Assurance"))
                            {
                                _instance.trophyModeComplete.Add("Assurance", true);
                            }
                            break;
                        case "ap_apparatus_vow(Clone)":
                            if (!_instance.trophyModeComplete.ContainsKey("Vow"))
                            {
                                _instance.trophyModeComplete.Add("Vow", true);
                            }
                            break;
                        case "ap_apparatus_offense(Clone)":
                            if (!_instance.trophyModeComplete.ContainsKey("Offense"))
                            {
                                _instance.trophyModeComplete.Add("Offense", true);
                            }
                            break;
                        case "ap_apparatus_march(Clone)":
                            if (!_instance.trophyModeComplete.ContainsKey("March"))
                            {
                                _instance.trophyModeComplete.Add("March", true);
                            }
                            break;
                        case "ap_apparatus_rend(Clone)":
                            if (!_instance.trophyModeComplete.ContainsKey("Rend"))
                            {
                                _instance.trophyModeComplete.Add("Rend", true);
                            }
                            break;
                        case "ap_apparatus_dine(Clone)":
                            if (!_instance.trophyModeComplete.ContainsKey("Dine"))
                            {
                                _instance.trophyModeComplete.Add("Dine", true);
                            }
                            break;
                        case "ap_apparatus_titan(Clone)":
                            if (!_instance.trophyModeComplete.ContainsKey("Titan"))
                            {
                                _instance.trophyModeComplete.Add("Titan", true);
                            }
                            break;
                    }

                    _instance.session.DataStorage["trophyScrap"] = new JObject(_instance.trophyModeComplete);
                }
            }

            if (_instance.scrapCollected >= _instance.collectathonGoal && _instance.goal == 1)
            {
                StatusUpdatePacket victory = new StatusUpdatePacket();
                victory.Status = ArchipelagoClientState.ClientGoal;
                _instance.session.Socket.SendPacket(victory);
            }
            else if (_instance.goal == 0)
            {
                string[] moons = new[] { "Experimentation", "Assurance", "Vow", "Offense", "March", "Rend", "Dine", "Titan"};
                bool win = true;
                foreach (string moon in moons)
                {
                    if (!_instance.trophyModeComplete.ContainsKey(moon))
                    {
                        win = false;
                        break;
                    }
                }

                if (win)
                {
                    StatusUpdatePacket victory = new StatusUpdatePacket();
                    victory.Status = ArchipelagoClientState.ClientGoal;
                    _instance.session.Socket.SendPacket(victory);
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
        static void RoundEndPrefix(StartOfRound __instance)
        {
            if (GameNetworkManager.Instance.isHostingGame && _instance.quotaChecksMet < _instance.numQuota)
            {
                if ((float)(TimeOfDay.Instance.profitQuota - TimeOfDay.Instance.quotaFulfilled) <= 0f)
                {
                    _instance.totalQuota += TimeOfDay.Instance.profitQuota;
                    _instance.session.DataStorage["totalQuota"] = _instance.totalQuota;
                    while ((_instance.quotaChecksMet+1) * _instance.moneyPerQuotaCheck <= _instance.totalQuota)
                    {
                        _instance.quotaChecksMet++;
                        _instance.session.DataStorage["quotaChecksMet"] = _instance.quotaChecksMet;
                        _instance.CompleteLocation($"Quota check {_instance.quotaChecksMet}");
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "NextItemSlot")]
        private static void LimitInventory(PlayerControllerB __instance, ref int __result, ref bool forward)
        {
            if (__result >= _instance.invSlots)
            {
                if (forward)
                {
                    __result = 0;
                }
                else
                {
                    __result = _instance.invSlots - 1;
                }
            }
        }

        private void Setup(Terminal t)
        {
            Item[] items = t.buyableItemsList;

            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                if (firstTimeSetup && !itemMap.ContainsKey(item.itemName))
                {
                    itemMap.Add(item.itemName, new[] { i, item.creditsWorth, 0 });
                }

                item.creditsWorth = 10000000;
            }

            CompatibleNoun[] nouns = t.terminalNodes.allKeywords[26].compatibleNouns;

            for (int i = 0; i < nouns.Length; i++)
            {
                CompatibleNoun noun = nouns[i];
                if (noun.result.name == "CompanyMoonroute")
                {
                    continue;
                }

                if (firstTimeSetup && !itemMap.ContainsKey(noun.result.name.Substring(0, noun.result.name.Length - 5)))
                {
                    itemMap.Add(noun.result.name.Substring(0, noun.result.name.Length - 5),
                        new[] { i, noun.result.itemCost, 2 });
                }

                noun.result.itemCost = 10000000;
                noun.result.terminalOptions[1].result.itemCost = 10000000;
            }

            nouns = t.terminalNodes.allKeywords[0].compatibleNouns;

            for (int i = 0; i < nouns.Length; i++)
            {
                CompatibleNoun noun = nouns[i];
                int ind = Array.IndexOf(new[] { "SignalTranslatorBuy", "InverseTeleporterBuy" },
                    noun.result.name);
                if (ind != -1)
                {
                    if (firstTimeSetup)
                    {
                        if (!itemMap.ContainsKey(noun.result.name.Substring(0, noun.result.name.Length - 3)))
                        {
                            itemMap.Add(noun.result.name.Substring(0, noun.result.name.Length - 3),
                                new[] { i, noun.result.itemCost, 1 });
                        }
                    }

                    noun.result.itemCost = 10000000;
                }

                ind = Array.IndexOf(new[] { "TeleporterBuy1", "LoudHornBuy1" },
                    noun.result.name);
                if (ind != -1)
                {
                    if (firstTimeSetup &&
                        !itemMap.ContainsKey(noun.result.name.Substring(0, noun.result.name.Length - 4)))
                    {
                        itemMap.Add(noun.result.name.Substring(0, noun.result.name.Length - 4),
                            new[] { i, noun.result.itemCost, 1 });
                    }

                    noun.result.itemCost = 10000000;
                }
            }

            if (firstTimeSetup)
            {
                ReadOnlyCollection<NetworkItem> apItems = session.Items.AllItemsReceived;
                foreach (NetworkItem item in apItems)
                {
                    newItems.Add(session.Items.GetItemName(item.Item));
                }

                CheckItems();
            }

            //Runs through each item received
            foreach (NetworkItem item in session.Items.AllItemsReceived)
            {
                //Gets the name
                string itemName = session.Items.GetItemName(item.Item);
                //If the item name is a moon, it needs to become the moon's number to apply it to the game 
                if (moonNameMap.ContainsKey(itemName))
                {
                    itemName = moonNameMap.Get(itemName);
                    if (!collectedMoonMap.ContainsKey(session.Items.GetItemName(item.Item)))
                    {
                        collectedMoonMap.Add(session.Items.GetItemName(item.Item), true);
                    }
                }

                //If it is in the item map, move to the next step
                if (itemMap.ContainsKey(itemName))
                {
                    //Get the data from the item map
                    int[] data = itemMap.Get(itemName);
                    //If the type is item
                    if (data[2] == 0)
                    {
                        //Unlock it
                        items[data[0]].creditsWorth = data[1];
                    }

                    //If the type is ship upgrade
                    if (data[2] == 1)
                    {
                        //Unlock it
                        t.terminalNodes.allKeywords[0].compatibleNouns[data[0]].result.itemCost = data[1];
                    }

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

            TerminalNode moons = t.terminalNodes.allKeywords[21].specialKeywordResult;
            moons.displayText = $@"Welcome to the exomoons catalogue.
To route the autopilot to a moon, use the word ROUTE.
To learn about any moon, use the word INFO.{(goal == 1 ? $"\nCollectathon progress: {scrapCollected}/{collectathonGoal}" : "")}
____________________________

* The Company building   //   Buying at [companyBuyingPercent].

* Experimentation [planetTime] ({moonChecks[0]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Experimentation") ? (trophyModeComplete.ContainsKey("Experimentation") ? "Trophy Found!" : "") : "Locked!")}
* Assurance [planetTime] ({moonChecks[1]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Assurance") ? (trophyModeComplete.ContainsKey("Assurance") ? "Trophy Found!" : "") : "Locked!")}
* Vow [planetTime] ({moonChecks[2]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Vow") ? (trophyModeComplete.ContainsKey("Vow") ? "Trophy Found!" : "") : "Locked!")}

* Offense [planetTime] ({moonChecks[3]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Offense") ? (trophyModeComplete.ContainsKey("Offense") ? "Trophy Found!" : "") : "Locked!")}
* March [planetTime] ({moonChecks[4]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("March") ? (trophyModeComplete.ContainsKey("March") ? "Trophy Found!" : "") : "Locked!")}

* Rend [planetTime] ({moonChecks[5]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Rend") ? (trophyModeComplete.ContainsKey("Rend") ? "Trophy Found!" : "") : "Locked!")}
* Dine [planetTime] ({moonChecks[6]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Dine") ? (trophyModeComplete.ContainsKey("Dine") ? "Trophy Found!" : "") : "Locked!")}
* Titan [planetTime] ({moonChecks[7]}/{checksPerMoon}) {(collectedMoonMap.ContainsKey("Titan") ? (trophyModeComplete.ContainsKey("Titan") ? "Trophy Found!" : "") : "Locked!")}

";
            TerminalNode bestiary = t.terminalNodes.allKeywords[16].specialKeywordResult;
            bestiary.displayText = $@"BESTIARY  ({t.scannedEnemyIDs.Count}/{t.enemyFiles.Count-1})

To access a creature file, type ""INFO"" after its name.
---------------------------------

[currentScannedEnemiesList]


";

            TerminalNode logs = t.terminalNodes.allKeywords[61].specialKeywordResult;
            logs.displayText = $@"SIGURD'S LOG ENTRIES  ({t.unlockedStoryLogs.Count - 1}/{t.logEntryFiles.Count - 1})

To read a log, use keyword ""VIEW"" before its name.
---------------------------------

[currentUnlockedLogsList]


";
        }

        [HarmonyPatch(typeof(Terminal), "Update")]
        [HarmonyPostfix]
        public static void SetCreditCheckUI(Terminal __instance)
        {
            if (!_instance.successfullyConnected)
            {
                return;
            }
            __instance.topRightText.text =
                $"${__instance.groupCredits}  ({_instance.quotaChecksMet}/{_instance.numQuota})";
        }

        private Dictionary<string, bool> collectedMoonMap = new Dictionary<string, bool>();
        private Dictionary<string, bool> trophyModeComplete = new Dictionary<string, bool>();
    }
}
