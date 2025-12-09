using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
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
    private readonly NetworkVariable<int> blueTeamScore = new(0);

    /// <summary>
    /// Network variable to sync red team score across all clients.
    /// </summary>
    private readonly NetworkVariable<int> redTeamScore = new(0);

    /// <summary>
    /// Points required to trigger the end scene.
    /// </summary>
    [SerializeField]
    private int pointsToWin = 5;

    /// <summary>
    /// Scene name to load when a team wins.
    /// </summary>
    [SerializeField]
    private string endSceneName = "EndScene";

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

    /// <summary>
    /// Prevents multiple win triggers once a team has reached the target score.
    /// </summary>
    private bool matchEnded;

    [Header("Chests")]
    [SerializeField]
    private GameObject chestPrefab;

    private readonly List<Transform> chestSpawnPos = new();
    private bool chestsSpawned;

    [Header("Flags")]
    [SerializeField]
    private GameObject blueFlagPrefab;

    [SerializeField]
    private GameObject redFlagPrefab;

    private Transform blueFlagPos;
    private Transform redFlagPos;
    private bool flagsSpawned;

    [Header("End Game UI")]
    [SerializeField]
    private Button btnEnd;

    [SerializeField]
    private Text btnEndLabel;

    [SerializeField]
    private float buttonRevealDelay = 3f;

    [SerializeField]
    private float autoQuitDelay = 30f;

    [SerializeField]
    private string lobbySceneName = "Lobby";

    private Coroutine countdownRoutine;
    private bool exitTriggered;

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

        // Hide btnEnd on awake
        if (btnEnd != null)
        {
            btnEnd.gameObject.SetActive(false);
            btnEnd.onClick.AddListener(OnBtnEndClicked);
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

        matchEnded = false;
        chestsSpawned = false;
        flagsSpawned = false;
        exitTriggered = false;

        // Ensure btnEnd is hidden on spawn
        if (btnEnd != null)
        {
            btnEnd.gameObject.SetActive(false);
        }

        UpdateScoreUI();

        TrySpawnSelectedLevel();

        if (IsServer && LevelSelectionState.Instance != null)
        {
            LevelSelectionState.Instance.ClearWinningTeam();
        }

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
        matchEnded = false;
        chestsSpawned = false;
        flagsSpawned = false;
        exitTriggered = false;

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }
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
                SpawnChests();
                SpawnFlags();
            }
        }
        else
        {
            // Non-networked level prefab: instantiate locally on each client/host.
            var instance = Instantiate(levelPrefab);
            CacheSpawnPositions(instance);
            SpawnChests();
            SpawnFlags();
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
        var chestPoints = transforms.Where(t => t.name.ToLower().Contains("chestpos")).ToList();
        blueFlagPos = transforms.FirstOrDefault(t => t.name == "blueFlagPos");
        redFlagPos = transforms.FirstOrDefault(t => t.name == "redFlagPos");

        spawnPos ??= new List<Transform>();
        spawnPos.Clear();
        chestSpawnPos.Clear();

        if (p1 != null)
        {
            spawnPos.Add(p1);
        }
        if (p2 != null)
        {
            spawnPos.Add(p2);
        }

        if (chestPoints.Count > 0)
        {
            chestSpawnPos.AddRange(chestPoints);
        }

        if (spawnPos.Count == 0)
        {
            Debug.LogWarning("[GameController] No spawn points named p1Spawn/p2Spawn found in level instance.");
        }

        if (chestPoints.Count == 0)
        {
            Debug.LogWarning("[GameController] No chest spawn points found (expects name containing 'chestPos').");
        }

        if (blueFlagPos == null || redFlagPos == null)
        {
            Debug.LogWarning("[GameController] Flag spawn points missing (expects 'blueFlagPos' and 'redFlagPos').");
        }
    }

    private void SpawnChests()
    {
        if (!IsServer || chestsSpawned)
        {
            return;
        }

        if (chestPrefab == null)
        {
            Debug.LogWarning("[GameController] Chest prefab not assigned; cannot spawn chests.");
            return;
        }

        foreach (var point in chestSpawnPos)
        {
            if (point == null)
            {
                continue;
            }

            var chest = Instantiate(chestPrefab, point.position, point.rotation);
            var net = chest.GetComponent<NetworkObject>();
            if (net != null)
            {
                net.Spawn(true);
            }
        }

        chestsSpawned = true;
    }

    private void SpawnFlags()
    {
        if (!IsServer || flagsSpawned)
        {
            return;
        }

        if (blueFlagPrefab == null || redFlagPrefab == null)
        {
            Debug.LogWarning("[GameController] Flag prefabs not assigned; cannot spawn flags.");
            return;
        }

        if (blueFlagPos != null)
        {
            var blue = Instantiate(blueFlagPrefab, blueFlagPos.position, blueFlagPos.rotation);
            var net = blue.GetComponent<NetworkObject>();
            if (net != null)
            {
                net.Spawn(true);
            }
        }

        if (redFlagPos != null)
        {
            var red = Instantiate(redFlagPrefab, redFlagPos.position, redFlagPos.rotation);
            var net = red.GetComponent<NetworkObject>();
            if (net != null)
            {
                net.Spawn(true);
            }
        }

        flagsSpawned = true;
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
        if (!IsServer)
        {
            return;
        }

        if (matchEnded)
        {
            return;
        }

        if (team == Team.Blue)
        {
            blueTeamScore.Value += points;
        }
        else if (team == Team.Red)
        {
            redTeamScore.Value += points;
        }

        int teamScore = GetScore(team);
        if (teamScore >= pointsToWin)
        {
            matchEnded = true;
            SetWinningTeam(team);
            PlayOutcomeAnimationsClientRpc(team);
        }
    }

    /// <summary>
    /// Stores the winning team so the end scene can display results.
    /// </summary>
    /// <param name="team">Team that reached the win condition.</param>
    private void SetWinningTeam(Team team)
    {
        if (LevelSelectionState.Instance != null)
        {
            LevelSelectionState.Instance.SetWinningTeam(team);
        }
    }

    [Header("Outcome Animation Settings")]
    [SerializeField]
    private string winTriggerName = "win";

    [SerializeField]
    private string loseTriggerName = "lose";

    [SerializeField]
    private string winStateName = "";

    [SerializeField]
    private string loseStateName = "";

    [SerializeField]
    private string idleStateName = "Idle";

    /// <summary>
    /// Plays win/lose animations on all players via ClientRpc.
    /// </summary>
    [ClientRpc]
    private void PlayOutcomeAnimationsClientRpc(Team winningTeam)
    {
        var players = FindObjectsOfType<PlayerController>(true);
        Debug.Log($"[GameController] Found {players.Length} players for outcome animations. Winning team: {winningTeam}");
        
        foreach (var player in players)
        {
            var teamMember = player.GetComponent<TeamMember>();
            Team playerTeam = teamMember != null ? teamMember.CurrentTeam : Team.None;
            bool isWinner = playerTeam == winningTeam && winningTeam != Team.None;
            
            Debug.Log($"[GameController] Player {player.name}: Team={playerTeam}, IsWinner={isWinner}");
            player.PlayOutcomeAnimation(isWinner, winTriggerName, loseTriggerName, winStateName, loseStateName, idleStateName);
        }

        Debug.Log($"[GameController] Match ended. Winner: {winningTeam}");

        // Start the countdown to show btnEnd and auto-quit
        StartEndGameCountdown();
    }

    /// <summary>
    /// Starts the end game countdown: delay showing button, then countdown to auto-quit.
    /// </summary>
    private void StartEndGameCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
        }
        countdownRoutine = StartCoroutine(EndGameCountdownRoutine());
    }

    /// <summary>
    /// Coroutine that waits for button reveal delay, shows button, then counts down to auto-quit.
    /// </summary>
    private IEnumerator EndGameCountdownRoutine()
    {
        // Wait before showing the button
        yield return new WaitForSeconds(buttonRevealDelay);

        // Show the button
        if (btnEnd != null)
        {
            btnEnd.gameObject.SetActive(true);
        }

        // Countdown and update label
        float remaining = autoQuitDelay;
        while (remaining > 0f && !exitTriggered)
        {
            UpdateBtnEndLabel(Mathf.CeilToInt(remaining));
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        // Auto-quit if not already triggered
        if (!exitTriggered)
        {
            OnBtnEndClicked();
        }
    }

    /// <summary>
    /// Updates the btnEnd label text with remaining seconds.
    /// </summary>
    private void UpdateBtnEndLabel(int secondsRemaining)
    {
        var label = btnEndLabel != null ? btnEndLabel : (btnEnd != null ? btnEnd.GetComponentInChildren<Text>() : null);
        if (label != null)
        {
            label.text = $"Go back to lobby ({secondsRemaining}s)";
        }
    }

    /// <summary>
    /// Called when btnEnd is clicked. Shuts down network and loads lobby scene.
    /// </summary>
    private void OnBtnEndClicked()
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

        UpdateBtnEndLabel(0);

        if (btnEnd != null)
        {
            btnEnd.interactable = false;
        }

        ShutdownNetworkAndLoadLobby();
    }

    /// <summary>
    /// Shuts down the network and loads the lobby scene.
    /// </summary>
    private void ShutdownNetworkAndLoadLobby()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        if (!string.IsNullOrWhiteSpace(lobbySceneName))
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }

    /// <summary>
    /// Despawns all player objects before changing scenes.
    /// </summary>
    private void DespawnAllPlayers()
    {
        if (!IsServer || NetworkManager.Singleton == null)
        {
            return;
        }

        foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
        {
            var playerObject = clientPair.Value?.PlayerObject;
            if (playerObject != null && playerObject.IsSpawned)
            {
                playerObject.Despawn(true);
            }
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
