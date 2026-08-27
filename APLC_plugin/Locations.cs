using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace APLC;

public class LocationCreator
{
    public static async Task<Locations> CreateQuotaLocationAsync(int moneyPerQuotaLocation, int numQuotas)
    {
        Quota location = new(moneyPerQuotaLocation, numQuotas);
        await location.Setup();
        return location;
    }

    public static async Task<Locations> CreateMoonLocationAsync(string name, int grade, int maxLocations)
    {
        MoonLocations location = new(name, grade, maxLocations);
        await location.Setup();
        return location;
    }

    public static Locations CreateLogLocation(int logID, string logName)
    {
        LogLocations location = new(logID, logName);
        //await location.Setup();
        return location;
    }

    public static Locations CreateBestiaryLocation(int bestiaryID, string bestiaryName)
    {
        BestiaryLocations location = new(bestiaryID, bestiaryName);
        //await location.Setup();
        return location;
    }

    public static async Task<Locations> CreateScrapLocationAsync(string[] scrapNames)
    {
        ScrapLocations location = new(scrapNames);
        await location.Setup();
        return location;
    }
}

/**
 * Handles completing locations, extensions handle specific locations
 */
public abstract class Locations(string type = "none")
{
    public string Type = type;
    public abstract void LocationComplete();
    public abstract string GetTrackerText();
}

public class Quota: Locations
{
    public readonly int MoneyPerQuotaLocation;
    private readonly int _numQuotas;
    public int TotalQuota;
    public Quota(int moneyPerQuotaLocation, int numQuotas) : base("Quota")
    {
        MoneyPerQuotaLocation = moneyPerQuotaLocation;
        _numQuotas = numQuotas;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"].Initialize(0);
    }

    internal async Task Setup()
    {
        TotalQuota = await MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"].GetAsync<int>();
    }

    public override string GetTrackerText()
    {
        return $"({Math.Min(TotalQuota/MoneyPerQuotaLocation, _numQuotas)}/{_numQuotas})";
    }

    public override void LocationComplete() { }

    /** 
     * Checks if the quota has been met and increments the total money earned towards quotas.
     * If the total money earned meets the threshold for a quota location, marks the corresponding location as complete.
     */
    public void LocationComplete(int profitQuotaCompleted)
    {
        if (!GameNetworkManager.Instance.localPlayerController.IsHost) return;
        var quotaLocationsCompleted = 0;
        TotalQuota += profitQuotaCompleted;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-totalQuota"] = TotalQuota;
        while ((quotaLocationsCompleted + 1) * MoneyPerQuotaLocation <= TotalQuota && quotaLocationsCompleted < _numQuotas)
        {
            quotaLocationsCompleted++;
            SaveManager.CompleteLocation($"Quota Location {quotaLocationsCompleted}");
        }
    }
}

/**
 * Handles moon grade locations
 */
public class MoonLocations : Locations
{
    private int _gradeLocationsCompleted;
    private readonly string _name;
    private readonly int _grade;
    private readonly int _maxLocations;
    public string Name { get { return _name; } }

    public MoonLocations(string name, int grade, int maxLocations): base("moon")
    {
        _name = name;
        _grade = grade;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-{_name} Grade Locations"].Initialize(0);
        _maxLocations = maxLocations;
    }

    internal async Task Setup()
    {
        _gradeLocationsCompleted = await MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-{_name} Grade Locations"].GetAsync<int>();
    }

    /** 
     * When the ship takes off, this checks if the grade meets the requirement to mark the location as complete.
     * If it does, this marks the corresponding location as complete and increments the number of times completed.
     */
    public void OnFinishMoon(string moonName, string grade)
    {
        if (_gradeLocationsCompleted >= _maxLocations) return;
        var gradeNum = Array.IndexOf(new[] { "S", "A", "B", "C", "D", "F" }, grade);
        if (gradeNum > _grade) return;
        SaveManager.CompleteLocation($"{_name} Grade Location {_gradeLocationsCompleted+1}");
        for (int i = 1; i < _gradeLocationsCompleted + 1; i++)
        {
            long id = MultiworldHandler.Instance.GetSession().Locations
                .GetLocationIdFromName(MultiworldHandler.Instance.Game, $"{_name} Grade Location {_gradeLocationsCompleted + 1}");
            if (!MultiworldHandler.Instance.GetSession().Locations.AllLocationsChecked.Contains(id))
            {
                SaveManager.CompleteLocation($"{_name} Grade Location {_gradeLocationsCompleted+1}");
            }
        }
        _gradeLocationsCompleted++;
        MultiworldHandler.Instance.GetSession().DataStorage[$"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-{_name} Grade Locations"] = _gradeLocationsCompleted;
    }
    
    public override void LocationComplete(){}

    public override string GetTrackerText()
    {
        string[] grades = ["S", "A", "B", "C", "D", "F"];
        return
            $"({(_grade < grades.Length ? grades[_grade] : "?")} {_gradeLocationsCompleted}/{_maxLocations}) {(((MoonItems)MwState.Instance.GetItemMap(_name)).GetTotal() > 0 ? MwState.Instance.CheckTrophy(_name) ? "Trophy Found!" : "" : "Locked!")}";
    }
}

/**
 * Handles locations for story logs
 */
public class LogLocations : Locations
{
    private readonly int _logID;
    private readonly string _logName;
    
    public LogLocations(int logID, string logName) : base("logF")
    {
        _logID = logID;
        _logName = logName;
    }

    public override void LocationComplete()
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

/**
 * Handles locations for bestiary entries
 */
public class BestiaryLocations : Locations
{
    private readonly int _bestiaryID;
    private readonly string _bestiaryName;
    
    public BestiaryLocations(int bestiaryID, string bestiaryName) : base("logB")
    {
        _bestiaryID = bestiaryID;
        _bestiaryName = bestiaryName;
    }

    public override void LocationComplete()
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

/**
 * Handles locations for scrap collection if scrapsanity is enabled
 */
public class ScrapLocations : Locations
{
    private int _collectedScrap;

    public ScrapLocations(string[] scrapNames) : base("Scrap")  // note: scrapNames isn't needed at all and we can just replace the tracker part with MultiworldHandler.Instance.GetSession().Locations.AllLocationsChecked and MultiworldHandler.Instance.GetSession().Locations.AllLocationsMissing
    {
        MultiworldHandler.Instance.GetSession()
            .DataStorage[
                $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-collectedScrap"]
            .Initialize(_collectedScrap);
    }

    internal async Task Setup()
    {
        _collectedScrap = await MultiworldHandler.Instance.GetSession().DataStorage[
            $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-collectedScrap"].GetAsync<int>();
    }

    public bool CheckIfCollected(string scrapName)
    {
        return MultiworldHandler.Instance.GetSession().Locations.AllLocationsChecked.Contains(MultiworldHandler.Instance
            .GetSession().Locations.GetLocationIdFromName(MultiworldHandler.Instance.Game, $"Scrap - {scrapName}"));
    }

    /*** 
     * Checks all scrap objects in the ship and marks their corresponding locations as complete.
     * Increments the count of collected scrap for the tracker.
     */
    public override void LocationComplete()
    {
        GameObject cruiser = GameObject.FindObjectsByType<VehicleController>(sortMode: FindObjectsSortMode.None).FirstOrDefault(vehicle => vehicle.magnetedToShip)?.gameObject;
        bool hasCruiser = cruiser != null;

        var list = (from obj in StartOfRound.Instance.elevatorTransform.GetComponentsInChildren<GrabbableObject>()
                    where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                    select obj).Union(hasCruiser ? (from obj in cruiser.GetComponentsInChildren<GrabbableObject>()
                                                    where obj.name != "CompanyCruiserManual(Clone)"
                                                    select obj) : []).ToList();
        foreach (var scrap in list)
        {
            string scrapName = scrap.itemProperties.itemName;
            if (scrap.name.Contains("ap_apparatus"))
            {
                string currentMoonName = MwState.Instance.GetCurrentMoonName();
                string moonNameOnLabel = scrapName[(scrapName.IndexOf(" - ") + 3)..];
                if (currentMoonName.ToLower().Contains(moonNameOnLabel.ToLower()) || (scrap.name.Contains("ap_apparatus_custom") && !scrap.scrapPersistedThroughRounds))
                {
                    scrapName = $"AP Apparatus - {currentMoonName}";
                }
                if (scrap.scrapPersistedThroughRounds) continue;
                //scrap.itemProperties.itemName = $"AP Apparatus - {MwState.Instance.GetCurrentMoonName().ToLower()}";
                scrapName = $"AP Apparatus - {MwState.Instance.GetCurrentMoonName()}";
            }
            if (scrap.name.Contains("KiwiBabyItem"))
            {
                scrapName = "Sapsucker Egg";
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
                        _collectedScrap++;
                    }

                    MultiworldHandler.Instance.GetSession().DataStorage[
                            $"Lethal Company-{MultiworldHandler.Instance.GetSession().Players.GetPlayerName(MultiworldHandler.Instance.GetSession().ConnectionInfo.Slot)}-collectedScrap"] =
                        _collectedScrap;
                }
            }
            catch (IndexOutOfRangeException e)
            {
                Plugin.Logger.LogError($"Extra logging info: scrapName: {scrapName}, collectedScrap: {_collectedScrap}\n\n" + e.Message + "\n" + e.StackTrace);
            }
        }
    }

    public override string GetTrackerText()
    {
        return $"{_collectedScrap}/{MwState.Instance.GetScrapData().Keys.Count + StartOfRound.Instance.levels.Length - 13}";
    }
}