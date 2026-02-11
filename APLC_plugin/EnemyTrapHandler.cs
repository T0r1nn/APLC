using System;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace APLC;

public enum EnemyType
{
    GhostGirl,
    Bracken
}

/**
 * Handles spawning enemies for enemy traps
 */
public static class EnemyTrapHandler
{
    private static SpawnableEnemyWithRarity ghostGirl;
    private static SpawnableEnemyWithRarity bracken;

    public static void SetupEnemyTrapHandler()
    {
        foreach (var moon in StartOfRound.Instance.levels)
        {
            foreach (var enemy in moon.Enemies)
            {
                if (enemy.enemyType.enemyName.ToLower().Contains("girl"))
                {
                    ghostGirl = enemy;
                }
                else if (enemy.enemyType.enemyName.ToLower().Contains("flowerman"))
                {
                    bracken = enemy;
                }

                if (bracken != null && ghostGirl != null)
                {
                    return;
                }
            }
        }
    }
    
    /**
     * Attempts to spawn an enemy, return true if the spawn succeeds
     */
    private static bool SpawnEnemy(SpawnableEnemyWithRarity enemy, bool inside, Vector3 spawnPos)
    {
        try
        {
            var gameObject = Object.Instantiate(enemy.enemyType.enemyPrefab,
                spawnPos + new Vector3(0f, 0.5f, 0f), Quaternion.identity);
            gameObject.GetComponentInChildren<NetworkObject>().Spawn(true);
            //gameObject.gameObject.GetComponentInChildren<EnemyAI>().stunNormalizedTimer = 1f;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /**
     * Attempts to spawn an enemy given the internal name of the enemy, returns true if spawn succeeds.
     */
    public static bool SpawnEnemyByName(EnemyType enemyType)
    {
        if (!StartOfRound.Instance.shipHasLanded || !StartOfRound.Instance.localPlayerController.IsHost || StartOfRound.Instance.allPlayersDead)
        {
            return false;
        }

        GameObject[] nodes = RoundManager.Instance.insideAINodes;
        if (nodes.Length == 0) return false;
        var randNode = UnityEngine.Random.RandomRangeInt(0, nodes.Length);
        var nodePos = nodes[randNode].transform.position;

        return enemyType switch
        {
            EnemyType.Bracken => SpawnEnemy(bracken, true, nodePos),
            EnemyType.GhostGirl => SpawnEnemy(ghostGirl, true, nodePos),
            _ => false
        };
    }
}