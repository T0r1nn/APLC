using System.Collections.Generic;
using System.Reflection;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using BepInEx;
using BepInEx.Configuration;

namespace APLC {
    public class PluginConfig
    {
        // For more info on custom configs, see https://lethal.wiki/dev/intermediate/custom-configs
        public ConfigEntry<bool> SendChatMessagesAsAPChat;
        public ConfigEntry<bool> ShowAPMessagesInChat;
        public ConfigEntry<int> MaxCharactersPerChatMessage;
        public ConfigEntry<bool> FillerTriggersInstantly;
        public ConfigEntry<bool> DisplayFillerNotification;
        public ConfigEntry<bool> DeathLink;
        public ConfigEntry<bool> OverrideMWDeathlink;
        public PluginConfig(ConfigFile cfg)
        {
            SendChatMessagesAsAPChat = cfg.Bind("Chat", "Send chat messages to Archipelago", true,
                "If true, in game chat messages will be sent to Archipelago for other players to see.");
            ShowAPMessagesInChat = cfg.Bind("Chat", "Show AP messages in chat", true,
                "If true, Archipelago will send messages into the LC chat.");
            MaxCharactersPerChatMessage = cfg.Bind("Chat", "Max characters per chat message", 50,
                "The max number of characters per chat message. Can be between 20 and 1000");
            FillerTriggersInstantly = cfg.Bind("Filler", "Filler triggers instantly", false,
                "If true, filler items will instantly trigger their effects upon receipt. Otherwise, filler items will need to be manually accepted from the terminal.");
            DisplayFillerNotification = cfg.Bind("Filler", "Display filler notifications", true,
                "If true, a notification will pop up when you land on the company building if you have any unspent filler items.");
            OverrideMWDeathlink = cfg.Bind("Death link", "Override yaml death link option", false,
                "If true, the mod config will be used to turn death link on/off instead of the yaml option.");
            DeathLink = cfg.Bind("Death link", "Enable death link", false,
                "When you die, everyone who enabled death link dies. Of course, the reverse is true too. This option does nothing if 'Override yaml death link option' is false.");


            ClearUnusedEntries(cfg);
        }

        private void ClearUnusedEntries(ConfigFile cfg) {
            // Normally, old unused config entries don't get removed, so we do it with this piece of code. Credit to Kittenji.
            PropertyInfo orphanedEntriesProp = cfg.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);
            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg, null);
            orphanedEntries.Clear(); // Clear orphaned entries (Unbound/Abandoned entries)
            cfg.Save(); // Save the config file to save these changes
        }
    }
}