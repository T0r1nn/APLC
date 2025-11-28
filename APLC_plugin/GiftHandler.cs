using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using Archipelago.Gifting.Net.Service;
using Archipelago.Gifting.Net.Traits;
using Archipelago.Gifting.Net.Utilities.CloseTraitParser;
using Archipelago.Gifting.Net.Versioning.Gifts;
using Archipelago.Gifting.Net.Versioning.Gifts.Current;
using LethalLevelLoader;
using Unity.Netcode;
using UnityEngine;

namespace APLC
{
    /**
     * Handles receiving and sending gifts via Archipelago.Gifting.Net. At the monent, only store items are supported, gifts are send via terminal command, and gifts are auto-delivered upon receipt.
     * The system has been tested with vanilla items only, and only between two Lethal Company players. Gifting between LC and different games has not been tested and may not yet work as intended.
     * I would eventually like to handle custom store items using their LLL tags to apply relevant traits. 
     * 
     **/
    internal class GiftHandler
    {
        private readonly GiftingService _giftingService;
        private readonly ICloseTraitParser<string> closeTraitParser;
        private static GiftHandler Instance;
        // move the rest of the logic here if things work out

        GiftHandler(GiftingService giftingService)
        {
            _giftingService = giftingService;
            _giftingService.OnNewGift += DeliverGift;
            closeTraitParser = new BKTreeCloseTraitParser<string>();

            var store = Plugin.Instance.GetTerminal().buyableItemsList;
            var vehicles = Plugin.Instance.GetTerminal().buyableVehicles;

            foreach (var storeItem in store)
            {
                string itemName = String.Join("", storeItem.itemName.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)).ToLower();
                //ExtendedItem extendedStoreItem = LethalLevelLoader.PatchedContent.ExtendedItems.Find(extItem => extItem.Item.Equals(storeItem));
                // my translation of tags: Tool -> Tool, Weapon + Consumable -> Bomb, Weapon -> Weapon, Communicative -> Communicative, Transportation -> Transportation
                //extendedStoreItem.ContentTagStrings
                // enumerate isn't the right word, but use the linq equivalent of a switch to pair vanilla items with relevant traits and a few common modded ones
                List<GiftTrait> traits = itemName switch // for now, I will only do store items (not the cruiser)
                {
                    "boombox" => [new GiftTrait("Instrument"), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                    "extensionladder" => [new GiftTrait("Transportation"), new GiftTrait(GiftFlag.Tool)],
                    "flashlight" => [new GiftTrait(GiftFlag.Light), new GiftTrait(GiftFlag.Tool)],
                    "pro-flashlight" => [new GiftTrait(GiftFlag.Light, 1.2), new GiftTrait(GiftFlag.Tool)],
                    "jetpack" => [new GiftTrait("Transportation", 1.3), new GiftTrait(GiftFlag.Tool)],
                    "lockpicker" => [new GiftTrait(GiftFlag.Key, 1.2), new GiftTrait(GiftFlag.Tool)],
                    "radar-booster" => [new GiftTrait("Communicative", 1.2), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                    "shovel" => [new GiftTrait("MeleeWeapon"), new GiftTrait(GiftFlag.Weapon)],
                    "spraypaint" => [new GiftTrait("Communicative", 0.6), new GiftTrait(GiftFlag.Tool), new GiftTrait(GiftFlag.Consumable)],
                    "stungrenade" => [new GiftTrait(GiftFlag.Bomb), new GiftTrait(GiftFlag.Tool), new GiftTrait(GiftFlag.Consumable)],
                    "tzp-inhalant" => [new GiftTrait(GiftFlag.Speed), new GiftTrait(GiftFlag.Buff), new GiftTrait(GiftFlag.Tool), new GiftTrait(GiftFlag.Consumable)],
                    "walkie-talkie" => [new GiftTrait("Communicative"), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                    "zapgun" => [new GiftTrait("RangedWeapon"), new GiftTrait(GiftFlag.Weapon), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                    "weedkiller" => [new GiftTrait("Chemicals"), new GiftTrait("Chemicals"), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                    "beltbag" => [new GiftTrait("Bag"), new GiftTrait("Container"), new GiftTrait(GiftFlag.Tool)],
                    //"Homemade flashbang" => [new GiftTrait(GiftFlag.Bomb, 0.7), new GiftTrait(GiftFlag.Tool, 0.7), new GiftTrait(GiftFlag.Consumable)],
                    //"Gun Ammo" => [new GiftTrait("Ammunition"), new GiftTrait(GiftFlag.Weapon, 0.5), new GiftTrait(GiftFlag.Consumable)],
                    //"Jar of pickles" => [new GiftTrait("Pickle"), new GiftTrait(GiftFlag.Vegetable), new GiftTrait(GiftFlag.Food)],
                    //"Key" => [new GiftTrait(GiftFlag.Key), new GiftTrait(GiftFlag.Consumable)],
                    //"Kitchen knife" => [new GiftTrait("MeleeWeapon"), new GiftTrait(GiftFlag.Weapon, 2)],
                    //"Shotgun" => [new GiftTrait("RangedWeapon"), new GiftTrait(GiftFlag.Weapon, 3)],
                    //"Stop sign" => [new GiftTrait("MeleeWeapon"), new GiftTrait(GiftFlag.Weapon), new GiftTrait(GiftFlag.Metal)],
                    //"Yield sign" => [new GiftTrait("MeleeWeapon"), new GiftTrait(GiftFlag.Weapon), new GiftTrait(GiftFlag.Metal)],
                    _ => null
                };

                if (traits != null)
                {
                    closeTraitParser.RegisterAvailableGift(itemName, [.. traits]);
                }
            }

            //foreach (var storeItem in vehicles)
            //{
            //    closeTraitParser.RegisterAvailableGift(storeItem.vehicleDisplayName, [new("Vehicle"), new(GiftFlag.Metal, 3)]);
            //}
        }

        public static GiftHandler GetInstance(GiftingService giftingService = null) // I might not end up doing it this way, but this is the "correct" way to handle a singleton
        {
            Instance ??= new GiftHandler(giftingService ?? MultiworldHandler.Instance.GetGiftingService());
            return Instance;
        }

        public void DeliverAllGiftsSinceLastSession()   
        {
            var gifts = _giftingService.CheckGiftBox();
            Plugin.Instance.LogInfo($"Collecting all {gifts.Count} gift(s) sent since the last session"); // it might be better to let players claim gifts with a terminal command instead of auto-claiming
            foreach (var gift in gifts)
            {
                DeliverGift(gift.Value);
            }
        }

        public void DeliverGift(Gift gift)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)) return; // only the server/host should be delivering gifts
            //closeTraitParser.FindClosestAvailableGift(gift.Traits);
            Plugin.Instance.LogInfo($"Received gift {gift.ItemName}");
            Item[] storeItems = Plugin.Instance.GetTerminal().buyableItemsList;
            GiftTrait[] traits = gift.Traits;

            Item giftToDeliver = storeItems.FirstOrDefault<Item>(buyableItem => String.Join("", buyableItem.itemName.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)).ToLower().Equals(gift.ItemName)) ?? 
                storeItems.FirstOrDefault<Item>(buyableItem => buyableItem.itemName.Equals(closeTraitParser.FindClosestAvailableGift(gift.Traits).FirstOrDefault()));

            if (giftToDeliver != null)
            {
                Plugin.Instance.LogInfo("Gift accepted! Delivering to local player(s).");
                Plugin.Instance.GetTerminal().orderedItemsFromTerminal.Add(Array.IndexOf(storeItems, giftToDeliver));
            }
            else
            {
                Plugin.Instance.LogInfo("Gift rejected! Gift does not match any valid items.");
                _giftingService.RefundGift(gift);
                return;
            }
            /*else if (traits.Any(trait => trait.Trait.Equals("Heal"))) // heal will grant health to all players based on the gift's trait strength
            {
                RemainingHealthToGrant += (int)();
            }*/
            // Heal, Life, and Trap will be more complex to implement. Life may not be possible at all because player health is hardcoded to be capped at 100 in several places.
            //HUDManager.Instance.DisplayTip("AP Notification", $"You received a gift from {gift.SenderSlot}!");    // needs to be done on clients, too
            _giftingService.RemoveGiftFromGiftBox(gift.ID);

            // note: if I want to deliver scrap via drop pod, I'll need to patch ItemDropship.OpenShipDoorsOnServer()
        }

        public void SendGift(string recipient, string itemName, out string message)
        {
            // slot checks
            if (recipient.Equals(MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot), StringComparison.OrdinalIgnoreCase))
            {
                message = "Unable to send gift. You cannot send a gift to yourself.";
                return; // cannot gift to self
            }

            Item[] storeItems = Plugin.Instance.GetTerminal().buyableItemsList;
            Item buyableItem = storeItems.FirstOrDefault(buyableItem => String.Join("", buyableItem.itemName.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)).Equals(itemName, StringComparison.OrdinalIgnoreCase));
            if (buyableItem == null)
            {
                message = $"Unable to send gift. Item '{itemName}' could not be found in the store";
                return; // item not found in store
            }
            Plugin.Instance.LogIfDebugBuild("found item in store");

            var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
                        where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                        select obj).ToList();
            GrabbableObject floorItemToSend = list.FirstOrDefault(grabbableItem => String.Join("", grabbableItem.itemProperties.itemName.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)).Contains(itemName, StringComparison.OrdinalIgnoreCase));
            if (floorItemToSend == null)
            {
                message = $"Unable to send gift. No item with the name '{itemName}' can be found in the ship.";
                return;
            }
            Plugin.Instance.LogIfDebugBuild("found item in ship");

            List<GiftTrait> giftTraits = itemName switch // for now, I will only do store items (not the cruiser)
            {
                "boombox" => [new GiftTrait("Instrument"), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                "extensionladder" => [new GiftTrait("Transportation"), new GiftTrait(GiftFlag.Tool)],   // this one is impossible to send with a command unless we increase the terminal character limit
                "flashlight" => [new GiftTrait(GiftFlag.Light), new GiftTrait(GiftFlag.Tool)],
                "pro-flashlight" => [new GiftTrait(GiftFlag.Light, 1.2), new GiftTrait(GiftFlag.Tool)],
                "jetpack" => [new GiftTrait("Transportation", 1.3), new GiftTrait(GiftFlag.Tool)],
                "lockpicker" => [new GiftTrait(GiftFlag.Key, 1.2), new GiftTrait(GiftFlag.Tool)],
                "radar-booster" => [new GiftTrait("Communicative", 1.2), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                "shovel" => [new GiftTrait("MeleeWeapon"), new GiftTrait(GiftFlag.Weapon)],
                "spraypaint" => [new GiftTrait("Communicative", 0.6), new GiftTrait(GiftFlag.Tool), new GiftTrait(GiftFlag.Consumable)],
                "stungrenade" => [new GiftTrait(GiftFlag.Bomb), new GiftTrait(GiftFlag.Tool), new GiftTrait(GiftFlag.Consumable)],
                "tzp-inhalant" => [new GiftTrait(GiftFlag.Speed), new GiftTrait(GiftFlag.Buff), new GiftTrait(GiftFlag.Tool), new GiftTrait(GiftFlag.Consumable)],
                "walkie-talkie" => [new GiftTrait("Communicative"), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                "zapgun" => [new GiftTrait("RangedWeapon"), new GiftTrait(GiftFlag.Weapon), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                "weedkiller" => [new GiftTrait("Chemicals"), new GiftTrait("Chemicals"), new GiftTrait(GiftFlag.Tool), new GiftTrait("Electronics")],
                "beltbag" => [new GiftTrait("Bag"), new GiftTrait("Container"), new GiftTrait(GiftFlag.Tool)],
                //"Homemade flashbang" => [new GiftTrait(GiftFlag.Bomb, 0.7), new GiftTrait(GiftFlag.Tool, 0.7), new GiftTrait(GiftFlag.Consumable)],
                //"Gun Ammo" => [new GiftTrait("Ammunition"), new GiftTrait(GiftFlag.Weapon, 0.5), new GiftTrait(GiftFlag.Consumable)],
                //"Jar of pickles" => [new GiftTrait("Pickle"), new GiftTrait(GiftFlag.Vegetable), new GiftTrait(GiftFlag.Food)],
                //"Key" => [new GiftTrait(GiftFlag.Key), new GiftTrait(GiftFlag.Consumable)],
                //"Kitchen knife" => [new GiftTrait("MeleeWeapon"), new GiftTrait(GiftFlag.Weapon, 2)],
                //"Shotgun" => [new GiftTrait("RangedWeapon"), new GiftTrait(GiftFlag.Weapon, 3)],
                //"Stop sign" => [new GiftTrait("MeleeWeapon"), new GiftTrait(GiftFlag.Weapon), new GiftTrait(GiftFlag.Metal)],
                //"Yield sign" => [new GiftTrait("MeleeWeapon"), new GiftTrait(GiftFlag.Weapon), new GiftTrait(GiftFlag.Metal)],
                _ => null
            };

            var giftQueryResult = _giftingService.CanGiftToPlayer(recipient, giftTraits.Select(trait => trait.Trait));
            if (!giftQueryResult.CanGift)
            {
                message = $"Unable to send gift to {recipient}: {giftQueryResult.Message}";
                return;
            }
            Plugin.Instance.LogIfDebugBuild("gift is sendable");

            GiftItem item = new GiftItem(itemName, 1, buyableItem.creditsWorth);
            var giftingresult = _giftingService.SendGift(item, giftTraits.ToArray(), recipient);    // this should eventually be handled by the host, but for now, it's easier to test this way

            // only destroy the object if the gift was successfully sent
            if (giftingresult.Success)
            {
                if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)   // handle item destruction on the host
                {
                    if (floorItemToSend is BeltBagItem beltBag)
                    {
                        for (int id = beltBag.objectsInBag.Count - 1; id >= 0; id--)
                        {
                            beltBag.RemoveObjectFromBag(id);
                        }
                    }
                    UnityEngine.Object.Destroy(floorItemToSend.gameObject);     // be aware that this CAN be undone by reloading the save.
                                                                                // It might be worth keeping a 'sent gift' list that saves to the file and gets cleared during GradingPostfix
                                                                                // that way, we can delete items that were sent even if the game is reloaded before the round ends
                }
                else DestroyGiftedItemServerRpc(itemName);
            }
            message = giftingresult.Success ? $"Gift in transit! Warning: the Company is not responsible for lost or damaged property" : "Failed to send gift";
        }

        [Rpc(SendTo.Server)]
        public void DestroyGiftedItemServerRpc(string itemName)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)) return; 
            var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
                        where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                        select obj).ToList();
            GrabbableObject giftedItem = list.FirstOrDefault(grabbableItem => String.Join("", grabbableItem.itemProperties.itemName.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)).Contains(itemName, StringComparison.OrdinalIgnoreCase));
            if (giftedItem != null)
            {
                if (giftedItem is BeltBagItem beltBag)
                {
                    for (int id = beltBag.objectsInBag.Count - 1; id >= 0; id--)
                    {
                        beltBag.RemoveObjectFromBag(id);
                    }
                }
                UnityEngine.Object.Destroy(giftedItem.gameObject);// be aware that this CAN be undone by reloading the save.
                                                                  // It might be worth keeping a 'sent gift' list that saves to the file and gets cleared during GradingPostfix
                                                                  // that way, we can delete items that were sent even if the game is reloaded before the round ends
                Plugin.Instance.LogIfDebugBuild($"Destroyed gifted item {itemName} upon request");
            }
        }

        public void Disconnect()
        {
            _giftingService.OnNewGift -= DeliverGift;
            Instance = null;
            Plugin.Instance.LogInfo("Destroying GiftHandler due to lobby close");
        }

    }
}
