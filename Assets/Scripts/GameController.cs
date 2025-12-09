using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages game initialization, player spawning, team assignment, and scoring for networked multiplayer.
/// </summary>
public class GameController : NetworkBehaviour
{
    /// <summary>
    /// The player prefab to instantiate for each connected client.
    /// </summary>
    public GameObject playerPrefabs;

    /// <summary>
    /// List of spawn positions for players. Each player spawns at the position corresponding to their index.
    /// </summary>
    public List<Transform> spawnPos;

    /// <summary>
    /// UI Text element to display the player's own team score.
    /// </summary>
    public Text scoreText;

    /// <summary>
    /// Network variable to sync blue team score across all clients.
    /// </summary>
    private NetworkVariable<int> blueTeamScore = new NetworkVariable<int>(0);

    /// <summary>
    /// Network variable to sync red team score across all clients.
    /// </summary>
    private NetworkVariable<int> redTeamScore = new NetworkVariable<int>(0);

    /// <summary>
    /// Level prefabs available to spawn when the game scene loads. Assign prefabs from Assets/Levels.
    /// </summary>
    [SerializeField]
    private List<GameObject> levelPrefabs = new List<GameObject>();

    /// <summary>
    /// Fallback level name if nothing is selected in the lobby.
    /// </summary>
    [SerializeField]
    private string defaultLevelName = "Demo";

    /// <summary>
    /// The local player's team, used to determine which score to display.
    /// </summary>
    private Team localPlayerTeam = Team.None;

    /// <summary>
    /// Singleton instance for easy access from other scripts.
    /// </summary>
    public static GameController Instance { get; private set; }

    /// <summary>
    /// Prevents duplicate level instantiation across host/client processes.
    /// </summary>
    private static bool levelSpawned;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called when the network object is spawned. The host spawns player objects for all connected clients.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        blueTeamScore.OnValueChanged += OnBlueScoreChanged;
        redTeamScore.OnValueChanged += OnRedScoreChanged;

        UpdateScoreUI();

        TrySpawnSelectedLevel();

        if (IsHost)
        {
            SpawnAllPlayers();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        blueTeamScore.OnValueChanged -= OnBlueScoreChanged;
        redTeamScore.OnValueChanged -= OnRedScoreChanged;

        levelSpawned = false;
    }

    /// <summary>
    /// Instantiates the chosen level prefab when the game scene loads.
    /// </summary>
    private void TrySpawnSelectedLevel()
    {
        if (levelSpawned)
        {
            return;
        }

        string chosenName = GetChosenLevelName();
        GameObject levelPrefab = ResolveLevelPrefab(chosenName);

        if (levelPrefab == null)
        {
            Debug.LogWarning($"[GameController] Level '{chosenName}' not found. Aborting level spawn.");
            return;
        }

        var networkObject = levelPrefab.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            if (IsServer)
            {
                GameObject instance = Instantiate(levelPrefab);
                var instanceNet = instance.GetComponent<NetworkObject>();
                instanceNet.Spawn(true);

                CacheSpawnPositions(instance);
            }
        }
        else
        {
            // Non-networked level prefab: instantiate locally on each client/host.
            var instance = Instantiate(levelPrefab);
            CacheSpawnPositions(instance);
        }

        levelSpawned = true;
    }

    /// <summary>
    /// Reads the lobby-selected level name from the synced state or falls back to default.
    /// </summary>
    private string GetChosenLevelName()
    {
        if (LevelSelectionState.Instance != null && !string.IsNullOrWhiteSpace(LevelSelectionState.Instance.SelectedLevelName))
        {
            return LevelSelectionState.Instance.SelectedLevelName;
        }

        return defaultLevelName;
    }

    /// <summary>
    /// Finds the level prefab that matches the provided name.
    /// </summary>
    /// <param name="levelName">Prefab name to locate.</param>
    private GameObject ResolveLevelPrefab(string levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return levelPrefabs.FirstOrDefault();
        }

        return levelPrefabs.FirstOrDefault(p => p != null && p.name == levelName);
    }

    /// <summary>
    /// Spawns player objects for all connected clients at their designated spawn positions.
    /// Host (index 0) is assigned to Blue team, other players to Red team.
    /// </summary>
    private void SpawnAllPlayers()
    {
        if (spawnPos == null || spawnPos.Count < NetworkManager.Singleton.ConnectedClientsIds.Count)
        {
            Debug.LogWarning($"[GameController] Not enough spawn positions. Required: {NetworkManager.Singleton.ConnectedClientsIds.Count}, Available: {spawnPos?.Count ?? 0}");
            return;
        }

        Quaternion spawnRotation = Quaternion.Euler(0f, 90f, 0f);
        for (int i = 0; i < NetworkManager.Singleton.ConnectedClientsIds.Count; i++)
        {
            ulong clientID = NetworkManager.Singleton.ConnectedClientsIds[i];
            GameObject player = Instantiate(playerPrefabs, spawnPos[i].position, spawnRotation);

            var networkObject = player.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(clientID);

            // Assign team: Host (index 0) = Blue, others = Red
            var teamMember = player.GetComponent<TeamMember>();
            if (teamMember != null)
            {
                Team assignedTeam = (i == 0) ? Team.Blue : Team.Red;
                teamMember.SetTeamRpc(assignedTeam);
            }
        }
    }

    /// <summary>
    /// Attempts to bind spawn positions from the spawned level instance.
    /// Looks for children named "p1Spawn" and "p2Spawn" (case-sensitive).
    /// </summary>
    /// <param name="levelInstance">Instantiated level object.</param>
    private void CacheSpawnPositions(GameObject levelInstance)
    {
        if (!IsServer || levelInstance == null)
        {
            return;
        }

        var transforms = levelInstance.GetComponentsInChildren<Transform>(true);
        var p1 = transforms.FirstOrDefault(t => t.name == "p1Spawn");
        var p2 = transforms.FirstOrDefault(t => t.name == "p2Spawn");

        spawnPos ??= new List<Transform>();
        spawnPos.Clear();

        if (p1 != null)
        {
            spawnPos.Add(p1);
        }
        if (p2 != null)
        {
            spawnPos.Add(p2);
        }

        if (spawnPos.Count == 0)
        {
            Debug.LogWarning("[GameController] No spawn points named p1Spawn/p2Spawn found in level instance.");
        }
    }

    /// <summary>
    /// Called when blue team score changes. Updates the UI.
    /// </summary>
    private void OnBlueScoreChanged(int oldValue, int newValue)
    {
        if (localPlayerTeam == Team.Blue)
        {
            UpdateScoreUI();
        }
    }

    /// <summary>
    /// Called when red team score changes. Updates the UI.
    /// </summary>
    private void OnRedScoreChanged(int oldValue, int newValue)
    {
        if (localPlayerTeam == Team.Red)
        {
            UpdateScoreUI();
        }
    }

    /// <summary>
    /// Updates the score UI to show only the local player's team score.
    /// </summary>
    private void UpdateScoreUI()
    {
        if (scoreText == null)
        {
            return;
        }

        int score = GetScore(localPlayerTeam);
        scoreText.text = "Score: " + score;
    }

    /// <summary>
    /// Sets the local player's team and updates the UI accordingly.
    /// </summary>
    /// <param name="team">The team assigned to the local player.</param>
    public void SetLocalPlayerTeam(Team team)
    {
        localPlayerTeam = team;
        UpdateScoreUI();
    }

    /// <summary>
    /// Adds score to the specified team. Called from flag triggers.
    /// </summary>
    /// <param name="team">The team to add score to.</param>
    /// <param name="points">The number of points to add.</param>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AddScoreRpc(Team team, int points = 1)
    {
        if (team == Team.Blue)
        {
            blueTeamScore.Value += points;
        }
        else if (team == Team.Red)
        {
            redTeamScore.Value += points;
        }
    }

    /// <summary>
    /// Gets the current score for the specified team.
    /// </summary>
    /// <param name="team">The team to get the score for.</param>
    /// <returns>The team's current score.</returns>
    public int GetScore(Team team)
    {
        if (team == Team.Blue)
        {
            return blueTeamScore.Value;
        }
        else if (team == Team.Red)
        {
            return redTeamScore.Value;
        }
        return 0;
    }
}
