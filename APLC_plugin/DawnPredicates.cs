using System;
using System.Linq;
using Dawn;
using UnityEngine;

namespace APLC
{
    public class APLCMoonPurchasePredicate(DawnMoonInfo moonInfo, ITerminalPurchasePredicate priorPredicate) : ITerminalPurchasePredicate
    {
        private TerminalNode _failNode = null;

        TerminalPurchaseResult ITerminalPurchasePredicate.CanPurchase()
        {
            // if the multiworldhandler is null, check the predicate. otherwise, check if the moon item has been received
            // if the predicate is also null, set the location to always accesible. otherwise, just use that predicate
            if (MultiworldHandler.Instance == null)
            {
                if (priorPredicate == null) return TerminalPurchaseResult.Success();
                return priorPredicate.CanPurchase();
            }
            if (MultiworldHandler.Instance.GetReceivedItems().Contains(moonInfo.Level.PlanetName) || (moonInfo.Level.PlanetName == "71 Gordion" && MultiworldHandler.Instance.GetReceivedItems().Contains("Company Building")))
            {
                return TerminalPurchaseResult.Success();
            }
            if (_failNode == null)
            {
                _failNode = ScriptableObject.CreateInstance<TerminalNode>();
                _failNode.name = $"{moonInfo.Level.PlanetName.Replace(" ", "").SkipWhile(x => !char.IsLetter(x)).ToArray()}APLCTerminalPredicateFail";
                _failNode.displayText = "This moon is not unlocked yet! Find it in the multiworld to travel there.";
            }
            return TerminalPurchaseResult.Fail(_failNode).SetOverrideName($"{(moonInfo.Level.PlanetName == "71 Gordion" ? "The Company building" : moonInfo.GetNumberlessPlanetName())} (Locked)");
        }
    }
    public class APLCStorePurchasePredicate(DawnShopItemInfo itemInfo, ITerminalPurchasePredicate priorPredicate) : ITerminalPurchasePredicate
    {
        private TerminalNode _failNode = null;

        TerminalPurchaseResult ITerminalPurchasePredicate.CanPurchase()
        {
            if (MultiworldHandler.Instance == null)
            {
                if (priorPredicate == null) return TerminalPurchaseResult.Success();
                return priorPredicate.CanPurchase();
            }
            if (MwState.Instance.GetItemMap<StoreItems>(itemInfo.ParentInfo.Item.itemName).GetTotal() >= 1)
            {
                return TerminalPurchaseResult.Success();
            }
            if (_failNode == null)
            {
                _failNode = ScriptableObject.CreateInstance<TerminalNode>();
                _failNode.name = $"{itemInfo.ParentInfo.Item.itemName.Replace(" ", "").SkipWhile(x => !char.IsLetter(x)).ToArray()}APLCTerminalPredicateFail";
                _failNode.displayText = $"{itemInfo.ParentInfo.Item.itemName} is not unlocked yet! Find it in the multiworld to unlock it in the store.\n\n";
            }
            return TerminalPurchaseResult.Fail(_failNode).SetOverrideName($"{itemInfo.ParentInfo.Item.itemName} (Locked)");
        }
    }
    // we can't use this yet but it may be useable in the future
    public class APLCVehiclePredicate(DawnVehicleInfo vehicleInfo, ITerminalPurchasePredicate priorPredicate) : ITerminalPurchasePredicate
    {
        private TerminalNode _failNode = null;

        TerminalPurchaseResult ITerminalPurchasePredicate.CanPurchase()
        {
            if (MultiworldHandler.Instance == null)
            {
                if (priorPredicate == null) return TerminalPurchaseResult.Success();
                return priorPredicate.CanPurchase();
            }
            if (MwState.Instance.GetItemMap<StoreItems>(vehicleInfo.VehiclePrefab.name).GetTotal() >= 1)
            {
                return TerminalPurchaseResult.Success();
            }
            if (_failNode == null)
            {
                _failNode = ScriptableObject.CreateInstance<TerminalNode>();
                _failNode.name = $"{vehicleInfo.VehiclePrefab.name.Replace(" ", "").SkipWhile(x => !char.IsLetter(x)).ToArray()}APLCTerminalPredicateFail";
                _failNode.displayText = $"{vehicleInfo.VehiclePrefab.name} is not unlocked yet! Find it in the multiworld to unlock it in the store.\n\n";
            }
            return TerminalPurchaseResult.Fail(_failNode).SetOverrideName($"{vehicleInfo.VehiclePrefab.name} (Locked)");
        }
    }
    public class APLCUnlockablePurchasePredicate(DawnUnlockableItemInfo itemInfo, ITerminalPurchasePredicate priorPredicate) : ITerminalPurchasePredicate
    {
        private TerminalNode _failNode = null;

        TerminalPurchaseResult ITerminalPurchasePredicate.CanPurchase()
        {
            if (MultiworldHandler.Instance == null)
            {
                if (priorPredicate == null) return TerminalPurchaseResult.Success();
                return priorPredicate.CanPurchase();
            }
            if (MwState.Instance.GetItemMap<ShipUpgrades>(itemInfo.UnlockableItem.unlockableName).GetTotal() >= 1)
            {
                return TerminalPurchaseResult.Success();
            }
            if (_failNode == null)
            {
                _failNode = ScriptableObject.CreateInstance<TerminalNode>();
                _failNode.name = $"{itemInfo.UnlockableItem.unlockableName.Replace(" ", "").SkipWhile(x => !char.IsLetter(x)).ToArray()}APLCTerminalPredicateFail";
                _failNode.displayText = $"{itemInfo.UnlockableItem.unlockableName} is not unlocked yet! Find it in the multiworld to unlock it in the store.\n\n";
            }
            return TerminalPurchaseResult.Fail(_failNode).SetOverrideName($"{itemInfo.UnlockableItem.unlockableName} (Locked)");    // the override name doesn't get set for the vanilla upgrades
        }
    }
    public class DawnCompat
    {
        public static void AssignPurchasePredicate(SelectableLevel moon)
        {
            DawnMoonInfo moonInfo = moon.GetDawnInfo();
            if (moonInfo.DawnPurchaseInfo.PurchasePredicate is not APLCMoonPurchasePredicate)
                moonInfo.DawnPurchaseInfo.PurchasePredicate = new APLCMoonPurchasePredicate(moonInfo, moonInfo.DawnPurchaseInfo.PurchasePredicate);
        }
        public static void AssignPurchasePredicate(Item item)
        {
            DawnItemInfo storeIteminfo = item.GetDawnInfo();
            /*if (_isVehicle)
            {

                //terminal.buyableVehicles[itemsIndex].
                if (storeIteminfo.ShopInfo.DawnPurchaseInfo.PurchasePredicate is not APLCStorePurchasePredicate)
                    storeIteminfo.ShopInfo.DawnPurchaseInfo.PurchasePredicate = new APLCStorePurchasePredicate(storeIteminfo.ShopInfo, storeIteminfo.ShopInfo.DawnPurchaseInfo.PurchasePredicate);
            }
            else
            {*/
            //DawnShopItemInfo storeIteminfo = _item.GetDawnInfo().ShopInfo;

            if (storeIteminfo.ShopInfo.DawnPurchaseInfo.PurchasePredicate is not APLCStorePurchasePredicate)
                storeIteminfo.ShopInfo.DawnPurchaseInfo.PurchasePredicate = new APLCStorePurchasePredicate(storeIteminfo.ShopInfo, storeIteminfo.ShopInfo.DawnPurchaseInfo.PurchasePredicate);
            //}
        }
        public static void AssignPurchasePredicate(TerminalNode upgradeNode)
        {
            DawnUnlockableItemInfo unlockable = StartOfRound.Instance.unlockablesList.unlockables[upgradeNode.shipUnlockableID].GetDawnInfo();
            if (unlockable.DawnPurchaseInfo.PurchasePredicate is not APLCUnlockablePurchasePredicate)
                unlockable.DawnPurchaseInfo.PurchasePredicate = new APLCUnlockablePurchasePredicate(unlockable, unlockable.DawnPurchaseInfo.PurchasePredicate);
        }
    }
}
