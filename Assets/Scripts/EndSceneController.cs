using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Spawns two showcase players in the end scene and plays win/lose animations based on the winning team.
/// </summary>
public class EndSceneController : MonoBehaviour
{
    [Header("Prefabs & Spawns")]
    [SerializeField]
    private GameObject playerPrefab;

    [SerializeField]
    private List<Transform> spawnPositions = new();

    [Header("Animator Triggers")]
    [SerializeField]
    private string winTriggerName = "win";

    [SerializeField]
    private string loseTriggerName = "lose";

    private void Start()
    {
        SpawnOutcomePlayers();
    }

    /// <summary>
    /// Spawns two players and plays win/lose animations.
    /// </summary>
    private void SpawnOutcomePlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("[EndSceneController] Player prefab not assigned.");
            return;
        }

        Team winner = GetWinningTeam();
        var resolvedSpawns = ResolveSpawnPositions();

        if (resolvedSpawns.Count < 2)
        {
            Debug.LogWarning("[EndSceneController] Need at least two spawn positions to display winner and loser.");
            return;
        }

        var winnerObj = Instantiate(playerPrefab, resolvedSpawns[0].position, resolvedSpawns[0].rotation);
        var loserObj = Instantiate(playerPrefab, resolvedSpawns[1].position, resolvedSpawns[1].rotation);

        PlayOutcomeAnimation(winnerObj, true);
        PlayOutcomeAnimation(loserObj, false);

        Debug.Log($"[EndSceneController] Winner: {winner}");
    }

    /// <summary>
    /// Determines which team won from the networked state.
    /// </summary>
    private Team GetWinningTeam()
    {
        if (LevelSelectionState.Instance == null)
        {
            Debug.LogWarning("[EndSceneController] LevelSelectionState missing; winner defaults to None.");
            return Team.None;
        }

        return LevelSelectionState.Instance.WinningTeam;
    }

    /// <summary>
    /// Looks for configured spawns, otherwise tries to find p1Spawn/p2Spawn in the scene.
    /// </summary>
    private List<Transform> ResolveSpawnPositions()
    {
        if (spawnPositions != null && spawnPositions.Count >= 2)
        {
            return spawnPositions;
        }

        var resolved = new List<Transform>();
        var transforms = FindObjectsOfType<Transform>(true);
        var p1 = transforms.FirstOrDefault(t => t.name == "p1Spawn");
        var p2 = transforms.FirstOrDefault(t => t.name == "p2Spawn");

        if (p1 != null)
        {
            resolved.Add(p1);
        }
        if (p2 != null)
        {
            resolved.Add(p2);
        }

        // Fallback: place two positions near origin if none found.
        if (resolved.Count < 2)
        {
            var origin = new GameObject("EndSceneSpawnFallback_A").transform;
            origin.position = Vector3.zero;
            resolved.Add(origin);

            var offset = new GameObject("EndSceneSpawnFallback_B").transform;
            offset.position = Vector3.right * 2f;
            resolved.Add(offset);
        }

        return resolved;
    }

    /// <summary>
    /// Triggers the animator state for winner/loser.
    /// </summary>
    private void PlayOutcomeAnimation(GameObject obj, bool isWinner)
    {
        if (obj == null)
        {
            return;
        }

        var animator = obj.GetComponent<Animator>();
        if (animator == null)
        {
            return;
        }

        var trigger = isWinner ? winTriggerName : loseTriggerName;
        if (!string.IsNullOrWhiteSpace(trigger))
        {
            animator.SetTrigger(trigger);
        }
    }
}
