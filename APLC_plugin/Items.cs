using System;
using System.Linq;
using System.Threading.Tasks;
using Dawn;
using Unity.Netcode;

namespace APLC;

public class ItemCreator
{
    public static async Task<Items> CreateFillerItemAsync(string name, Func<bool> receivedFunc, bool trap)
    {
        FillerItems filler = new(name, receivedFunc, trap);
        await filler.Setup();
        return filler;
    }

    public static Items CreateMoonItem(SelectableLevel level)
    {
        MoonItems moon = new(level);
        _ = moon.Setup();
        return moon;
    }

    public static Items CreateStoreItem(Item item)
    {
        StoreItems storeItem = new(item);
        _ = storeItem.Setup();
        return storeItem;
    }

    public static Items CreateVehicleItem(BuyableVehicle vehicle)
    {
        StoreVehicleItems vehicleItem = new(vehicle);
        _ = vehicleItem.Setup();
        return vehicleItem;
    }

    public static Items CreateUnlockableItem(UnlockableItem unlockable)
    {
        ShipUpgrades shipUpgrade = new(unlockable);
        _ = shipUpgrade.Setup();
        return shipUpgrade;
    }

    public static Items CreateUpgradeItem(string name, int startingAmount)
    {
        PlayerUpgrades playerUpgrade = new(name, startingAmount);
        _ = playerUpgrade.Setup();
        return playerUpgrade;
    }
}

/**
 * Handles received items, extended classes handle specific items
 */
public abstract class Items
{
    protected int _received { get; private set; }
    protected int _total { get; private set; }
    private int _waiting;
    private bool _isPersistent;
    public string _name { get; private set; }

    public Items(string name, bool isPersistent = false)
    {
        _name = name;
        _isPersistent = isPersistent;
    }

    internal async Task Setup()
    {
        if (_isPersistent)
        {
            try
            {
                MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-" + _name].Initialize(0);
                _total = await MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-" + _name].GetAsync<int>();
            }
            catch (Exception e)
            {
                _total = 0;
                Plugin.Logger.LogError(e.Message + "\n" + e.StackTrace);
            }
        }
        else
        {
            _total = 0;
        }
    }

    /** 
     * Handles when an item is received from Archipelago.
     * This increments _waiting if the item is filler (excluding Strength Training) and _total if it is anything else (moon, store item, ship upgrade, or player upgrade).
     */
    public void OnReceived()
    {
        _received++;
        if (_received <= _total) return;
        if (!HandleReceived())
        {
            _waiting++;
        }
        else
        {
            _total++;
        }

        if (_isPersistent && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
        {
            MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-" + _name] = _total;
        }
    }

    protected abstract bool HandleReceived(bool isTick=false);

    public void Reset()
    {
        if (!_isPersistent)
        {
            _received = 0;
            _total = 0;
            _waiting = 0;
            //MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-"+_name] = 0;
        }
        else
        {
            _received = 0;
            _waiting = 0;
        }
    }

    public void Tick()
    {
        if (!GameNetworkManager.Instance.localPlayerController.IsHost) return;
        if (_waiting <= 0) return;
        if (!HandleReceived(true)) return;
        SuccessfulUse();
    }

    public int GetTotal()
    {
        return _total;
    }

    public int GetUsed()
    {
        return _total;
    }

    public int GetReceived()
    {
        return _received;
    }

    // only used for filler and traps at the moment. maybe tie this to the _isPersistent field for consistency?
    protected void SuccessfulUse()
    {
        if (GameNetworkManager.Instance.localPlayerController.IsHost)
        {
            _waiting--;
            _total++;
            MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-" + _name] = _total;
            APLCNetworking.Instance.SyncClientFillerUsedRpc(_name, _received, _total);
        }
    }

    public void UpdateUsed(int newReceived = -1, int newTotal = -1)
    {
        if (newReceived == -1 || newTotal == -1) return;
        _received = newReceived;
        _total = newTotal;
    }
}

/**
 * Filler items are items that are not required to beat the game and do not help the player substantially.
 * These include Money, More time, Birthday gifts, Scrap clone, and traps. Strength Training is instead considered a player upgrade.
 */
public class FillerItems : Items
{
    private readonly Func<bool> _receivedFunc;
    private readonly bool _trap;
    public FillerItems(string name, Func<bool> receivedFunc, bool trap) : base(name, isPersistent:true)
    {
        _receivedFunc = receivedFunc;
        _trap = trap;
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        return isTick && (Config.FillerTriggersInstantly || _trap) && _receivedFunc();
    }

    public bool Use()
    {
        if (_receivedFunc())
        {
            SuccessfulUse();
            return true;
        }
        else
        {
            if (!GameNetworkManager.Instance.localPlayerController.IsHost && _received > _total)
            {
                APLCNetworking.Instance.UseFillerServerRpc(_name, GameNetworkManager.Instance.localPlayerController.playerClientId);
                return true;   // might not want to return true because the terminal will show a successful use even if the host refused the attempt
            }
            return false;
        }
    }
}

/**
 * Moon items unlock routes to new moons in the terminal.
 */
public class MoonItems : Items
{
    private readonly SelectableLevel _level;
    public MoonItems(SelectableLevel level) : base(level.PlanetName)   // todo: pass the SelectableLevel as a parameter instead of the moon name
    {

        DawnCompat.AssignPurchasePredicate(level);
        if (level.GetDawnInfo().RouteNode?.itemCost > 0) level.GetDawnInfo().RouteNode.itemCost = 0;
        if (level.GetDawnInfo().ReceiptNode?.itemCost > 0) level.GetDawnInfo().ReceiptNode.itemCost = 0;

        _level = level;
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        Plugin.Logger.LogInfo($"Unlocking moon {_level.PlanetName}");
        // We don't need to do anything here because Dawn handles the unlocking for us
        return true;
    }
}

/**
 * Store items are items that can be purchased in the terminal store.
 */
public class StoreItems : Items
{
    private readonly Item _item;

    public StoreItems(Item item) : base(item.itemName)
    {
        //Terminal terminal = Plugin.Instance.GetTerminal();
        _item = item;
        DawnCompat.AssignPurchasePredicate(_item);
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        return true;
    }
}

/**
 * Store items are items that can be purchased in the terminal store.
 */
public class StoreVehicleItems : Items
{
    private readonly BuyableVehicle _vehicle;

    public StoreVehicleItems(BuyableVehicle vehicle) : base(vehicle.vehicleDisplayName)
    {
        _vehicle = vehicle;
    }

    protected override bool HandleReceived(bool isTick = false)
    {
        return true;
    }
}

/**
 * Ship upgrades are items that modify or can be placed on the player's ship.
 */
public class ShipUpgrades : Items
{
    private readonly UnlockableItem _upgrade;

    public ShipUpgrades(UnlockableItem item) : base(item.unlockableName)
    {
        _upgrade = item;
        DawnCompat.AssignPurchasePredicate(_upgrade);
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        return true;
    }
}

/**
 * Player upgrades are items that modify the player's abilities.
 */
public class PlayerUpgrades : Items
{
    private readonly int _startingAmount;

    public PlayerUpgrades(string name, int startingAmount) : base(name)
    {
        _startingAmount = startingAmount;
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        return true;
    }

    public int GetNum()
    {
        return _startingAmount + GetTotal();
    }
}