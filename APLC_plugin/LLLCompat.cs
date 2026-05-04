using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Bootstrap;
using Steamworks.Ugc;

namespace APLC
{
    internal class LLLCompat
    {
        public static bool IsLethalLevelLoaderInstalled => Chainloader.PluginInfos.ContainsKey(LethalLevelLoader.Plugin.ModGUID);

        public static double GetDynamicRarityForAllDungeons(Item item, SelectableLevel moon, int totalInteriorRarity)
        {
            var extItem = LethalLevelLoader.PatchedContent.ExtendedItems.Find(extItem => extItem.Item.Equals(item));
            if (extItem == null) return 0;
            double rarity = 0;
            foreach (var interior in LethalLevelLoader.DungeonManager.GetValidExtendedDungeonFlows(LethalLevelLoader.LevelManager.GetExtendedLevel(moon), false))
            {
                int scrapWeight = extItem.DungeonMatchingProperties.GetDynamicRarity(interior.extendedDungeonFlow);
                if (scrapWeight > 0) rarity += scrapWeight * ((double)interior.rarity / Math.Max(totalInteriorRarity, interior.rarity));
            }
            return rarity;
        }
    }
}
