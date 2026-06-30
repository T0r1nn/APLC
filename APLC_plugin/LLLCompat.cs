using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Bootstrap;
using DunGen.Graph;
using LethalLevelLoader;
using Steamworks.Ugc;

namespace APLC
{
    public static class LLLCompat
    {
        private static bool? _enabled;

        public static bool IsLethalLevelLoaderInstalled {
            get {
                if (_enabled == null) 
                {
                    _enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(LethalLevelLoader.Plugin.ModGUID);
                }
                return (bool) _enabled;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static double GetDynamicRarityForAllDungeons(Item item, SelectableLevel moon, int totalInteriorRarity)
        {
            ExtendedItem extItem = null;
            foreach (var exitem in LethalLevelLoader.PatchedContent.ExtendedItems)
            {
                if (exitem.Item.Equals(item))
                {
                    extItem = exitem;
                    break;
                }
            }

            if (extItem == null) return 0;
            double rarity = 0;
            foreach (var interior in LethalLevelLoader.DungeonManager.GetValidExtendedDungeonFlows(LethalLevelLoader.LevelManager.GetExtendedLevel(moon), false))
            {
                int scrapWeight = extItem.DungeonMatchingProperties.GetDynamicRarity(interior.extendedDungeonFlow);
                if (scrapWeight > 0) rarity += scrapWeight * ((double)interior.rarity / Math.Max(totalInteriorRarity, interior.rarity));
            }
            return rarity;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int GetRarityOfScrapForThisDungeon(Item item, SelectableLevel moon, DungeonFlow dungeon)
        {
            ExtendedItem extItem = null;
            foreach (var exitem in LethalLevelLoader.PatchedContent.ExtendedItems)
            {
                if (exitem.Item.Equals(item))
                {
                    extItem = exitem;
                    break;
                }
            }
            if (extItem == null) return 0;
            ExtendedDungeonFlowWithRarity matchingDungeon = null;
            foreach (var interior in LethalLevelLoader.DungeonManager.GetValidExtendedDungeonFlows(LethalLevelLoader.LevelManager.GetExtendedLevel(moon), false))
            {
                if (interior.extendedDungeonFlow.DungeonFlow.Equals(dungeon))
                {
                    matchingDungeon = interior;
                    break;
                }
            }
            if (matchingDungeon == null) return 0;
            return extItem.DungeonMatchingProperties.GetDynamicRarity(matchingDungeon.extendedDungeonFlow);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static bool OverrideScrapRarity(SpawnableItemWithRarity item, IEnumerable<string> moonNames, int newRarity = 30)
        {
            ExtendedItem extItem = null;
            foreach (var exitem in LethalLevelLoader.PatchedContent.ExtendedItems)
            {
                if (exitem.Item.Equals(item))
                {
                    extItem = exitem;
                    break;
                }
            }
            if (extItem == null) return false;
            LethalLevelLoader.LevelMatchingProperties newProperties = LethalLevelLoader.LevelMatchingProperties.Create(extItem);
            List<LethalLevelLoader.StringWithRarity> moonsFoundOn = new();
            foreach (var moonName in moonNames)
            {
                moonsFoundOn.Add(new LethalLevelLoader.StringWithRarity(moonName, newRarity));
            }
            newProperties.ApplyValues(newPlanetNames: moonsFoundOn);
            extItem.SetLevelMatchingProperties(newProperties);
            extItem.DungeonMatchingProperties = LethalLevelLoader.DungeonMatchingProperties.Create(extItem);
            return true;
        }
    }
}
