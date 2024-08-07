﻿using System;
using System.Linq;
using LethalLevelLoader;

namespace APLC;

/**
 * Handles received items, extended classes handle specific items
 */
public abstract class Items
{
    private int _received;
    private int _total;         
    private int _waiting; 
    private bool _resetAll;
    private string _name;
    protected void Setup(string name, bool resetAll=false)
    {
        _name = name;
        try
        {
            MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-"+name].Initialize(0);
            _total = MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-"+name];
        }
        catch (Exception e)
        {
            _total = 0;
            Plugin._instance.LogError(e.Message+"\n"+e.StackTrace);
        }

        _resetAll = resetAll;
    }

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

        if (GameNetworkManager.Instance.localPlayerController.IsHost)
        {
            MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-"+_name] = _total;
        }
    }

    protected abstract bool HandleReceived(bool isTick=false);

    public void Reset()
    {
        if (_resetAll)
        {
            _received = 0;
            _total = 0;
            _waiting = 0;
            MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-"+_name] = 0;
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
        _waiting--;
        _total++;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-"+_name] = _total;
    }

    public int GetTotal()
    {
        return _total;
    }
}

public class FillerItems : Items
{
    private readonly Func<bool> _receivedFunc;
    public FillerItems(string name, Func<bool> receivedFunc)
    {
        Setup(name);
        _receivedFunc = receivedFunc;
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        return isTick && _receivedFunc();
    }
}

public class MoonItems : Items
{
    private readonly int _terminalIndex;
    private readonly int keywordIndex = 26;
    private readonly string _name;
    public MoonItems(string name)
    {
        for (int i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords.Length; i++)
        {
            if (Plugin._instance.getTerminal().terminalNodes.allKeywords[i].name == "Route")
            {
                keywordIndex = i;
            }
        }
        Setup(name, resetAll:true);
        for (var i = 0; i < Plugin._instance.getTerminal().terminalNodes.allKeywords[keywordIndex].compatibleNouns.Length; i++)
        {
            if (Plugin._instance.getTerminal().terminalNodes.allKeywords[keywordIndex].compatibleNouns[i].noun.word.ToLower()
                .Contains(name.ToLower()))
            {
                _terminalIndex = i;
            }
        }
        Plugin._instance.getTerminal().terminalNodes.allKeywords[keywordIndex].compatibleNouns[_terminalIndex].result.itemCost = 10000000;
        Plugin._instance.getTerminal().terminalNodes.allKeywords[keywordIndex].compatibleNouns[_terminalIndex].result.terminalOptions[1].result
            .itemCost = 10000000;

        for (int i = 0; i < StartOfRound.Instance.levels.Length; i++)
        {
            var moon = StartOfRound.Instance.levels[i];
            if (moon.PlanetName.Contains(name))
            {
                LethalLevelLoader.LevelManager.GetExtendedLevel(moon).IsRouteHidden = true;
            }
        }

        _name = name;
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        Plugin._instance.LogInfo($"Unlocking moon {Plugin._instance.getTerminal().terminalNodes.allKeywords[keywordIndex].compatibleNouns[_terminalIndex].noun.word}");
        Plugin._instance.getTerminal().terminalNodes.allKeywords[keywordIndex].compatibleNouns[_terminalIndex].result.itemCost = 0;
        Plugin._instance.getTerminal().terminalNodes.allKeywords[keywordIndex].compatibleNouns[_terminalIndex].result.terminalOptions[1].result
            .itemCost = 0;

        for (int i = 0; i < StartOfRound.Instance.levels.Length; i++)
        {
            var moon = StartOfRound.Instance.levels[i];
            if (moon.PlanetName.Contains(_name))
            {
                LethalLevelLoader.LevelManager.GetExtendedLevel(moon).IsRouteHidden = false;
            }
        }
        return true;
    }
}

public class StoreItems : Items
{
    private readonly int _itemsIndex;
    private readonly bool _isVehicle;
    private readonly int _normalCost;

    public StoreItems(string name, int itemsIndex, bool isVehicle = false)
    {
        Setup(name, resetAll:true);
        _itemsIndex = itemsIndex;
        _isVehicle = isVehicle;
        if (isVehicle)
        {
            _normalCost = Plugin._instance.getTerminal().buyableVehicles[_itemsIndex].creditsWorth;
            Plugin._instance.getTerminal().buyableVehicles[_itemsIndex].creditsWorth = 10000000;
        }
        else
        {
            _normalCost = Plugin._instance.getTerminal().buyableItemsList[_itemsIndex].creditsWorth;
            Plugin._instance.getTerminal().buyableItemsList[_itemsIndex].creditsWorth = 10000000;
        }
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        if (_isVehicle)
        {
            Plugin._instance.getTerminal().buyableVehicles[_itemsIndex].creditsWorth = _normalCost;
        }
        else
        {
            Plugin._instance.getTerminal().buyableItemsList[_itemsIndex].creditsWorth = _normalCost;
        }
        return true;
    }
}

public class ShipUpgrades : Items
{
    private readonly int _upgradeIndex;
    private readonly int _normalCost;

    public ShipUpgrades(string name, int upgradeIndex)
    {
        Setup(name, resetAll:true);
        _upgradeIndex = upgradeIndex;
        _normalCost = Plugin._instance.getTerminal().terminalNodes.allKeywords[0].compatibleNouns[upgradeIndex].result
            .itemCost;
        Plugin._instance.getTerminal().terminalNodes.allKeywords[0].compatibleNouns[upgradeIndex].result.itemCost =
            10000000;
    }

    protected override bool HandleReceived(bool isTick=false)
    {
        Plugin._instance.getTerminal().terminalNodes.allKeywords[0].compatibleNouns[_upgradeIndex].result.itemCost =
            _normalCost;
        return true;
    }
}

public class PlayerUpgrades : Items
{
    private readonly int _startingAmount;

    public PlayerUpgrades(string name, int startingAmount)
    {
        Setup(name, resetAll:true);
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