using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace APLC
{
    internal class Unit_Tests
    {
        internal static void Run_All_Tests()
        {
            StringBuilder results = new StringBuilder();
            int passed = 0;
            int failed = 0;
            void RunTest(string testName, Func<bool> testFunc)
            {
                try
                {
                    if (testFunc())
                    {
                        results.AppendLine($"[PASS] {testName}");
                        passed++;
                    }
                    else
                    {
                        results.AppendLine($"[FAIL] {testName}");
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    results.AppendLine($"[ERROR] {testName} - Exception: {ex.Message}");
                    failed++;
                }
            }
            // Add tests here
            RunTest("Terminal Command Tests", TestTerminalCommands);
            RunTest("Logic String Tests", TestLogicString);
            RunTest("Trophy Goal Tests", TestTrophyGoal);
            results.AppendLine($"Total Tests: {passed + failed}, Passed: {passed}, Failed: {failed}");
            Plugin.Instance.LogInfo(results.ToString());
        }
        private static bool TestTerminalCommands()
        {
            // Example test logic
            TerminalCommands testCommandStructure = new();
                testCommandStructure.TrackerCommand();
                testCommandStructure.ProgressCommand();
                testCommandStructure.ScrapCommand(Plugin.Instance.GetTerminal(), "Gold bar");
                testCommandStructure.HintsCommand();
                testCommandStructure.ConfigCommand();
                //testCommandStructure.ConfigSetCommand(Plugin.Instance.GetTerminal(), "recapchat true");
                testCommandStructure.FillerCommand(Plugin.Instance.GetTerminal());
            return true; // Test passes
        }
        private static bool TestLogicString()
        {
            var logic = Plugin.Instance.GetGameLogic();
            Dictionary<string, Collection<Tuple<string, double>>> scrapList = logic.Item5;
            foreach (var scrap in scrapList.Keys)
            {
                // check that no weights are NaN or negative
                foreach (var tuple in scrapList[scrap])
                {
                    if (double.IsNaN(tuple.Item2) || tuple.Item2 < 0)
                    {
                        Plugin.Instance.LogError($"TestLogicString failed: Scrap item '{scrap}' has invalid weight {tuple.Item2} for '{tuple.Item1}'");
                        return false;
                    }
                }
            }
            Dictionary<string, Collection<Tuple<string, double>>> bestiaryList = logic.Item4;
            foreach (var monster in bestiaryList.Keys)
            {
                // check that no weights are NaN or negative
                foreach (var tuple in scrapList[monster])
                {
                    if (double.IsNaN(tuple.Item2) || tuple.Item2 < 0)
                    {
                        Plugin.Instance.LogError($"TestLogicString failed: Bestiary entity '{monster}' has invalid weight {tuple.Item2} for '{tuple.Item1}'");
                        return false;
                    }
                }
            }

            return true;
        }
        private static bool TestTrophyGoal()
        {
            if (MultiworldHandler.Instance == null) return false;
            object[] trophies;
            string[] validLevels = [.. StartOfRound.Instance.levels.Where(level => !level.PlanetName.Contains("Gordion") && !level.PlanetName.Contains("Liquidation")).Select(
                level => level.PlanetName.ToLower())];
            MwState.Instance.ResetTrophyList();
            // test 1: every vanilla moon gets its trophy completed
            foreach (string moon in validLevels)
            {
                MwState.Instance.CompleteTrophy(moon, new PhysicsProp { scrapPersistedThroughRounds = false });
            }
            trophies = MwState.Instance.GetTrophyList();
            foreach (var moon in validLevels){
                if (!trophies.Contains(moon)) {
                    Plugin.Instance.LogError($"TrophyGoalTest1 failed: Missing trophy for moon '{moon}'");
                    return false;
                }
            }
            MwState.Instance.ResetTrophyList();

            // test 2: every moon gets its trophy completed, but one of the trophies has a duplicate that persisted through rounds and one that didn't
            for (int i = 0; i < 4; i++)
            {
                MwState.Instance.CompleteTrophy(validLevels[i], new PhysicsProp { scrapPersistedThroughRounds = false });
            }
            MwState.Instance.CompleteTrophy(validLevels[3], new PhysicsProp { scrapPersistedThroughRounds = true });
            MwState.Instance.CompleteTrophy(validLevels[3], new PhysicsProp { scrapPersistedThroughRounds = false });
            for (int i = 4; i < validLevels.Length; i++)
            {
                MwState.Instance.CompleteTrophy(validLevels[i], new PhysicsProp { scrapPersistedThroughRounds = false });
            }
            trophies = MwState.Instance.GetTrophyList();
            foreach (var moon in validLevels)
            {
                if (!trophies.Contains(moon))
                {
                    Plugin.Instance.LogError($"TrophyGoalTest2 failed: Missing trophy for moon '{moon}'");
                    return false;
                }
            }
            MwState.Instance.ResetTrophyList();

            // test 3: every moon gets its trophy completed, but two of them are custom trophies and one persisted through rounds
            for (int i = 0; i < 3; i++)
            {
                MwState.Instance.CompleteTrophy(validLevels[i], new PhysicsProp { scrapPersistedThroughRounds = false });
            }
            MwState.Instance.CompleteTrophy("custom", new PhysicsProp { scrapPersistedThroughRounds = false });
            MwState.Instance.CompleteTrophy("custom", new PhysicsProp { scrapPersistedThroughRounds = true });
            for (int i = 3; i < validLevels.Length; i++)
            {
                MwState.Instance.CompleteTrophy(validLevels[i], new PhysicsProp { scrapPersistedThroughRounds = false });
            }
            trophies = MwState.Instance.GetTrophyList();
            foreach (var moon in validLevels)
            {
                if (!trophies.Contains(moon))
                {
                    Plugin.Instance.LogError($"TrophyGoalTest3 failed: Missing trophy for moon '{moon}'");
                    return false;
                }
            }
            MwState.Instance.ResetTrophyList();

            return true;
        }
    }
}
