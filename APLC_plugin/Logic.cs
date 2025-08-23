using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rule = System.Func<APLC.State, bool>;

namespace APLC;

public class Logic
{
    private readonly Region _menu = new("Menu");
    private readonly State _state;
    private readonly Region[] _moons;
    private readonly Region[] _scrap;

    public Logic()
    {
        Tuple<Item[], BuyableVehicle[], SelectableLevel[], Dictionary<string, Collection<Tuple<string, double>>>, Dictionary<string, Collection<Tuple<string, double>>>> importedLogic = Plugin.Instance.GetGameLogic();
        
        Rule canBuy = state =>
            (MultiworldHandler.Instance.GetSlotSetting("randomizeterminal") == 0 || state.Has("Terminal")) &&
            (MultiworldHandler.Instance.GetSlotSetting("randomizecompany") == 0 || state.Has("Company"));
        _state = new State(MwState.Instance);
        Region startingMoon = new Region(MwState.Instance.GetStartingMoon());
        _menu.AddConnection(startingMoon, state => true);
        Region terminal = new Region("Terminal");
        _menu.AddConnection(terminal, state => MultiworldHandler.Instance.GetSlotSetting("randomizeterminal")==0 || state.Has("Terminal"));
        Region companyBuilding = new Region("Company Building");
        _moons = new Region[importedLogic.Item3.Length];
        _scrap = new Region[importedLogic.Item5.Count];
        
        terminal.AddConnection(companyBuilding, state => MultiworldHandler.Instance.GetSlotSetting("randomizecompany")==0 || state.Has("Company"));

        for (var i = 0; i < MultiworldHandler.Instance.GetSlotSetting("numQuota"); i++)
        {
            companyBuilding.AddLocation(new Location($"Quota check {i+1}", state=>state.Has("Stamina Bar") && state.Has("Inventory Slot", 2)));
        }

        string[] moonNames = new string[importedLogic.Item3.Length];
        for (int i = 0; i < importedLogic.Item3.Length; i++)
        {
            moonNames[i] = importedLogic.Item3[i].PlanetName;
        }
        for (var index = 0; index < moonNames.Length; index++)
        {
            var moonName = moonNames[index];
            Plugin.Instance.LogWarning(moonName);
            if (moonName == startingMoon.GetName())
            {
                _moons[index] = startingMoon;
            }
            else
            {
                _moons[index] = new Region(moonName);
                terminal.AddConnection(_moons[index], state => state.Has(moonName));
            }

            for (var i = 0; i < MultiworldHandler.Instance.GetSlotSetting("checksPerMoon"); i++)
            {
                _moons[index].AddLocation(new Location($"{moonName} check {i + 1}",
                    state => state.Has("Inventory Slot", 2) && state.Has("Stamina Bar", 1)));
            }
        }

        for (var index = 0; index < moonNames.Length; index++)
        {
            var moon = _moons[index];
            var moonName = moon.GetName();
            switch (moonName)
            {
                case "Experimentation":
                    moon.AddLocation("Log - Swing of Things");
                    moon.AddLocation(new Location("Log - Autopilot", state=>state.Has("Stamina Bar", 2)));  // this might have been renamed to Log - Autopilot
                    break;
                case "Assurance":
                    moon.AddLocation("Log - Mummy");     // this might have been renamed to Log - Mummy
                    break;
                case "Vow":
                    moon.AddLocation("Log - Screams");
                    break;
                case "March":
                    moon.AddLocation("Log - Goodbye");
                    break;
                case "Adamance":
                    moon.AddLocation("Log - Team Synergy");
                    break;
                case "Rend":
                    moon.AddLocation("Log - Golden Planet");
                    moon.AddLocation("Log - Idea");
                    moon.AddLocation("Log - Nonsense");
                    break;
                case "Dine":
                    moon.AddLocation("Log - Hiding");
                    break;
                case "Titan":
                    moon.AddLocation(new Location("Log - Real job", state => state.HasAny("Jetpack", "Extension ladder") && canBuy(state)));
                    moon.AddLocation(new Location("Log - Desmond", state => state.Has("Jetpack") && canBuy(state)));
                    break;
                case "Artifice":
                    moon.AddLocation("Log - Letter of Resignation");
                    break;
            }
        }

        string[] scrapNames = MultiworldHandler.Instance.GetSlotSetting("fixscrapsanity") == 1
            ? MultiworldHandler.Instance.GetScrapToMoonMap().Keys.ToArray()
            : importedLogic.Item5.Keys.ToArray();

        Dictionary<string, Collection<Tuple<string, double>>> scrapMoons = importedLogic.Item5;

        Dictionary<string, string[]> scrapMoonsAlt = MultiworldHandler.Instance.GetSlotSetting("fixscrapsanity") == 1
            ? MultiworldHandler.Instance.GetScrapToMoonMap()
            : new Dictionary<string, string[]>();

        Dictionary<string, Collection<Tuple<string, double>>> bestiaryMoons = importedLogic.Item4;

        foreach (string key in bestiaryMoons.Keys)
        {
            Region entry = new Region(key);
            entry.AddLocation($"Bestiary Entry - {key}");
            Collection<string> allowed = new Collection<string>();
            foreach (string moon in moonNames)
            {
                foreach (Tuple<string, double> moonRarity in bestiaryMoons[key])
                {
                    if (moonRarity.Item2 > MultiworldHandler.Instance.GetSlotSetting("minmonsterchance", 5)/100f && moonRarity.Item1.Contains(moon))
                    {
                        foreach (var moonRegion in _moons)
                        {
                            if (moonRegion.GetName() == moon)
                            {
                                moonRegion.AddConnection(entry, state=>state.Has("Scanner"));
                            }
                        }
                    }
                }
            }
        }

        Dictionary<string, Region> scrapRegionMap = new Dictionary<string, Region>();
        foreach (string scrap in scrapNames)
        {
            Region scrapRegion = new Region(scrap);
            if (scrap is "Shotgun" or "Kitchen knife")
            {
                scrapRegion.AddLocation(new Location($"Scrap - {scrap}", state=>canBuy(state)&&state.Has("Shovel")));
            }
            else
            {
                scrapRegion.AddLocation($"Scrap - {scrap}");
            }

            scrapRegionMap.Add(scrap, scrapRegion);
        }

        string[] vanillaMoons =
        [
            "experimentation", "assurance", "vow", "offense", "march", "adamance", "embrion", "rend", "dine", "titan",
            "artifice", "liquidation", "gordion"
        ];

        foreach (var moon in StartOfRound.Instance.levels)
        {
            if (moon.PlanetName.Contains("Gordion") || moon.PlanetName.Contains("Liquidation")) continue;
            var vanilla = vanillaMoons.Any(vanillaMoon => moon.PlanetName.ToLower().Contains(vanillaMoon));
            if (vanilla) continue;
            var scrapName = $"AP Apparatus - {moon}";
                
            var scrapRegion = new Region(scrapName);
            scrapRegion.AddLocation(scrapName);
            scrapRegionMap.Add($"Scrap - {scrapName}", scrapRegion);
        }

        _scrap = scrapRegionMap.Values.ToArray();

        if (MultiworldHandler.Instance.GetSlotSetting("scrapsanity") == 1)
        {
            if (MultiworldHandler.Instance.GetSlotSetting("fixscrapsanity") == 1)
            {
                foreach (string scrapName in scrapMoonsAlt.Keys)
                {
                    foreach (string moon in scrapMoonsAlt[scrapName])
                    {
                        foreach (Region moonRegion in _moons)
                        {
                            if (moonRegion.GetName() == moon || moon == "Common")
                            {
                                moonRegion.AddConnection(scrapRegionMap[scrapName], state => state.Has("Stamina Bar"));
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (string scrapName in scrapMoons.Keys)
                {
                    foreach (Tuple<string, double> data in scrapMoons[scrapName])
                    {
                        foreach (Region moonRegion in _moons)
                        {
                            if (data.Item1.Contains(moonRegion.GetName()) && data.Item2 >
                                MultiworldHandler.Instance.GetSlotSetting("minscrapchance", 3) / 100f)
                            {
                                moonRegion.AddConnection(scrapRegionMap[scrapName], state => state.Has("Stamina Bar"));
                            }
                        }
                    }
                }
            }
        }

        foreach (var moon in StartOfRound.Instance.levels)
        {
            var moonName = moon.PlanetName;
            if (moonName.Contains("Gordion") || moonName.Contains("Liquidation")) continue;
            var moonRegion = GetMoonRegion(moonName);
            if (moonRegion == null)
            {
                Plugin.Instance.LogError($"Region for moon {moonName} was null");
                continue;
            }

            try
            {
                if (!vanillaMoons.Any(vanillaMoon => moonName.ToLower().Contains(vanillaMoon)))
                {
                    moonRegion.AddConnection(GetScrapRegion($"AP Apparatus - {moonName}"),
                        state => state.Has("Stamina Bar"));
                }

                for (var index = 0; index < moonRegion.GetConnections().Count; index++)
                {
                    if (moonRegion.GetConnections()[index].GetExit().GetName().Contains("AP Apparatus - Custom"))
                    {
                        moonRegion.GetConnections().Remove(moonRegion.GetConnections()[index]);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.LogError(ex.Message);
                Plugin.Instance.LogError(ex.StackTrace);
                Plugin.Instance.LogError($"The above error happened as a result of the moon {moonName}");
            }
        }
    }

    public Region GetMoonRegion(string moonName)
    {
        foreach (Region region in _moons)
        {
            if (region.GetName().ToLower().Contains(moonName.ToLower()))
            {
                return region;
            }
        }

        return null;
    }

    public Region[] GetMoonRegions()
    {
        return _moons;
    }
    
    public Region GetScrapRegion(string scrapName)
    {
        foreach (Region region in _scrap)
        {
            if (region.GetName().ToLower().Contains(scrapName.ToLower()))
            {
                return region;
            }
        }

        return null;
    }

    public Collection<Location> GetAccessibleLocations()
    {
        Collection<Location> locations = new Collection<Location>();

        Collection<Region> openRegions = new Collection<Region>();
        Collection<Region> closedRegions = new Collection<Region>();
        
        openRegions.Add(_menu);

        while (openRegions.Count > 0)
        {
            foreach (Location location in openRegions[0].GetLocations())
            {
                location.SetAccessible(true);
                if (location.CheckAccessible(_state) && !locations.Contains(location) && !MultiworldHandler.Instance.CheckComplete(location.GetLocationString()))
                {
                    locations.Add(location);
                }
            }
            Collection<Connection> connections = openRegions[0].GetConnections();
            closedRegions.Add(openRegions[0]);
            openRegions.RemoveAt(0);
            foreach (var connection in connections)
            {
                connection.SetAccessible(true);
                if (connection.CheckAccessible(_state))
                {
                    if (!closedRegions.Contains(connection.GetExit()) && !openRegions.Contains(connection.GetExit()))
                    {
                        openRegions.Add(connection.GetExit());
                    }
                }
            }
        }
        
        return locations;
    }
    
    public Collection<Region> GetAccessibleRegions()
    {
        Collection<Region> openRegions = new Collection<Region>();
        Collection<Region> closedRegions = new Collection<Region>();
        
        openRegions.Add(_menu);

        while (openRegions.Count > 0)
        {
            foreach (Location location in openRegions[0].GetLocations())
            {
                location.SetAccessible(true);
            }
            Collection<Connection> connections = openRegions[0].GetConnections();
            closedRegions.Add(openRegions[0]);
            openRegions.RemoveAt(0);
            foreach (var connection in connections)
            {
                connection.SetAccessible(true);
                if (connection.CheckAccessible(_state))
                {
                    if (!closedRegions.Contains(connection.GetExit()) && !openRegions.Contains(connection.GetExit()))
                    {
                        openRegions.Add(connection.GetExit());
                    }
                }
            }
        }
        
        return closedRegions;
    }
}

public class Region
{
    private string _name;
    private readonly Collection<Connection> _connections = new();
    private readonly Collection<Location> _locations = new();
    public Region(string name)
    {
        _name = name;
    }

    public void AddConnection(Region b, Rule rule)
    {
        Connection connection = new Connection(this, b, rule);
        _connections.Add(connection);
    }

    public void AddLocation(string name)
    {
        _locations.Add(new Location(name));
    }

    public void AddLocation(Location location)
    {
        _locations.Add(location);
    }

    public Collection<Connection> GetConnections()
    {
        return _connections;
    }
    
    public Collection<Location> GetLocations()
    {
        return _locations;
    }

    public string GetName()
    {
        return _name;
    }
}

public class Location
{
    private readonly string _name;
    private bool _accessible;
    private Rule _rule;

    public Location(string name)
    {
        _name = name;
        _rule = state => true;
    }

    public Location(string name, Rule rule)
    {
        _name = name;
        _rule = rule;
    }

    public void SetAccessible(bool accessible)
    {
        _accessible = accessible;
    }

    public bool CheckAccessible(State state)
    {
        return _accessible && _rule(state);
    }

    public string GetLocationString()
    {
        return _name;
    }
}

public class Connection
{
    private readonly Region _a;
    private readonly Region _b;
    private readonly Rule _rule;
    private bool _accessible;
    public Connection(Region a, Region b, Rule rule)
    {
        _a = a;
        _b = b;
        _rule = rule;
    }

    public bool CheckAccessible(State state)
    {
        return _accessible && _rule(state);
    }

    public void SetAccessible(bool accessible)
    {
        _accessible = accessible;
    }

    public Region GetEntrance()
    {
        return _a;
    }

    public Region GetExit()
    {
        return _b;
    }
}

public class State
{
    private readonly MwState _mwState;
    public State(MwState state)
    {
        _mwState = state;
    }
    
    public bool HasAny(params string[] items){
        foreach (string item in items)
        {
            Items itemObject = _mwState.GetItemMap(item);
            if (itemObject is PlayerUpgrades upgradeItem)
            {
                if (upgradeItem.GetNum() >= 1)
                {
                    return true;
                }
            }
            else if (itemObject.GetTotal() >= 1)
            {
                return true;
            }
        }

        return false;
    }
    
    public bool HasAll(params string[] items){
        foreach (string item in items)
        {
            Items itemObject = _mwState.GetItemMap(item);
            if (itemObject is PlayerUpgrades upgradeItem)
            {
                if (upgradeItem.GetNum() == 0)
                {
                    return false;
                }
            }
            else if (itemObject.GetTotal() == 0)
            {
                return false;
            }
        }

        return true;
    }

    public bool Has(string item, int count = 1)
    {
        Items itemObject = _mwState.GetItemMap(item);
        if (itemObject is PlayerUpgrades upgradeItem)
        {
            if (upgradeItem.GetNum() < count)
            {
                return false;
            }
        }
        else if (itemObject.GetTotal() < count)
        {
            return false;
        }

        return true;
    }
}