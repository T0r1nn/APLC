using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

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
    private static void Sprint(PlayerControllerB __instance)
    {
        if (MultiworldHandler.Instance == null) return;
        int staminaChecks = ((PlayerUpgrades)MwState.Instance.GetItemMap("Stamina Bar")).GetNum();
        if (staminaChecks == 1)
        {
            __instance.sprintMeter = Mathf.Min(__instance.sprintMeter, 0.35f);
            return;
        }

        __instance.sprintMeter = Mathf.Min(__instance.sprintMeter, staminaChecks * 0.25f);
        return;
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

    /**
     * Adjusts carry weight based on strength training upgrades
     */
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

    /**
     * Creates and adds the APLC Network Manager prefab to the game
     */
    // Token: 0x060001F4 RID: 500 RVA: 0x00007030 File Offset: 0x00005230
    [HarmonyPriority(200)]
    [HarmonyPatch(typeof(GameNetworkManager), "Start")]
    [HarmonyPrefix]
    internal static void GameNetworkManagerStart_Prefix(GameNetworkManager __instance)
    {
        Plugin.Instance.LogInfo("Attempting to create APLC Network Manager");

        // Doing this instead of using PrefabHelper removes our need to use LethalLevelLoader as a dependency
        GameObject networkManagerPrefab = new GameObject("APLCNetworkManager");
        networkManagerPrefab.hideFlags = HideFlags.HideAndDontSave;
        var networkObject = networkManagerPrefab.AddComponent<NetworkObject>();
        var fieldInfo = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
        fieldInfo!.SetValue(networkObject, PluginInfo.PLUGIN_GUID?.Aggregate(17u, (current, c) => unchecked((current * 31) ^ c)) ?? 0u);

        //GameObject networkManagerPrefab = LethalLevelLoader.PrefabHelper.CreateNetworkPrefab("APLCNetworkManager");
        networkManagerPrefab.AddComponent<APLCNetworking>();
        networkManagerPrefab.GetComponent<NetworkObject>().SceneMigrationSynchronization = true;
        networkManagerPrefab.GetComponent<NetworkObject>().DestroyWithScene = false;
        Object.DontDestroyOnLoad(networkManagerPrefab);

        APLCNetworking.NetworkingManagerPrefab = networkManagerPrefab;
        __instance.GetComponent<NetworkManager>().AddNetworkPrefab(networkManagerPrefab);

    }

    /**
     * Spawns the APLC Network Manager on the server
     */
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
    [HarmonyPatch(typeof(StartOfRound), "Start")]
    public static void SetStartingMoon(StartOfRound __instance)
    {
        if (MultiworldHandler.Instance == null || !ES3.KeyExists("APStartingMoon", GameNetworkManager.Instance.currentSaveFileName)) return;
        SelectableLevel startingMoon = StartOfRound.Instance.levels.FirstOrDefault(l => l.PlanetName.ToLower().Contains(ES3.Load<string>("APStartingMoon", GameNetworkManager.Instance.currentSaveFileName).ToLower()));
        if (startingMoon != null)
        {
            StartOfRound.Instance.defaultPlanet = startingMoon.levelID;
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
    public static void CheckIfLogOrBestiary(string displayText)
    {
        if (MultiworldHandler.Instance == null) return;
        if (displayText == "New creature data sent to terminal!" ||
            displayText.Substring(0, 19) == "Found journal entry") MwState.Instance.CheckLogs();

        return;
    }

    /**
     * Handles the trackers displaying on the terminal
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Terminal), "BeginUsingTerminal")]
    private static void TerminalStartPrefix(Terminal __instance)
    {
        if (MultiworldHandler.Instance == null) return;
        TerminalHandler.DisplayLogTracker(__instance);
        TerminalHandler.DisplayBestiaryTracker(__instance);
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
        if (!Plugin.IsDawnLibInstalled && modifiedDisplayText.Contains("[buyableItemsList]"))   // remove this if we decide to completely switch to DawnLib
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
                                storeItemStringBuilder.Append("\n* " + __instance.buyableItemsList[j].itemName +
                                                      " (Locked)  //  Price: $" +
                                                      (__instance.buyableItemsList[j].creditsWorth *
                                                       (__instance.itemSalesPercentages[j] / 100f)));
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
                            storeVehicleStringBuilder.Append("\n* " + __instance.buyableVehicles[j].vehicleDisplayName +
                                                  " (Locked)  //  Price: $" +
                                                  __instance.buyableVehicles[j].creditsWorth);
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
        // this won't be needed if DawnLib makes the name overrides work for vanilla upgrades
        if (!Plugin.IsDawnLibInstalled && modifiedDisplayText.Contains("SHIP UPGRADES"))
        {
            if (MwState.Instance.GetItemMap<ShipUpgrades>("Loud horn").GetTotal() < 1)
                modifiedDisplayText = modifiedDisplayText.Insert(modifiedDisplayText.IndexOf("Loud horn") + "Loud horn".Length, " (Locked)");
            if (MwState.Instance.GetItemMap<ShipUpgrades>("Signal translator").GetTotal() < 1)
                modifiedDisplayText = modifiedDisplayText.Insert(modifiedDisplayText.IndexOf("Signal Translator") + "Signal Translator".Length, " (Locked)");
            if (MwState.Instance.GetItemMap<ShipUpgrades>("Teleporter").GetTotal() < 1)
                modifiedDisplayText = modifiedDisplayText.Insert(modifiedDisplayText.IndexOf("Teleporter") + "Teleporter".Length, " (Locked)");
            if (MwState.Instance.GetItemMap<ShipUpgrades>("Inverse Teleporter").GetTotal() < 1)
                modifiedDisplayText = modifiedDisplayText.Insert(modifiedDisplayText.IndexOf("Inverse Teleporter") + "Inverse Teleporter".Length, " (Locked)");
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

        GameObject cruiser = GameObject.FindObjectsByType<VehicleController>(sortMode: FindObjectsSortMode.None).FirstOrDefault(vehicle => vehicle.magnetedToShip)?.gameObject;
        bool hasCruiser = cruiser != null;

        var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
                    where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                    select obj).Union(hasCruiser ? (from obj in cruiser.GetComponentsInChildren<GrabbableObject>()
                                                    where obj.name != "CompanyCruiserManual(Clone)"
                                                    select obj) : []).ToList();
        int apchestCount = 0;
        foreach (var scrap in list)
        {
            if (scrap.name == "ap_chest(Clone)" && !scrap.scrapPersistedThroughRounds && MwState.Instance.GetGoal() == 1)
            {
                apchestCount++;
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
        if ((NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer) && apchestCount > 0)
        {
            Plugin.Instance.LogDebug($"Attempting to increment total apchests by {apchestCount}");
            MwState.Instance.AddCollectathonScrap(apchestCount);
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

    /**
     * Overrides the chat length limit when sending messages
     */
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

    /**
     * Overrides the chat length limit when receiving messages on the server
     */
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

    private static TerminalNode _failNode;

    /**
     * Prevents buying locked items from the terminal. This includes store items, vehicles, ship upgrades, and moon reroutes.
     * Routing to the company building is a special case because node.buyRerouteToMoon is -1 for the initial node and 3 for the confirmation node, so we have to perform extra checks.
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Terminal), "LoadNewNode")]
    private static bool PreventBuyingLockedItems(ref TerminalNode node)
    {
        if (MultiworldHandler.Instance == null || !MultiworldHandler.Instance.IsConnected())
        {
            return true;
        }
        Terminal terminal = Plugin.Instance.GetTerminal();
        if (_failNode == null)
        {
            _failNode = ScriptableObject.CreateInstance<TerminalNode>();
            _failNode.name = "APLCGenericPurchaseFail";
            _failNode.displayText = $"This item is not unlocked yet! Find it in the multiworld to unlock it in the store.\n\n";
        }
        if (!Plugin.IsDawnLibInstalled && node.buyItemIndex != -1)  // remove this if we decide to completely switch to DawnLib
        {
            if (node.buyItemIndex != -7)
            {
                Item item = terminal.buyableItemsList[node.buyItemIndex];
                if (MwState.Instance.GetItemMap<StoreItems>(item.itemName).GetTotal() < 1)
                {
                    node = _failNode;
                }
            }
        }
        // we won't need this if vanilla vehicle support comes to DawnLib
        if (node.buyVehicleIndex != -1)
        {
            BuyableVehicle vehicle = terminal.buyableVehicles[node.buyVehicleIndex];
            if (MwState.Instance.GetItemMap<StoreItems>(vehicle.vehicleDisplayName).GetTotal() < 1)
            {
                node = _failNode;
            }
        }

        if (!Plugin.IsDawnLibInstalled && node.shipUnlockableID != -1)  // remove this if we decide to completely switch to DawnLib
        {
            if (node.shipUnlockableID < StartOfRound.Instance.unlockablesList.unlockables.Count)
            {
                try
                {
                    if (MwState.Instance.GetItemMap<ShipUpgrades>(StartOfRound.Instance.unlockablesList
                            .unlockables[node.shipUnlockableID].unlockableName).GetTotal() < 1)
                    {
                        node = _failNode;
                    }
                }
                catch (Exception)
                {
                    //Ignore, means that we collided with a cosmetic item which we don't randomize(yet)
                    Plugin.Instance.LogInfo("Collided with a cosmetic item in PreventBuyingLockedItems");
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
            __instance.GetComponentInChildren<ScanNodeProperties>().headerText = $"AP Apparatus - {(int.TryParse(planetName.Split(" ", 2)[0], out _) ? planetName.Split(" ", 2)[1] : planetName)}";
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
    private static void RemindPlayersOfAvailableFiller()
    {
        if (MultiworldHandler.Instance == null || !StartOfRound.Instance.currentLevel.PlanetName.Contains("Gordion") || !Plugin.BoundConfig.DisplayFillerNotification.Value) return;
        string[] fillerNames = ["More Time", "Clone Scrap", "Birthday Gift", "Money"];
        foreach (string fillerName in fillerNames)
        {
            if (MwState.Instance.GetItemMap<FillerItems>(fillerName).GetReceived() > MwState.Instance.GetItemMap<FillerItems>(fillerName).GetUsed())
            {
                HUDManager.Instance.DisplayTip("Archipelago", "You have unspent filler items! Use the 'apfiller' command in the terminal for details.");
                break;
            }
        }
    }
}