using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Reflection;

/// <summary>
/// Spawns two showcase players in the end scene and plays win/lose animations based on the winning team.
/// </summary>
public class EndSceneController : MonoBehaviour
{
    private static WaitForSeconds _waitForSeconds1 = new WaitForSeconds(1f);
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

    [Header("Team Visuals")]
    [SerializeField]
    private Material blueTeamMaterial;

    [SerializeField]
    private Material redTeamMaterial;

    [SerializeField]
    private SkinnedMeshRenderer[] showcaseRenderers;

    [Header("Lobby Exit UI")]
    [SerializeField]
    private Button quitToLobbyButton;

    [SerializeField]
    private Text quitButtonLabel;

    [SerializeField]
    private float buttonRevealDelay = 5f;

    [SerializeField]
    private float autoQuitDelay = 30f;

    [SerializeField]
    private string lobbySceneName = "Lobby";

    private Coroutine countdownRoutine;
    private bool exitTriggered;

    private void Start()
    {
        SpawnOutcomePlayers();
        InitializeQuitFlow();
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

        ApplyTeamMaterial(winnerObj, winner);
        ApplyTeamMaterial(loserObj, GetOpposingTeam(winner));

        PlayOutcomeAnimation(winnerObj, true);
        PlayOutcomeAnimation(loserObj, false);

        Debug.Log($"[EndSceneController] Winner: {winner}");
    }

    private void InitializeQuitFlow()
    {
        if (quitToLobbyButton != null)
        {
            quitToLobbyButton.gameObject.SetActive(false);
            quitToLobbyButton.onClick.AddListener(OnQuitToLobbyClicked);
        }

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
        }

        countdownRoutine = StartCoroutine(QuitCountdown());
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

    private Team GetOpposingTeam(Team team)
    {
        return team switch
        {
            Team.Blue => Team.Red,
            Team.Red => Team.Blue,
            _ => Team.Red
        };
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
        var transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
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

    private System.Collections.IEnumerator QuitCountdown()
    {
        yield return new WaitForSeconds(buttonRevealDelay);

        if (quitToLobbyButton != null)
        {
            quitToLobbyButton.gameObject.SetActive(true);
        }

        float remaining = autoQuitDelay;
        while (remaining > 0f && !exitTriggered)
        {
            UpdateQuitLabel(Mathf.CeilToInt(remaining));
            yield return _waitForSeconds1;
            remaining -= 1f;
        }

        if (!exitTriggered)
        {
            OnQuitToLobbyClicked();
        }
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

    private void ApplyTeamMaterial(GameObject obj, Team team)
    {
        if (obj == null)
        {
            return;
        }

        var material = ResolveTeamMaterial(team, obj);
        if (material == null)
        {
            return;
        }

        var renderers = ResolveRenderers(obj);
        foreach (var renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.material = material;
            }
        }
    }

    private Material ResolveTeamMaterial(Team team, GameObject obj)
    {
        Material material = null;
        switch (team)
        {
            case Team.Blue:
                material = blueTeamMaterial;
                break;
            case Team.Red:
                material = redTeamMaterial;
                break;
            default:
                return null;
        }

        if (material != null)
        {
            return material;
        }

        var playerController = obj.GetComponent<PlayerController>();
        if (playerController == null)
        {
            return null;
        }

        var materialFieldName = team == Team.Blue ? "blueTeamMaterial" : "redTeamMaterial";
        var materialField = typeof(PlayerController).GetField(materialFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return materialField?.GetValue(playerController) as Material;
    }

    private IEnumerable<SkinnedMeshRenderer> ResolveRenderers(GameObject obj)
    {
        if (showcaseRenderers != null && showcaseRenderers.Length > 0)
        {
            return showcaseRenderers;
        }

        var playerController = obj.GetComponent<PlayerController>();
        if (playerController != null)
        {
            var renderersField = typeof(PlayerController).GetField("playerRenderers", BindingFlags.NonPublic | BindingFlags.Instance);
            var renderersValue = renderersField?.GetValue(playerController) as SkinnedMeshRenderer[];
            if (renderersValue != null && renderersValue.Length > 0)
            {
                return renderersValue;
            }
        }

        return obj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    private void UpdateQuitLabel(int secondsRemaining)
    {
        var label = quitButtonLabel != null ? quitButtonLabel : quitToLobbyButton != null ? quitToLobbyButton.GetComponentInChildren<Text>() : null;

        if (label != null)
        {
            label.text = $"Go back to lobby ({secondsRemaining}s)";
        }
    }

    private void OnQuitToLobbyClicked()
    {
        if (exitTriggered)
        {
            return;
        }

        exitTriggered = true;

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        UpdateQuitLabel(0);

        if (quitToLobbyButton != null)
        {
            quitToLobbyButton.interactable = false;
        }

        ShutdownNetwork();
        LoadLobbyScene();
    }

    private void ShutdownNetwork()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
    }

    private void LoadLobbyScene()
    {
        if (!string.IsNullOrWhiteSpace(lobbySceneName))
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }
}
