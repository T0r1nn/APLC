using System;
using LethalAPI.LibTerminal;
using LethalAPI.LibTerminal.Attributes;
using LethalAPI.LibTerminal.Models;
using System.Collections.ObjectModel;

namespace APLC;

public class TerminalCommands
{
    public static Logic LcLogic;
    
    public static void Patch()
    {
        TerminalModRegistry commands = TerminalRegistry.CreateTerminalRegistry();
        commands.RegisterFrom<TerminalCommands>();
    }

    public static void SetLogic()
    {
        LcLogic = new Logic();
    }

    [TerminalCommand("Tracker", true), CommandInfo("Shows all logically reachable checks")]
    public string TestCommand()
    {
        if (MultiworldHandler.Instance == null) return "Not connected to AP server";

        Collection<Location> locations = LcLogic.GetAccessibleLocations();

        string terminalText = "Currently accessible locations:\n\n";

        string[] lines = new string[locations.Count];

        for (var index = 0; index < locations.Count; index++)
        {
            var location = locations[index];
            lines[index] = location.GetLocationString();
        }
        
        Array.Sort(lines);

        terminalText += string.Join('\n', lines);

        return terminalText;
    }
}