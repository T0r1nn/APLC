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
    public readonly int MoneyPerQuotaCheck;
    private readonly int _numQuotas;
    public int TotalQuota;
    public Quota(int moneyPerQuotaCheck, int numQuotas)
    {
        Type = "Quota";
        MoneyPerQuotaCheck = moneyPerQuotaCheck;
        _numQuotas = numQuotas;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"].Initialize(0);
        TotalQuota = MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"];
    }

    public override string GetTrackerText()
    {
        return $"({Math.Min(TotalQuota/MoneyPerQuotaCheck, _numQuotas)}/{_numQuotas})";
    }

    public override void CheckComplete()
    {
        if (!GameNetworkManager.Instance.localPlayerController.IsHost) return;
        if (!(TimeOfDay.Instance.profitQuota - TimeOfDay.Instance.quotaFulfilled <= 0f)) return;
        var quotaChecksMet = 0;
        TotalQuota += TimeOfDay.Instance.profitQuota;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"] = TotalQuota;
        while ((quotaChecksMet + 1) * MoneyPerQuotaCheck <= TotalQuota && quotaChecksMet < _numQuotas)
        {
            quotaChecksMet++;
            if (quotaChecksMet == (int)Math.Ceiling(_numQuotas / 4.0) - 1)
            {
                SaveManager.CompleteLocation("Quota 25%");
            }
            if (quotaChecksMet == (int)Math.Ceiling(_numQuotas / 2.0) - 1)
            {
                SaveManager.CompleteLocation("Quota 50%");
            }
            if (quotaChecksMet == (int)Math.Ceiling(3.0 * _numQuotas / 4.0) - 1)
            {
                SaveManager.CompleteLocation("Quota 75%");
            }
            SaveManager.CompleteLocation($"Quota check {quotaChecksMet}");
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
        SaveManager.CompleteLocation($"{_name} check {_timesChecked+1}");
        for (int i = 1; i < _timesChecked + 1; i++)
        {
            long id = MultiworldHandler.Instance.GetSession().Locations
                .GetLocationIdFromName(MultiworldHandler.Instance.Game, $"{_name} check {_timesChecked + 1}");
            if (!MultiworldHandler.Instance.GetSession().Locations.AllLocationsChecked.Contains(id))
            {
                SaveManager.CompleteLocation($"{_name} check {_timesChecked+1}");
            }
        }
        _timesChecked++;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-{_name} checks"] = _timesChecked;
    }
    
    public override void CheckComplete(){}

    public override string GetTrackerText()
    {
        return
            $"({_timesChecked}/{_maxChecks}) {(((MoonItems)MwState.Instance.GetItemMap(_name)).GetTotal() > 0 ? MwState.Instance.CheckTrophy(_name) ? "Trophy Found!" : "" : "Locked!")}";
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
        if (Plugin.Instance.GetTerminal().unlockedStoryLogs.IndexOf(_logID) != -1)
        {
            SaveManager.CompleteLocation($"Log - {_logName}");
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
        if (Plugin.Instance.GetTerminal().scannedEnemyIDs.IndexOf(_bestiaryID) != -1)
        {
            SaveManager.CompleteLocation($"Bestiary Entry - {_bestiaryName}");
        }
    }

    public override string GetTrackerText()
    {
        return null;
    }
}

public class ScrapLocations : Locations
{
    private int _checkedScrap;

    public ScrapLocations(string[] scrapNames)
    {
        Type = "scrap";
        MultiworldHandler.Instance.GetSession()
            .DataStorage[
                $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-checkedScrap"]
            .Initialize(_checkedScrap);
        _checkedScrap = MultiworldHandler.Instance.GetSession().DataStorage[
            $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-checkedScrap"];
    }

    public bool CheckCollected(string scrapName)
    {
        return MultiworldHandler.Instance.GetSession().Locations.AllLocationsChecked.Contains(MultiworldHandler.Instance
            .GetSession().Locations.GetLocationIdFromName(MultiworldHandler.Instance.Game, $"Scrap - {scrapName}"));
    }

    public override void CheckComplete()
    {
        var list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
            where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
            select obj).ToList();
        foreach (var scrap in list)
        {
            string scrapName = scrap.itemProperties.itemName;
            if (scrap.name.Contains("ap_apparatus_custom"))
            {
                scrap.itemProperties.itemName = $"AP Apparatus - {MwState.Instance.GetCurrentMoonName().ToLower()}";
                scrapName = $"AP Apparatus - {MwState.Instance.GetCurrentMoonName().ToLower()}";
            }
            try
            {
                
                if (scrap.itemProperties.isScrap)
                {
                    SaveManager.CompleteLocation($"Scrap - {scrapName}");

                    if (MultiworldHandler.Instance.GetSession().Locations
                            .GetLocationIdFromName(MultiworldHandler.Instance.Game,
                                $"Scrap - {scrapName}") != -1)
                    {
                        SaveManager.CompleteLocation(
                            $"Scrap - {scrapName}");
                        _checkedScrap++;
                    }

                    MultiworldHandler.Instance.GetSession().DataStorage[
                            $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-checkedScrap"] =
                        _checkedScrap;
                }
            }
            catch (IndexOutOfRangeException e)
            {
                Plugin.Instance.LogError($"Extra logging info: scrapName: {scrapName}, checkedScrap: {_checkedScrap}\n\n" + e.Message + "\n" + e.StackTrace);
            }
        }
    }

    public override string GetTrackerText()
    {
        return $"{_checkedScrap}/{MwState.Instance.GetScrapData().Keys.Count + StartOfRound.Instance.levels.Length - 13}";
    }
}