using System;
using System.Linq;
using Archipelago.MultiClient.Net.Packets;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace APLC;

public class Patches
{
    private static readonly Harmony Harmony = new(PluginInfo.PLUGIN_GUID);
    private static float _time;
    
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
        while (_time >= 5f)
        {
            _time -= 5f;
            MultiworldHandler.Instance.TickItems();
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
                switch (scrap.name)
                {
                    case "ap_apparatus_experimentation(Clone)":
                        MultiworldHandler.Instance.CompleteTrophy("Experimentation");
                        break;
                    case "ap_apparatus_assurance(Clone)":
                        MultiworldHandler.Instance.CompleteTrophy("Assurance");
                        break;
                    case "ap_apparatus_vow(Clone)":
                        MultiworldHandler.Instance.CompleteTrophy("Vow");
                        break;
                    case "ap_apparatus_offense(Clone)":
                        MultiworldHandler.Instance.CompleteTrophy("Offense");
                        break;
                    case "ap_apparatus_march(Clone)":
                        MultiworldHandler.Instance.CompleteTrophy("March");
                        break;
                    case "ap_apparatus_rend(Clone)":
                        MultiworldHandler.Instance.CompleteTrophy("Rend");
                        break;
                    case "ap_apparatus_dine(Clone)":
                        MultiworldHandler.Instance.CompleteTrophy("Dine");
                        break;
                    case "ap_apparatus_titan(Clone)":
                        MultiworldHandler.Instance.CompleteTrophy("Titan");
                        break;
                }
            }
        }
    }
    
    //Misc
    /**
     * Handles the receiving of connection packets from the host(sent invisibly through the chat)
     */
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    private static bool CheckConnections(ref string chatMessage)
    {
        var fail = ChatHandler.HandleConnectingOthers(chatMessage);

        return ChatHandler.AllowChatMessageToSend(chatMessage) && fail;
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

    // //Trophy case stuff
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(StartOfRound), "OnShipLandedMiscEvents")]
    // private static void SpawnTrophyCase()
    // {
    //     //if (MultiworldHandler.Instance == null || MultiworldHandler.Instance.GetGoal() == 1/* || StartOfRound.Instance.currentLevel.PlanetName != "Company"*/) return;
    //     var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    //     go.transform.localScale = new Vector3(0.7f,7,10);
    //     go.transform.position = new Vector3(-28.25f, 0.8f, 0);
    //     go.AddComponent<TrophyCase>();
    //     Object.Instantiate(go, new Vector3(-28.25f, 0.8f, 0), Quaternion.identity);
    // }
}