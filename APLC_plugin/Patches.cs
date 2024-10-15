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
        return ((PlayerUpgrades)MultiworldHandler.Instance.GetItemMap("Scanner")).GetNum() >= 1;
    }

    /**
     * Cancels the scan animation when the scanner isn't unlocked
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "PingScan_performed")]
    private static bool CancelScanAnimation()
    {
        if (MultiworldHandler.Instance == null) return true;
        return ((PlayerUpgrades)MultiworldHandler.Instance.GetItemMap("Scanner")).GetNum() >= 1;
    }

    /**
     *  Limits the stamina of the player when stamina is shuffled
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    private static bool Sprint(PlayerControllerB __instance)
    {
        if (MultiworldHandler.Instance == null) return true;
        int staminaChecks = ((PlayerUpgrades)MultiworldHandler.Instance.GetItemMap("Stamina Bar")).GetNum();
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
        if (__result >= ((PlayerUpgrades)MultiworldHandler.Instance.GetItemMap("Inventory Slot")).GetNum())
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
        int invSlots = ((PlayerUpgrades)MultiworldHandler.Instance.GetItemMap("Inventory Slot")).GetNum();
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
                    0.90f, MultiworldHandler.Instance.GetItemMap<PlayerUpgrades>("Strength Training").GetNum()
                ),
                0f,
                10f
            )
        );

        __instance.carryWeight = Mathf.Max(1f, newWeight);
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
            MultiworldHandler.Instance.TickItems();
        }

        while (_time1Sec >= 1f)
        {
            _time1Sec -= 1f;
            if (_waitingForTerminalQuit && StartOfRound.Instance.localPlayerController.inTerminalMenu)
            {
                AccessTools.Method(typeof(Terminal), "QuitTerminal", new []{ typeof(bool) })
                    .Invoke(Plugin._instance.getTerminal(), new object[] { true });
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
                new MultiworldHandler(url, port, slot, password);
            }
        }
        else
        {
            Plugin._instance.getTerminal().GetComponent<APLCNetworking>().RequestConnectionRpc();
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
            displayText.Substring(0, 19) == "Found journal entry") MultiworldHandler.Instance.CheckLogs();

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
            if (MultiworldHandler.Instance.GetItemMap<PlayerUpgrades>("Terminal").GetNum() == 0)
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
                StringBuilder stringBuilder2 = new StringBuilder();
                for (int j = 0; j < __instance.buyableItemsList.Length; j++)
                {
                    try
                    {
                        if (GameNetworkManager.Instance.isDemo && __instance.buyableItemsList[j].lockedInDemo)
                        {
                            stringBuilder2.Append("\n* " + __instance.buyableItemsList[j].itemName + " (Locked)");
                        }
                        else
                        {
                            if (MultiworldHandler.Instance
                                    .GetItemMap<StoreItems>(__instance.buyableItemsList[j].itemName)
                                    .GetTotal() >= 1)
                            {
                                stringBuilder2.Append("\n* " + __instance.buyableItemsList[j].itemName +
                                                      "  //  Price: $" +
                                                      ((float)__instance.buyableItemsList[j].creditsWorth *
                                                       ((float)__instance.itemSalesPercentages[j] / 100f)).ToString());

                            }
                            else
                            {
                                stringBuilder2.Append(
                                    "\n* " + __instance.buyableItemsList[j].itemName + "  //  Locked!");
                            }
                        }

                        if (__instance.itemSalesPercentages[j] != 100 && MultiworldHandler.Instance
                                .GetItemMap<StoreItems>(__instance.buyableItemsList[j].itemName).GetTotal() >= 1)
                        {
                            stringBuilder2.Append(string.Format("   ({0}% OFF!)",
                                100 - __instance.itemSalesPercentages[j]));
                        }
                    }
                    catch (Exception)
                    {
                        stringBuilder2.Append(string.Format("\n* " + __instance.buyableItemsList[j].itemName +
                                                            "  //  Price: $" +
                                                            ((float)__instance.buyableItemsList[j].creditsWorth *
                                                             ((float)__instance.itemSalesPercentages[j] / 100f))
                                                            .ToString()));
                        if (__instance.itemSalesPercentages[j] != 100)
                        {
                            stringBuilder2.Append(string.Format("   ({0}% OFF!)",
                                100 - __instance.itemSalesPercentages[j]));
                        }
                    }
                }
                modifiedDisplayText = modifiedDisplayText.Replace("[buyableItemsList]", stringBuilder2.ToString());
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
        MultiworldHandler.Instance.GetLocationMap("Quota").CheckComplete();
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
        if (dead) MultiworldHandler.Instance.HandleDeathLink();
        
        ((MoonLocations)MultiworldHandler.Instance.GetLocationMap(StartOfRound.Instance.currentLevel.PlanetName.Split(" ")[1])).OnFinishMoon(StartOfRound.Instance.currentLevel.PlanetName, grade);

        if (MultiworldHandler.Instance.GetSlotSetting("scrapsanity") == 1)
        {
            MultiworldHandler.Instance.GetLocationMap("Scrap").CheckComplete();
        }

        var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
            where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
            select obj).ToList();
        foreach (var scrap in list)
        {
            if (scrap.name == "ap_chest(Clone)" && MultiworldHandler.Instance.GetGoal() == 1)
            {
                Object.Destroy(scrap.gameObject);
                MultiworldHandler.Instance.AddCollectathonScrap(1);
            }
            else if (MultiworldHandler.Instance.GetGoal() == 0)
            {
                if (scrap.name.Contains("ap_apparatus_"))
                {
                    string[] landing = new string[scrap.name.Split("_").Length-2];
                    Array.ConstrainedCopy(scrap.name.Split("_"), 2, landing, 0, scrap.name.Split("_").Length - 2);
                    MultiworldHandler.Instance.CompleteTrophy(String.Join(" ", landing).Split("(Clone)")[0].ToLower(), scrap);
                }
            }
        }
    }
    
    //Misc
    /**
     * Fixes the double message bug 
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    private static bool CheckConnections(ref string chatMessage)
    {
        var fail = ChatHandler.PreventMultisendBug(chatMessage);

        return fail;
    }
    
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
        var packet = new SayPacket()
        {
            Text = chatMessage
        };
        MultiworldHandler.Instance.GetSession().Socket.SendPacket(packet);
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Terminal), "LoadNewNode")]
    private static bool PreventBuyingLockedItems(TerminalNode node){
        if (MultiworldHandler.Instance == null || !MultiworldHandler.Instance.IsConnected())
        {
            return true;
        }
        if (node.buyItemIndex != -1)
        {
            if (node.buyItemIndex != -7)
            {
                Item item = Plugin._instance.getTerminal().buyableItemsList[node.buyItemIndex];
                if (MultiworldHandler.Instance.GetItemMap<StoreItems>(item.itemName).GetTotal() < 1)
                {
                    Plugin._instance.getTerminal().LoadNewNode(Plugin._instance.getTerminal().currentNode);
                    return false;
                }
            }
        }

        if (node.buyVehicleIndex != -1)
        {
            BuyableVehicle vehicle = Plugin._instance.getTerminal().buyableVehicles[node.buyVehicleIndex];
            if (MultiworldHandler.Instance.GetItemMap<StoreItems>(vehicle.vehicleDisplayName).GetTotal() < 1)
            {
                Plugin._instance.getTerminal().LoadNewNode(Plugin._instance.getTerminal().currentNode);
                return false;
            }
        }

        if (node.shipUnlockableID != -1)
        {
            if (node.shipUnlockableID < StartOfRound.Instance.unlockablesList.unlockables.Count)
            {
                try
                {
                    if (MultiworldHandler.Instance.GetItemMap<ShipUpgrades>(StartOfRound.Instance.unlockablesList
                            .unlockables[node.shipUnlockableID].unlockableName).GetTotal() < 1)
                    {
                        Plugin._instance.getTerminal().LoadNewNode(Plugin._instance.getTerminal().currentNode);
                        return false;
                    }
                }
                catch (Exception)
                {
                    //Ignore, means that we collided with a cosmetic item which we don't randomize(yet)
                }
            }
        }

        if (node.buyRerouteToMoon != -1 && node.buyRerouteToMoon != -2)
        {
            SelectableLevel level = StartOfRound.Instance.levels[node.buyRerouteToMoon];
            
            string moonName = level.PlanetName;
            moonName = moonName.Substring(moonName.IndexOf(" ") + 1, moonName.Length - moonName.IndexOf(" ") - 1);
            if (moonName.Contains("Liquidation")) return true;

            if (moonName.Contains("Gordion"))
            {
                //TODO: check if company is rando'd, if yes block if not unlocked, if no then return true;
            }

            if (MultiworldHandler.Instance.GetItemMap<MoonItems>(moonName).GetTotal() < 1)
            {
                Plugin._instance.getTerminal().LoadNewNode(Plugin._instance.getTerminal().currentNode);
                return false;
            }
        }
        else if (node.buyRerouteToMoon == -2)
        {
            SelectableLevel level = StartOfRound.Instance.levels[node.terminalOptions[1].result.buyRerouteToMoon];
            string moonName = level.PlanetName;
            moonName = moonName.Substring(moonName.IndexOf(" ") + 1, moonName.Length - moonName.IndexOf(" ") - 1);
            if (moonName.Contains("Liquidation")) return true;

            if (moonName.Contains("Gordion"))
            {
                //TODO: check if company is rando'd, if yes block if not unlocked, if no then return true;
            }

            if (MultiworldHandler.Instance.GetItemMap<MoonItems>(moonName).GetTotal() < 1)
            {
                Plugin._instance.getTerminal().LoadNewNode(Plugin._instance.getTerminal().currentNode);
                return false;
            }
        }

        return true;
    }

    // //Trophy case stuff
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(StartOfRound), "OnShipLandedMiscEvents")]
//     private static void SpawnTrophyCase()
//     {
//         //if (MultiworldHandler.Instance == null || MultiworldHandler.Instance.GetGoal() == 1/* || StartOfRound.Instance.currentLevel.PlanetName != "Company"*/) return;
//         var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
// //        go.transform.localScale = new Vector3(0.7f,7,10);
// //        go.transform.position = new Vector3(-28.25f, 0.8f, 0);
// //        go.AddComponent<TrophyCase>();
//     }
}