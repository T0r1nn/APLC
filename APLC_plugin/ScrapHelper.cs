using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dawn;
using UnityEngine;
using UnityEngine.UIElements.Collections;

namespace APLC
{
    internal class ScrapHelper
    {

        public static int GetRarityOfThisScrap(Item scrapItem, SpawnWeightContext context)
        {
            DawnItemInfo itemInfo = scrapItem.GetDawnInfo();
            if (itemInfo.ScrapInfo != null)
            {
                return itemInfo.ScrapInfo.Weights.GetFor(context.Moon, context) ?? 0;
            }
            if (LLLCompat.IsLethalLevelLoaderInstalled)
                return LLLCompat.GetRarityOfScrapForThisDungeon(scrapItem, context.Moon.Level, context.Dungeon.DungeonFlow);

            return 0;
        }

        public static void AddScrapSpawnProbabilitiesForThisMoon(SelectableLevel moon,
            ref Dictionary<string, Collection<ValueTuple<string, double>>> scrapMap)
        {
            if (!moon.spawnEnemiesAndScrap || moon.PlanetName.Contains("Liquidation")) return;

            DawnMoonInfo moonInfo = moon.GetDawnInfo(); // can use Item.spawnPrefab.GetComponent<GrabbableObject>().GetType() to check if the item is a LungProp

            // begin scrap --------------------------------------------------------------------------------------------------------------

            //Dictionary<string, double> scrapRarityDict = [];
            Dictionary<DawnDungeonInfo, int> totalExtraRaritiesForInteriors = [];

            var scrap = moon.spawnableScrap;
            List<Item> scrapFoundHere = [];
            int totalRarity = 0;
            SpawnWeightContext blankContext = new(moonInfo, null, null);
            var interiors = RoundManager.Instance.dungeonFlowTypes.Where(flow => flow.dungeonFlow.GetDawnInfo().Weights.GetFor(moonInfo, ctx: blankContext) > 0).Select(flow => flow.dungeonFlow.GetDawnInfo());
            int totalInteriorRarity = 0;
            foreach (var interior in interiors)
            {
                int spawnWeight = interior.Weights.GetFor(moonInfo, ctx: blankContext) ?? 0;    // this might not work for LLL interiors?
                totalInteriorRarity += spawnWeight;
                totalExtraRaritiesForInteriors.Add(interior, 0);
            }

            foreach (var item in scrap)
            {
                if (!item.spawnableItem.isScrap) continue;
                if (item.rarity > 0)
                {
                    totalRarity += item.rarity;
                    scrapFoundHere.Add(item.spawnableItem);
                    continue;
                }
                DawnItemInfo itemInfo = item.spawnableItem.GetDawnInfo();
                int rarity = itemInfo.ScrapInfo.Weights.GetFor(moonInfo, blankContext) ?? 0;
                bool foundSomewhere = false;
                if (rarity > 0)
                {
                    totalRarity += rarity;
                    foundSomewhere = true;
                }
                // Dawn scrap that don't have an interior requirement will show their normal weight for all interiors
                foreach (var interiorInfo in interiors)
                {
                    int interiorScrapWeight = itemInfo.ScrapInfo.Weights.GetFor(moonInfo, new SpawnWeightContext(moonInfo, interiorInfo, null)) ?? 0;
                    if (interiorScrapWeight > rarity)
                    {
                        totalExtraRaritiesForInteriors[interiorInfo] += interiorScrapWeight - rarity;
                        foundSomewhere = true;
                    }
                    //rarity += scrapWeight ?? 0 * (int)interiorInfo.Weights.GetFor(moonInfo, ctx: blankContext) / (float)totalInteriorRarity;
                }
                if (foundSomewhere) scrapFoundHere.Add(item.spawnableItem);
            }

            if (LLLCompat.IsLethalLevelLoaderInstalled)
            {
                foreach (Item item in LethalContent.Items.Values.Where(item => item.ShopInfo == null && item.ScrapInfo == null && item.Item.isScrap).Select(itemInfo => itemInfo.Item))
                {
                    bool foundSomewhere = false;
                    foreach (var interiorInfo in interiors)
                    {
                        int interiorScrapWeight = LLLCompat.GetRarityOfScrapForThisDungeon(item, moon, interiorInfo.DungeonFlow);
                        if (interiorScrapWeight > 0)
                        {
                            totalExtraRaritiesForInteriors[interiorInfo] += interiorScrapWeight;
                            foundSomewhere = true;
                        }
                    }
                    if (foundSomewhere) scrapFoundHere.Add(item);
                }
            }

            foreach (Item scrapItem in scrapFoundHere)
            {
                string itemName = scrapItem.itemName;
                if (itemName.Contains("AP Apparatus - ") &&
                    moon.PlanetName.Contains(
                        itemName[new Range(15, itemName.Length)]))
                {
                    if (itemName.Contains("Adamance"))   // Adamance has two apparatuses, and the second one seems to replace the one in moon.spawnableScrap.
                                                         // This is an expensive and hacky way to fix it, but I don't have a better one right now
                    {
                        var spawningItems = Resources.FindObjectsOfTypeAll<Item>().Where(item => item.spawnPrefab && item.itemName.Contains("AP Apparatus - Adamance")).ToHashSet();

                        foreach (var appy in spawningItems)
                        {
                            appy.itemName = $"AP Apparatus - {moon.PlanetName}";
                        }
                    }
                    itemName = $"AP Apparatus - {moon.PlanetName}";
                }
                else if (itemName.Contains("AP Apparatus - ") && !itemName.Contains("Custom"))
                {
                    continue;
                }

                string scrapName = itemName.Equals("AP Apparatus - Custom") ? $"AP Apparatus - {moon.PlanetName}" : itemName;
                scrapMap.TryAdd(scrapName, new Collection<ValueTuple<string, double>>());
                var checkMoons = scrapMap.Get(scrapName);
                double probOfAtLeastOne = GetProbOfAtLeastOneScrap(scrapItem, moonInfo, totalRarity, totalInteriorRarity, totalExtraRaritiesForInteriors);
                bool existsAlready = false;

                for (var index = 0; index < checkMoons.Count; index++)
                {
                    var entry = checkMoons[index];
                    if (entry.Item1 == moon.PlanetName)
                    {
                        checkMoons[index] = new ValueTuple<string, double>(entry.Item1, entry.Item2 + probOfAtLeastOne);
                        existsAlready = true;
                    }
                }

                if (!existsAlready)
                {
                    scrapMap.Get(scrapName).Add(new ValueTuple<string, double>(moon.PlanetName, probOfAtLeastOne));
                }
            }

            // end scrap -------------------------------------------------------------------------------------------------------------

            int totalIntRarity = 0;
            int facilityRarity = 0;
            foreach (var interior in moon.dungeonFlowTypes)
            {
                totalIntRarity += interior.rarity;
                if (interior.id == 0 || interior.id == 3)
                {
                    facilityRarity = interior.rarity;
                }
            }

            if (Double.IsNaN((double)facilityRarity / totalIntRarity))
            {
                totalIntRarity = 1;
            }
            scrapMap.Get("Apparatus").Add(new ValueTuple<string, double>(moon.PlanetName, (double)facilityRarity / totalIntRarity));
        }

        internal static double GetProbOfAtLeastOneScrap(Item scrapItem, DawnMoonInfo moonInfo, int totalRegularScrapRarity, int totalInteriorRarity, Dictionary<DawnDungeonInfo, int> interiorExtraRarityMap)
        {
            double probOfAtLeastOne = 0;    // this is our goal for each scrap

            SpawnWeightContext blankContext = new(moonInfo, null, null);

            foreach (var (interiorInfo, interiorExtraScrapRarity) in interiorExtraRarityMap)
            {
                SpawnWeightContext context = new SpawnWeightContext(moonInfo, interiorInfo, null);

                int scrapRarityForThisInterior = ScrapHelper.GetRarityOfThisScrap(scrapItem, context);

                double spawnChanceOfThisInterior = (interiorInfo.Weights.GetFor(moonInfo, ctx: blankContext) ?? 0) / (double)totalInteriorRarity;
                int totalExtraScrapRarityForThisInterior = interiorExtraScrapRarity;

                int totalScrapRarityForThisInterior = totalRegularScrapRarity + totalExtraScrapRarityForThisInterior;

                double probNotSpawned = (totalScrapRarityForThisInterior - scrapRarityForThisInterior) / (double)totalScrapRarityForThisInterior;

                probOfAtLeastOne += (1 - Math.Pow(probNotSpawned, moonInfo.Level.minScrap)) * spawnChanceOfThisInterior;
#if DEBUG
            if (scrapItem.itemName.Contains("Large axle"))
            {
                Plugin.Logger.LogInfo($"Info for {scrapItem.itemName} in {interiorInfo.DungeonFlow.name} on {moonInfo.Level.PlanetName}: \n\tprobOfAtLeastOne-{probOfAtLeastOne} \n\tprobNotSpawned-{probNotSpawned} \n\tspawnChanceOfThisInterior-{spawnChanceOfThisInterior}");
            }
#endif
            }
            return probOfAtLeastOne;
        }

    }
}
