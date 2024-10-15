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
    public readonly int _moneyPerQuotaCheck;
    private readonly int _numQuotas;
    public int _totalQuota;
    public Quota(int moneyPerQuotaCheck, int numQuotas)
    {
        Type = "Quota";
        _moneyPerQuotaCheck = moneyPerQuotaCheck;
        _numQuotas = numQuotas;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"].Initialize(0);
        _totalQuota = MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"];
    }

    public override string GetTrackerText()
    {
        return $"({Math.Min(_totalQuota/_moneyPerQuotaCheck, _numQuotas)}/{_numQuotas})";
    }

    public override void CheckComplete()
    {
        if (!GameNetworkManager.Instance.localPlayerController.IsHost) return;
        if (!(TimeOfDay.Instance.profitQuota - TimeOfDay.Instance.quotaFulfilled <= 0f)) return;
        var quotaChecksMet = 0;
        _totalQuota += TimeOfDay.Instance.profitQuota;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"] = _totalQuota;
        while ((quotaChecksMet + 1) * _moneyPerQuotaCheck <= _totalQuota && quotaChecksMet < _numQuotas)
        {
            quotaChecksMet++;
            if (quotaChecksMet == (int)Math.Ceiling(_numQuotas / 4.0) - 1)
            {
                MultiworldHandler.Instance.CompleteLocation("Quota 25%");
            }
            if (quotaChecksMet == (int)Math.Ceiling(_numQuotas / 2.0) - 1)
            {
                MultiworldHandler.Instance.CompleteLocation("Quota 50%");
            }
            if (quotaChecksMet == (int)Math.Ceiling(3.0 * _numQuotas / 4.0) - 1)
            {
                MultiworldHandler.Instance.CompleteLocation("Quota 75%");
            }
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
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-{_name} checks"].Initialize(0);
        _timesChecked = MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-{_name} checks"];
        _maxChecks = maxChecks;
    }

    public void OnFinishMoon(string moonName, string grade)
    {
        if (_timesChecked >= _maxChecks) return;
        var gradeNum = Array.IndexOf(new[] { "S", "A", "B", "C", "D", "F" }, grade);
        if (gradeNum > _grade) return;
        MultiworldHandler.Instance.CompleteLocation($"{_name} check {_timesChecked+1}");
        for (int i = 1; i < _timesChecked + 1; i++)
        {
            long id = MultiworldHandler.Instance.GetSession().Locations
                .GetLocationIdFromName("Lethal Company", $"{_name} check {_timesChecked + 1}");
            if (!MultiworldHandler.Instance.GetSession().Locations.AllLocationsChecked.Contains(id))
            {
                MultiworldHandler.Instance.CompleteLocation($"{_name} check {_timesChecked+1}");
            }
        }
        _timesChecked++;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-{_name} checks"] = _timesChecked;
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
    public readonly bool[] checkedScrap;
    private readonly string[] checkNames =
    {
        "Airhorn", "Apparatice", "Bee Hive", "Big bolt", "Bottles", "Brass bell", "Candy", "Cash register",
        "Chemical jug", "Clown horn", "Coffee mug", "Comedy", "Cookie mold pan", "DIY-Flashbang", "Double-barrel", "Dust pan",
        "Egg beater", "Fancy lamp", "Flask", "Gift Box", "Gold bar", "Golden cup", "Hair brush", "Hairdryer",
        "Jar of pickles", "Large axle", "Laser pointer", "Magic 7 ball", "Magnifying glass", "Old phone",
        "Painting", "Perfume bottle", "Pill bottle", "Plastic fish", "Red soda", "Remote", "Ring", "Robot toy",
        "Rubber Ducky", "Steering wheel", "Stop sign", "Tattered metal sheet", "Tea kettle", "Teeth", "Toothpaste",
        "Toy cube", "Tragedy", "V-type engine", "Whoopie-Cushion", "Yield sign"
    };

    private readonly string[] scrapNames =
    {
        "Airhorn", "Apparatus", "Hive", "Big bolt", "Bottles", "Bell", "Candy", "Cash register",
        "Chemical jug", "Clown horn", "Mug", "Comedy", "Cookie mold pan", "Homemade flashbang", "Shotgun", "Dust pan",
        "Egg beater", "Fancy lamp", "Flask", "Gift", "Gold bar", "Golden cup", "Brush", "Hairdryer",
        "Jar of pickles", "Large axle", "Laser pointer", "Magic 7 ball", "Magnifying glass", "Old phone",
        "Painting", "Perfume bottle", "Pill bottle", "Plastic fish", "Red soda", "Remote", "Ring", "Toy robot",
        "Rubber Ducky", "Steering wheel", "Stop sign", "Metal sheet", "Tea kettle", "Teeth", "Toothpaste",
        "Toy cube", "Tragedy", "V-type engine", "Whoopie cushion", "Yield sign"
    };

    public ScrapLocations(string[] scrapNames, string[] checkNames)
    {
        Type = "scrap";
        _scrapNames = scrapNames;
        _checkNames = checkNames;
        checkedScrap = new bool[_checkNames.Length];
        MultiworldHandler.Instance.GetSession()
            .DataStorage[
                $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-checkedScrap"]
            .Initialize(checkedScrap);
        checkedScrap = MultiworldHandler.Instance.GetSession().DataStorage[
            $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-checkedScrap"];
    }

    public bool CheckCollected(string scrapName)
    {
        return checkedScrap[Array.IndexOf(_scrapNames, scrapName)];
    }

    public override void CheckComplete()
    {
        var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
            where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
            select obj).ToList();
        foreach (var scrap in list)
        {
            string name = scrap.itemProperties.itemName.ToLower();
            if (scrap.name.Contains("ap_apparatus_custom"))
            {
                name = "ap apparatus - custom";
            }
            if (name == "ap apparatus - custom")
            {
                name = $"ap apparatus - {MultiworldHandler.Instance.GetCurrentMoonName().ToLower()}";
            }
            for (int i = 0; i < _scrapNames.Length; i++)
            {
                try
                {
                    if (string.Equals(scrap.itemProperties.itemName.ToLower(), _scrapNames[i].ToLower(),
                            StringComparison.Ordinal))
                    {
                        MultiworldHandler.Instance.CompleteLocation($"Scrap - {_checkNames[i]}");

                        if (scrapNames.Contains(_scrapNames[i]) && MultiworldHandler.Instance.GetSession().Locations
                                .GetLocationIdFromName("Lethal Company",
                                    $"Scrap - {checkNames[Array.IndexOf(scrapNames, _scrapNames[i])]}") != -1)
                        {
                            MultiworldHandler.Instance.CompleteLocation(
                                $"Scrap - {checkNames[Array.IndexOf(scrapNames, _scrapNames[i])]}");
                        }

                        checkedScrap[i] = true;
                        MultiworldHandler.Instance.GetSession().DataStorage[
                                $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-checkedScrap"] =
                            checkedScrap;
                    }
                }
                catch (IndexOutOfRangeException e)
                {
                    Plugin._instance.LogError($"Extra logging info: i: {i}, _scrapNames.Length: {_scrapNames.Length}, _checkNames.Length: {_checkNames.Length}, checkedScrap.Length: {checkedScrap.Length}, scrap: {scrap.itemProperties.itemName}\n\n" + e.Message + "\n" + e.StackTrace);
                }
            }
        }
    }

    public override string GetTrackerText()
    {
        int count = 0;
        foreach (var check in checkedScrap)
        {
            if (check)
            {
                count++;
            }
        }
        return $"{count}/{checkedScrap.Length}";
    }
}