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
                if (enemy.enemyType.enemyName.ToLower().Contains("dress"))
                {
                    ghostGirl = enemy;
                }
                else if (enemy.enemyType.enemyName.ToLower().Contains("flower"))
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
    private static bool SpawnEnemy(SpawnableEnemyWithRarity enemy, bool inside, PlayerControllerB player)
    {
        try
        {
            if (inside ^ player.isInsideFactory) return false;

            var gameObject = Object.Instantiate(enemy.enemyType.enemyPrefab,
                player.transform.position + new Vector3(0f, 0.5f, 0f), Quaternion.identity);
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
     * Attempts to spawn an enemy given the in code name of the enemy, returns true if spawn succeeds.
     */
    public static bool SpawnEnemyByName(EnemyType enemyType)
    {
        if (!StartOfRound.Instance.shipHasLanded || !StartOfRound.Instance.localPlayerController.IsHost)
        {
            return false;
        }

        var allPlayers = StartOfRound.Instance.allPlayerScripts;
        var i = UnityEngine.Random.RandomRangeInt(0, allPlayers.Length);
        var startI = i;

        var spawnPlayer = allPlayers[i];
        while (spawnPlayer.isPlayerDead ||
               spawnPlayer.playerUsername == "Player #0" || spawnPlayer.playerUsername == "Player #1" ||
               spawnPlayer.playerUsername == "Player #2" || spawnPlayer.playerUsername == "Player #3" ||
               spawnPlayer.playerUsername == "Player #4" || spawnPlayer.playerUsername == "Player #5" ||
               spawnPlayer.playerUsername == "Player #6" || spawnPlayer.playerUsername == "Player #7")
        {
            i++;
            i %= allPlayers.Length;
            spawnPlayer = allPlayers[i];
            if (i == startI)
            {
                return false;
            }
        }

        return enemyType switch
        {
            EnemyType.Bracken => SpawnEnemy(bracken, true, spawnPlayer),
            EnemyType.GhostGirl => SpawnEnemy(ghostGirl, true, spawnPlayer),
            _ => false
        };
    }
}