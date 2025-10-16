using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLevelLoader;
using UnityEngine;
using Object = UnityEngine.Object;
using Unity.Netcode;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace APLC;

public class Patches
{
    private static readonly Harmony Harmony = new(PluginInfo.PLUGIN_GUID);
    private static float _time;
    private static float _time1Sec;
    private static bool _waitingForTerminalQuit;

    /**
     * Patches the game with all the patches in this file.
     */
    public static void Patch()
    {
        Harmony.PatchAll(typeof(Patches));
    }

    //Player upgrade managing
    /**
     * Cancels the scan functionality when the scanner isn't unlocked
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "UpdateScanNodes")]
    private static bool CancelScan()
    {
        if (MultiworldHandler.Instance == null) return true;
        return ((PlayerUpgrades)MwState.Instance.GetItemMap("Scanner")).GetNum() >= 1;
    }

    /**
     * Cancels the scan animation when the scanner isn't unlocked
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "PingScan_performed")]
    private static bool CancelScanAnimation()
    {
        if (MultiworldHandler.Instance == null) return true;
        return ((PlayerUpgrades)MwState.Instance.GetItemMap("Scanner")).GetNum() >= 1;
    }

    /**
     *  Limits the stamina of the player when stamina is shuffled
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    private static bool Sprint(PlayerControllerB __instance)
    {
        if (MultiworldHandler.Instance == null) return true;
        int staminaChecks = ((PlayerUpgrades)MwState.Instance.GetItemMap("Stamina Bar")).GetNum();
        if (staminaChecks == 1)
        {
            __instance.sprintMeter = Mathf.Min(__instance.sprintMeter, 0.35f);
            return true;
        }

        __instance.sprintMeter = Mathf.Min(__instance.sprintMeter, staminaChecks * 0.25f);
        return true;
    }

    /**
     * Limits grabbing when your inventory is full, stops you from getting things in an unreachable spot in your inventory
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), "FirstEmptyItemSlot")]
    private static void LimitGrabbing(PlayerControllerB __instance, ref int __result)
    {
        if (MultiworldHandler.Instance == null) return;
        if (__result >= ((PlayerUpgrades)MwState.Instance.GetItemMap("Inventory Slot")).GetNum())
        {
            __result = -1;
        }
    }

    /**
     * Stops you from scrolling past your unlocked inventory slots
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), "NextItemSlot")]
    private static void LimitInventory(PlayerControllerB __instance, ref int __result, ref bool forward)
    {

        if (MultiworldHandler.Instance == null) return;
        int invSlots = ((PlayerUpgrades)MwState.Instance.GetItemMap("Inventory Slot")).GetNum();
        if (__result >= invSlots)
        {
            if (forward)
                __result = 0;
            else
                __result = invSlots - 1;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    private static void FixCarryWeight(PlayerControllerB __instance)
    {
        if (MultiworldHandler.Instance == null) return;
        var newWeight = 1f + __instance.ItemSlots.Where(item => item != null).Sum(
            item => Mathf.Clamp(
                (item.itemProperties.weight - 1f) * Mathf.Pow(
                    0.90f, MwState.Instance.GetItemMap<PlayerUpgrades>("Strength Training").GetNum()
                ),
                0f,
                10f
            )
        );

        __instance.carryWeight = Mathf.Max(1f, newWeight);
    }
    
    // Token: 0x060001F4 RID: 500 RVA: 0x00007030 File Offset: 0x00005230
    [HarmonyPriority(200)]
    [HarmonyPatch(typeof(GameNetworkManager), "Start")]
    [HarmonyPrefix]
    internal static void GameNetworkManagerStart_Prefix(GameNetworkManager __instance)
    {
        Plugin.Instance.LogInfo("Attempting to create APLC Network Manager");
        
        GameObject networkManagerPrefab = PrefabHelper.CreateNetworkPrefab("APLCNetworkManager");
        networkManagerPrefab.AddComponent<APLCNetworking>();
        networkManagerPrefab.GetComponent<NetworkObject>().SceneMigrationSynchronization = true;
        networkManagerPrefab.GetComponent<NetworkObject>().DestroyWithScene = false;
        Object.DontDestroyOnLoad(networkManagerPrefab);
        
        APLCNetworking.NetworkingManagerPrefab = networkManagerPrefab;
        __instance.GetComponent<NetworkManager>().AddNetworkPrefab(networkManagerPrefab);
        
    }

    [HarmonyPriority(200)]
    [HarmonyPatch(typeof(StartOfRound), "Awake")]
    [HarmonyPrefix]
    internal static void StartOfRoundAwake_Prefix(StartOfRound __instance)
    {
        if (GameNetworkManager.Instance.GetComponent<NetworkManager>().IsServer)
        {
            Object.Instantiate(APLCNetworking.NetworkingManagerPrefab).GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
        }
    }
    
    //Archipelago connection
    /**
     * Ticks all waiting items and refreshes the unlocked items
     */
    [HarmonyPatch(typeof(StartOfRound), "Update")]
    [HarmonyPostfix]
    private static void Tick()
    {
        if (MultiworldHandler.Instance == null) return;
        _time += Time.deltaTime;
        _time1Sec += Time.deltaTime;
        while (_time >= 5f)
        {
            _time -= 5f;
            MultiworldHandler.Instance.Tick(new AplcEventArgs(MultiworldHandler.Instance.GetReceivedItems()));
        }

        while (_time1Sec >= 1f)
        {
            _time1Sec -= 1f;
            if (_waitingForTerminalQuit && StartOfRound.Instance.localPlayerController.inTerminalMenu)
            {
                AccessTools.Method(typeof(Terminal), "QuitTerminal", [typeof(bool)])
                    .Invoke(Plugin.Instance.GetTerminal(), [true]);
            }

            _waitingForTerminalQuit = false;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), "Start")]
    public static void GetArchiInfoFromFile(StartOfRound __instance)
    {
        if (__instance.IsServer)
        {
            if (ES3.KeyExists("ArchipelagoURL", GameNetworkManager.Instance.currentSaveFileName) &&
                MultiworldHandler.Instance == null)
            {
                string url = ES3.Load<string>("ArchipelagoURL", GameNetworkManager.Instance.currentSaveFileName);
                int port = ES3.Load<int>("ArchipelagoPort", GameNetworkManager.Instance.currentSaveFileName);
                string slot = ES3.Load<string>("ArchipelagoSlot", GameNetworkManager.Instance.currentSaveFileName);
                string password =
                    ES3.Load<string>("ArchipelagoPassword", GameNetworkManager.Instance.currentSaveFileName);
                ChatHandler.SetConnectionInfo(url, port, slot, password);
                ConnectionInfo info = new ConnectionInfo(url, port, slot, password);
                MwState state = new MwState(info);
                APLCNetworking.Instance.SendConnection(info);
            }
        }
        else
        {
            APLCNetworking.Instance.RequestConnectionServerRpc();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), "Start")]
    public static void FindEnemiesForTraps(StartOfRound __instance)
    {
        if (__instance.IsServer)
        {
            EnemyTrapHandler.SetupEnemyTrapHandler();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MenuManager), "Update")]
    public static void DisconnectIfInMenu()
    {
        if (MultiworldHandler.Instance != null && StartOfRound.Instance == null)
        {
            MultiworldHandler.Instance.Disconnect();
        }
    }

/**
 * Handles log and bestiary scanning and sends the check to the server
 */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "DisplayGlobalNotification")]
    public static bool CheckIfLogOrBestiary(string displayText)
    {
        if (MultiworldHandler.Instance == null) return true;
        if (displayText == "New creature data sent to terminal!" ||
            displayText.Substring(0, 19) == "Found journal entry") MwState.Instance.CheckLogs();

        return true;
    }
    
    /**
     * Handles the trackers displaying on the terminal
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Terminal), "BeginUsingTerminal")]
    private static void TerminalStartPrefix(Terminal __instance)
    {
        if (MultiworldHandler.Instance == null) return;
        TerminalHandler.DisplayMoonTracker(__instance);
        TerminalHandler.DisplayLogTracker(__instance);
        TerminalHandler.DisplayBestiaryTracker(__instance);
        TerminalHandler.DisplayModifiedShop(__instance);
        if (MultiworldHandler.Instance.GetSlotSetting("randomizeterminal") == 1)
        {
            if (MwState.Instance.GetItemMap<PlayerUpgrades>("Terminal").GetNum() == 0)
            {
                _waitingForTerminalQuit = true;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
    private static void ModifyItemPricesToShowAsLocked(ref string modifiedDisplayText, TerminalNode node, Terminal __instance)
    {
        if (MultiworldHandler.Instance == null) return;
        if (modifiedDisplayText.Contains("[buyableItemsList]"))
        {
            if (__instance.buyableItemsList == null || __instance.buyableItemsList.Length == 0)
            {
                modifiedDisplayText = modifiedDisplayText.Replace("[buyableItemsList]", "[No items in stock!]");
            }
            else
            {
                StringBuilder storeItemStringBuilder = new StringBuilder();
                for (int j = 0; j < __instance.buyableItemsList.Length; j++)
                {
                    try
                    {
                        if (GameNetworkManager.Instance.isDemo && __instance.buyableItemsList[j].lockedInDemo)
                        {
                            storeItemStringBuilder.Append("\n* " + __instance.buyableItemsList[j].itemName + " (Locked)");
                        }
                        else
                        {
                            if (MwState.Instance
                                    .GetItemMap<StoreItems>(__instance.buyableItemsList[j].itemName)
                                    .GetTotal() >= 1)
                            {
                                storeItemStringBuilder.Append("\n* " + __instance.buyableItemsList[j].itemName +
                                                      "  //  Price: $" +
                                                      (__instance.buyableItemsList[j].creditsWorth *
                                                       (__instance.itemSalesPercentages[j] / 100f)));

                            }
                            else
                            {
                                storeItemStringBuilder.Append(
                                    "\n* " + __instance.buyableItemsList[j].itemName + "  //  Locked!");
                            }
                        }

                        if (__instance.itemSalesPercentages[j] != 100 && MwState.Instance
                                .GetItemMap<StoreItems>(__instance.buyableItemsList[j].itemName).GetTotal() >= 1)
                        {
                            storeItemStringBuilder.Append(string.Format("   ({0}% OFF!)",
                                100 - __instance.itemSalesPercentages[j]));
                        }
                    }
                    catch (Exception)
                    {
                        storeItemStringBuilder.Append(string.Format("\n* " + __instance.buyableItemsList[j].itemName +
                                                            "  //  Price: $" +
                                                            __instance.buyableItemsList[j].creditsWorth *
                                                             (__instance.itemSalesPercentages[j] / 100f)));
                        if (__instance.itemSalesPercentages[j] != 100)
                        {
                            storeItemStringBuilder.Append(string.Format("   ({0}% OFF!)",
                                100 - __instance.itemSalesPercentages[j]));
                        }
                    }
                }
                modifiedDisplayText = modifiedDisplayText.Replace("[buyableItemsList]", storeItemStringBuilder.ToString());
            }
        }
        if (modifiedDisplayText.Contains("[buyableVehiclesList]"))
        {
            if (__instance.buyableVehicles == null || __instance.buyableVehicles.Length == 0)
            {
                modifiedDisplayText = modifiedDisplayText.Replace("[buyableVehiclesList]", "[No vehicles in stock!]");
            }
            else
            {
                StringBuilder storeVehicleStringBuilder = new StringBuilder();
                for (int j = 0; j < __instance.buyableVehicles.Length; j++)
                {
                    try
                    {
                        // buyableVehicles don't have a lockedInDemo field
                        if (MwState.Instance
                                .GetItemMap<StoreItems>(__instance.buyableVehicles[j].vehicleDisplayName)
                                .GetTotal() >= 1)
                        {
                            storeVehicleStringBuilder.Append("\n* " + __instance.buyableVehicles[j].vehicleDisplayName +
                                                  "  //  Price: $" +
                                                  __instance.buyableVehicles[j].creditsWorth);

                        }
                        else
                        {
                            storeVehicleStringBuilder.Append(
                                "\n* " + __instance.buyableVehicles[j].vehicleDisplayName + "  //  Locked!");
                        }
                        // vehicles don't seem to be able to go on sale
                    }
                    catch (Exception)
                    {
                        storeVehicleStringBuilder.Append(string.Format("\n* " + __instance.buyableVehicles[j].vehicleDisplayName +
                                                            "  //  Price: $" +
                                                            __instance.buyableVehicles[j].creditsWorth));
                        // vehicles don't seem to be able to go on sale
                    }
                }
                modifiedDisplayText = modifiedDisplayText.Replace("[buyableVehiclesList]", storeVehicleStringBuilder.ToString());
            }
        }
    }
    
    /**
     * Handles the money tracker(has to update every frame since the money display itself is updated every frame)
     */
    [HarmonyPatch(typeof(Terminal), "Update")]
    [HarmonyPostfix]
    public static void SetCreditCheckUI(Terminal __instance)
    {
        if (MultiworldHandler.Instance == null) return;
        TerminalHandler.DisplayMoneyTracker(__instance);
    }
    
    /**
     * Handles the getting of quota checks
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
    private static void RoundEndPrefix(StartOfRound __instance)
    {
        if (MultiworldHandler.Instance == null) return;
        MwState.Instance.GetLocationMap("Quota").CheckComplete();
    }
    
    /**
     * Checks deathlink, checks for moon checks, and checks for victory
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), "FillEndGameStats")]
    private static void GradingPostfix()
    {
        if (MultiworldHandler.Instance == null) return;
        var grade = HUDManager.Instance.statsUIElements.gradeLetter.text;
        var dead = StartOfRound.Instance.allPlayersDead;
        if (dead && !MwState.Instance.IgnoreDL) MultiworldHandler.Instance.HandleDeathLink();
        if (dead) MwState.Instance.IgnoreDL = false;
        
        ((MoonLocations)MwState.Instance.GetLocationMap(StartOfRound.Instance.currentLevel.PlanetName)).OnFinishMoon(StartOfRound.Instance.currentLevel.PlanetName, grade);

        if (MultiworldHandler.Instance.GetSlotSetting("scrapsanity") == 1)
        {
            MwState.Instance.GetLocationMap("Scrap").CheckComplete();
        }

        var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
            where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
            select obj).ToList();
        foreach (var scrap in list)
        {
            if (scrap.name == "ap_chest(Clone)" && !scrap.scrapPersistedThroughRounds && MwState.Instance.GetGoal() == 1)
            {
                MwState.Instance.AddCollectathonScrap(1);
            }
            else if (MwState.Instance.GetGoal() == 0)
            {
                if (scrap.name.Contains("ap_apparatus_"))
                {
                    string[] landing = new string[scrap.name.Split("_").Length-2];
                    Array.ConstrainedCopy(scrap.name.Split("_"), 2, landing, 0, scrap.name.Split("_").Length - 2);
                    MwState.Instance.CompleteTrophy(String.Join(" ", landing).Split("(Clone)")[0].ToLower(), scrap);
                }
            }
        }
    }

    //Misc
    /**
     * Fixes the double message bug 
     */
    /*[HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    private static bool CheckConnections(ref string chatMessage)    // it seems like v70 added this fix, commenting out for now
    {
        var fail = ChatHandler.PreventMultisendBug(chatMessage);

        return fail;
    }*/

    /**
     * Handles the sending of commands and archipelago chat(including commands like !hint and !help)
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    private static void OnMessageSent(ref string chatMessage, string nameOfUserWhoTyped)
    {
        if (nameOfUserWhoTyped != GameNetworkManager.Instance.localPlayerController.playerUsername) 
            return;
        var used = ChatHandler.HandleCommands(chatMessage, nameOfUserWhoTyped);
        if (!ChatHandler.IsChatMessage(chatMessage) || used) return;
        if (!Config.SendChatMessagesAsAPChat) return;
        var packet = new SayPacket()
        {
            Text = chatMessage
        };
        MultiworldHandler.Instance.GetSession().Socket.SendPacket(packet);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "SubmitChat_performed")]
    private static bool SubmitChat_preformed_override(ref InputAction.CallbackContext context, HUDManager __instance)
    {
        __instance.localPlayer = GameNetworkManager.Instance.localPlayerController;
        if (!context.performed)
        {
            return false;
        }
        if (__instance.localPlayer == null || !__instance.localPlayer.isTypingChat)
        {
            return false;
        }
        if ((!__instance.localPlayer.IsOwner || (__instance.IsServer && !__instance.localPlayer.isHostPlayerObject)) && !__instance.localPlayer.isTestingPlayer)
        {
            return false;
        }
        if (__instance.localPlayer.isPlayerDead)
        {
            return false;
        }
        if (!string.IsNullOrEmpty(__instance.chatTextField.text) && __instance.chatTextField.text.Length <= Config.MaxCharactersPerChatMessage)
        {
            __instance.AddTextToChatOnServer(__instance.chatTextField.text, (int)__instance.localPlayer.playerClientId);
        }
        for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
        {
            if (StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled && Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position) > 24.4f && (!GameNetworkManager.Instance.localPlayerController.holdingWalkieTalkie || !StartOfRound.Instance.allPlayerScripts[i].holdingWalkieTalkie))
            {
                __instance.playerCouldRecieveTextChatAnimator.SetTrigger("ping");
                break;
            }
        }
        __instance.localPlayer.isTypingChat = false;
        __instance.chatTextField.text = "";
        EventSystem.current.SetSelectedGameObject(null);
        __instance.PingHUDElement(__instance.Chat, 2f, 1f, 0.2f);
        __instance.typingIndicator.enabled = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "AddPlayerChatMessageServerRpc")]
    private static bool ChatLengthOverridePart2(ref string chatMessage, ref int playerId, HUDManager __instance)
    {
        NetworkManager networkManager = __instance.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return false;
        if (!networkManager.IsServer && !networkManager.IsHost || chatMessage.Length > Config.MaxCharactersPerChatMessage)
            return false;
        
        MethodInfo methodInfo = typeof(HUDManager).GetMethod("AddPlayerChatMessageClientRpc", BindingFlags.Instance | BindingFlags.NonPublic);

        var parameters = new object[] { chatMessage, playerId };
        
        methodInfo?.Invoke(__instance, parameters);
        
        return false;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Terminal), "LoadNewNode")]
    private static bool PreventBuyingLockedItems(TerminalNode node){
        if (MultiworldHandler.Instance == null || !MultiworldHandler.Instance.IsConnected())
        {
            return true;
        }
        Terminal terminal = Plugin.Instance.GetTerminal();
        if (node.buyItemIndex != -1)
        {
            if (node.buyItemIndex != -7)
            {
                Item item = terminal.buyableItemsList[node.buyItemIndex];
                if (MwState.Instance.GetItemMap<StoreItems>(item.itemName).GetTotal() < 1)
                {
                    terminal.LoadNewNode(terminal.currentNode);
                    return false;
                }
            }
        }

        if (node.buyVehicleIndex != -1)
        {
            BuyableVehicle vehicle = terminal.buyableVehicles[node.buyVehicleIndex];
            if (MwState.Instance.GetItemMap<StoreItems>(vehicle.vehicleDisplayName).GetTotal() < 1)
            {
                terminal.LoadNewNode(terminal.currentNode);
                return false;
            }
        }

        if (node.shipUnlockableID != -1)
        {
            if (node.shipUnlockableID < StartOfRound.Instance.unlockablesList.unlockables.Count)
            {
                try
                {
                    if (MwState.Instance.GetItemMap<ShipUpgrades>(StartOfRound.Instance.unlockablesList
                            .unlockables[node.shipUnlockableID].unlockableName).GetTotal() < 1)
                    {
                        terminal.LoadNewNode(terminal.currentNode);
                        return false;
                    }
                }
                catch (Exception)
                {
                    //Ignore, means that we collided with a cosmetic item which we don't randomize(yet)
                }
            }
        }

        if (node.buyRerouteToMoon != -1 && node.buyRerouteToMoon != -2)     // for Gordion, this will only trigger for the reroute confirmation node, but by then the player has already been routed
        {
            SelectableLevel level = StartOfRound.Instance.levels[node.buyRerouteToMoon];
            
            string moonName = level.PlanetName;
            if (moonName != null)
            {
                if (moonName.Contains("Liquidation")) return true;
                if (moonName != "71 Gordion" || MultiworldHandler.Instance.GetSlotSetting("randomizecompany") == 1)
                {
                    if (MwState.Instance.GetItemMap<MoonItems>(moonName).GetTotal() < 1)
                    {
                        Plugin.Instance.LogInfo($"{level.PlanetName} is locked. Blocking reroute.");
                        terminal.LoadNewNode(terminal.currentNode);
                        return false;
                    }
                }
            }
        }
        else if (node.buyRerouteToMoon == -2)
        {
            SelectableLevel level = StartOfRound.Instance.levels[node.terminalOptions[1].result.buyRerouteToMoon];
            string moonName = level.PlanetName;
            if (moonName.Contains("Liquidation")) return true;

            if (moonName != "71 Gordion" || MultiworldHandler.Instance.GetSlotSetting("randomizecompany") == 1) // this condition will always be true because buyRerouteToMoon is -1 or 3 for gordion
            {
                if (MwState.Instance.GetItemMap<MoonItems>(moonName).GetTotal() < 1)
                {
                    Plugin.Instance.LogInfo($"{level.PlanetName} is locked. Blocking reroute.");
                    terminal.LoadNewNode(terminal.currentNode);
                    return false;
                }
            }
        }
        else if (node.buyRerouteToMoon == -1 && node.terminalOptions.Length > 1 && node.terminalOptions[1] != null && node.terminalOptions[1].result != null)
        {
            int nextNodeMoonIndex = node.terminalOptions[1].result.buyRerouteToMoon;
            if (nextNodeMoonIndex > -1)
            {
                string moonName = StartOfRound.Instance.levels[nextNodeMoonIndex].PlanetName;
                if (moonName == "71 Gordion" && MultiworldHandler.Instance.GetSlotSetting("randomizecompany") == 1 && MwState.Instance.GetItemMap<MoonItems>(moonName).GetTotal() < 1)
                {
                    Plugin.Instance.LogInfo("Company building is locked. Blocking reroute.");
                    terminal.LoadNewNode(terminal.currentNode);
                    return false;
                }
            }
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
    private static void ChangeNamesOfCustomTrophies(GrabbableObject __instance)
    {
        
        if (!__instance.isInShipRoom && __instance.name.Contains("ap_apparatus_custom"))
        {
            string planetName = StartOfRound.Instance.currentLevel.PlanetName;
            __instance.GetComponentInChildren<ScanNodeProperties>().headerText = $"AP Apparatus - {(int.TryParse(planetName.Split(" ", 2)[0],out _) ? planetName.Split(" ", 2)[1] : planetName)}";
        }
    }
}