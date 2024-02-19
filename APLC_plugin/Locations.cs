using System;
using System.Linq;
using UnityEngine;

namespace APLC;

/**
 * Handles checking locations, extensions handle specific locations
 */
public abstract class Locations
{
    public string Type = "none";
    public abstract void CheckComplete();
    public abstract string GetTrackerText();
}

public class Quota: Locations
{
    private readonly int _moneyPerQuotaCheck;
    private readonly int _numQuotas;
    private int _totalQuota;
    public Quota(int moneyPerQuotaCheck, int numQuotas)
    {
        Type = "Quota";
        _moneyPerQuotaCheck = moneyPerQuotaCheck;
        _numQuotas = numQuotas;
        MultiworldHandler.Instance.GetSession().DataStorage["totalQuota"].Initialize(0);
        _totalQuota = MultiworldHandler.Instance.GetSession().DataStorage["totalQuota"];
    }

    public override string GetTrackerText()
    {
        return $"({Math.Min(_totalQuota/_moneyPerQuotaCheck, _numQuotas)}/{_numQuotas})";
    }

    public override void CheckComplete()
    {
        var quotaChecksMet = 0;
        if (!GameNetworkManager.Instance.localPlayerController.IsHost) return;
        if (!(TimeOfDay.Instance.profitQuota - TimeOfDay.Instance.quotaFulfilled <= 0f)) return;
        _totalQuota += TimeOfDay.Instance.profitQuota;
        MultiworldHandler.Instance.GetSession().DataStorage["totalQuota"] = _totalQuota;
        while ((quotaChecksMet + 1) * _moneyPerQuotaCheck <= _totalQuota && quotaChecksMet < _numQuotas)
        {
            quotaChecksMet++;
            MultiworldHandler.Instance.CompleteLocation($"Quota check {quotaChecksMet}");
        }
    }
}

public class MoonLocations : Locations
{
    private int _timesChecked;
    private readonly string _name;
    private readonly int _grade;
    private readonly int _maxChecks;

    public MoonLocations(string name, int grade, int maxChecks)
    {
        _name = name;
        _grade = grade;
        Type = "moon";
        MultiworldHandler.Instance.GetSession().DataStorage[$"{_name} checks"].Initialize(0);
        _timesChecked = MultiworldHandler.Instance.GetSession().DataStorage[$"{_name} checks"];
        _maxChecks = maxChecks;
    }

    public void OnFinishMoon(string moonName, string grade)
    {
        if (_timesChecked >= _maxChecks) return;
        var gradeNum = Array.IndexOf(new[] { "S", "A", "B", "C", "D", "F" }, grade);
        if (gradeNum > _grade) return;
        MultiworldHandler.Instance.CompleteLocation($"{_name} check {_timesChecked+1}");
        _timesChecked++;
        MultiworldHandler.Instance.GetSession().DataStorage[$"{_name} checks"] = _timesChecked;
    }
    
    public override void CheckComplete(){}

    public override string GetTrackerText()
    {
        return
            $"({_timesChecked}/{_maxChecks}) {(((MoonItems)MultiworldHandler.Instance.GetItemMap(_name)).GetTotal() > 0 ? MultiworldHandler.Instance.CheckTrophy(_name) ? "Trophy Found!" : "" : "Locked!")}";
    }
}

public class LogLocations : Locations
{
    private readonly int _logID;
    private readonly string _logName;
    
    public LogLocations(int logID, string logName)
    {
        Type = "logF";
        _logID = logID;
        _logName = logName;
    }

    public override void CheckComplete()
    {
        if (Plugin._instance.getTerminal().unlockedStoryLogs.IndexOf(_logID) != -1)
        {
            MultiworldHandler.Instance.CompleteLocation($"Log - {_logName}");
        }
    }

    public override string GetTrackerText()
    {
        return null;
    }
}

public class BestiaryLocations : Locations
{
    private readonly int _bestiaryID;
    private readonly string _bestiaryName;
    
    public BestiaryLocations(int bestiaryID, string bestiaryName)
    {
        Type = "logB";
        _bestiaryID = bestiaryID;
        _bestiaryName = bestiaryName;
    }

    public override void CheckComplete()
    {
        if (Plugin._instance.getTerminal().scannedEnemyIDs.IndexOf(_bestiaryID) != -1)
        {
            MultiworldHandler.Instance.CompleteLocation($"Bestiary Entry - {_bestiaryName}");
        }
    }

    public override string GetTrackerText()
    {
        return null;
    }
}

public class ScrapLocations : Locations
{
    private readonly string[] _scrapNames;
    private readonly string[] _checkNames;

    public ScrapLocations(string[] scrapNames, string[] checkNames)
    {
        Type = "scrap";
        _scrapNames = scrapNames;
        _checkNames = checkNames;
    }

    public override void CheckComplete()
    {
        var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
            where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
            select obj).ToList();
        foreach (var scrap in list)
        {
            for (int i = 0; i < _scrapNames.Length; i++)
            {
                if (string.Equals(scrap.itemProperties.itemName.ToLower(), _scrapNames[i].ToLower(),
                        StringComparison.Ordinal))
                {
                    MultiworldHandler.Instance.CompleteLocation($"Scrap - {_checkNames[i]}");
                }
            }
        }
    }

    public override string GetTrackerText()
    {
        return null;
    }
}