using System;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace APLC;

public static class EnemyTrapHandler
{
    /**
     * Attempts to spawn an enemy, return true if the spawn succeeds
     */
    private static bool SpawnEnemy(SpawnableEnemyWithRarity enemy, int amount, bool inside, PlayerControllerB player)
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
    public static bool SpawnEnemyByName(string name)
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

        foreach (var enemy in StartOfRound.Instance.currentLevel.Enemies.Where(enemy => enemy.enemyType.enemyName.ToLower().Contains(name)))
            return SpawnEnemy(enemy, 1, true, spawnPlayer);

        foreach (var enemy in StartOfRound.Instance.currentLevel.OutsideEnemies.Where(enemy => enemy.enemyType.enemyName.ToLower().Contains(name)))
            return SpawnEnemy(enemy, 1, false, spawnPlayer);

        return (from enemy in StartOfRound.Instance.currentLevel.DaytimeEnemies where enemy.enemyType.enemyName.ToLower().Contains(name) select SpawnEnemy(enemy, 1, false, spawnPlayer)).FirstOrDefault();
    }
}