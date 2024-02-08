﻿using System;
using BepInEx;

namespace APLC;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("Lethal Company.exe")]
[BepInIncompatibility("LethalExpansion")]
[BepInIncompatibility("com.github.lethalmods.lethalexpansioncore")]
public class Plugin : BaseUnityPlugin
{
    //Instance of the plugin for other classes to access
    public static Plugin _instance;
    public static float carryWeight;
    public static float initialWeight;
    
    /**
     * Patches the game on startup, injecting the code into the game.
     */
    private void Awake()
    {
        if (_instance == null) _instance = this;
        
        Patches.Patch();
        LogInfo("Plugin APLC Loaded");
    }

    /**
     * Gets the terminal object for editing(needs to be here because only monobehaviors can findobjectoftype)
     */
    public Terminal getTerminal()
    {
        return FindObjectOfType<Terminal>();
    }

    /**
     * Logs a warning to the console
     */
    public void LogWarning(string message)
    {
        Logger.LogWarning(message);
    }
    
    /**
     * Logs info to the console
     */
    public void LogInfo(string message)
    {
        Logger.LogInfo(message);
    }
    
    /**
     * Logs an error to the console
     */
    public void LogError(string message)
    {
        Logger.LogError(message);
    }
}