using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            RunTest("Sample Test 1", TestTerminalCommands);
            RunTest("Sample Test 2", TestLogicString);
            results.AppendLine($"Total Tests: {passed + failed}, Passed: {passed}, Failed: {failed}");
            Console.WriteLine(results.ToString());
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
            // Example test logic
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
                        return false; // Test fails
                    }
                }
            }

            return true; // Test passes
        }
    }
}
