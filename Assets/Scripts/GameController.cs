using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages game initialization, player spawning, team assignment, and scoring for networked multiplayer.
/// </summary>
public class GameController : NetworkBehaviour
{
    private static readonly WaitForSeconds _waitForSeconds1 = new(1f);

    /// <summary>
    /// The player prefab to instantiate for each connected client.
    /// </summary>
    [SerializeField]
    private GameObject playerPrefabs;

    /// <summary>
    /// List of spawn positions for players. Each player spawns at the position corresponding to their index.
    /// </summary>
    [SerializeField]
    private List<Transform> spawnPos;

    [Header("Score UI")]
    [SerializeField]
    private GameObject ownFlagParent;

    [SerializeField]
    private GameObject otherFlagParent;

    [SerializeField]
    private Text ownScoreText;

    [SerializeField]
    private Text otherScoreText;

    [SerializeField]
    private float ownFlagScale = 300f;

    [SerializeField]
    private float otherFlagScale = 200f;

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
    /// Level prefabs available to spawn when the game scene loads. Assign prefabs from Assets/Levels.
    /// </summary>
    [SerializeField]
    private List<GameObject> levelPrefabs = new();

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

    [Header("Items")]
    [SerializeField]
    private GameObject itemSpawnManagerPrefab;

    [Header("Flags")]
    [SerializeField]
    private GameObject blueFlagPrefab;

    [SerializeField]
    private GameObject redFlagPrefab;

    [Header("Flag Sprites (UI)")]
    [SerializeField]
    private Sprite blueFlagSprite;

    [SerializeField]
    private Sprite redFlagSprite;

    private Transform blueFlagPos;
    private Transform redFlagPos;
    private bool flagsSpawned;

    private GameObject ownFlagInstance;
    private GameObject otherFlagInstance;

    [Header("End Game UI")]
    [SerializeField]
    private Button btnEnd;

    [SerializeField]
    private Text btnEndLabel;

    [SerializeField]
    private float buttonRevealDelay = 3f;

    [SerializeField]
    private float autoQuitDelay = 30f;

#if UNITY_EDITOR
    [SerializeField]
    private SceneAsset lobbyScene;
#endif

    [SerializeField, HideInInspector]
    private string lobbySceneName = "LobbyScene";

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

        // Use coroutine to wait for LevelSelectionState.Instance before spawning level
        StartCoroutine(WaitForLevelSelectionAndSpawn());
    }

    /// <summary>
    /// Waits for LevelSelectionState.Instance to be available, then spawns the level and players.
    /// </summary>
    private IEnumerator WaitForLevelSelectionAndSpawn()
    {
        // Wait until LevelSelectionState.Instance is available
        float timeout = 5f;
        float elapsed = 0f;
        while (LevelSelectionState.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (LevelSelectionState.Instance == null)
        {
            Debug.LogWarning("[GameController] LevelSelectionState.Instance not found after timeout. Using default level.");
        }
        else
        {
            Debug.Log($"[GameController] LevelSelectionState.Instance found. SelectedLevelName = '{LevelSelectionState.Instance.SelectedLevelName}'");
        }

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

        ClearFlagInstances();
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


        if (levelPrefab.TryGetComponent<NetworkObject>(out var networkObject))
        {
            if (IsServer)
            {
                GameObject instance = Instantiate(levelPrefab);
                var instanceNet = instance.GetComponent<NetworkObject>();
                instanceNet.Spawn(true);

                CacheSpawnPositions(instance);
                SpawnChests();
                SpawnFlags();
                SpawnItemSpawnManager();
            }
        }
        else
        {
            // Non-networked level prefab: instantiate locally on each client/host.
            var instance = Instantiate(levelPrefab);
            CacheSpawnPositions(instance);
            SpawnChests();
            SpawnFlags();
            SpawnItemSpawnManager();
        }

        levelSpawned = true;
    }

    /// <summary>
    /// Reads the lobby-selected level name from the synced state.
    /// </summary>
    private string GetChosenLevelName()
    {
        if (LevelSelectionState.Instance != null)
        {
            string levelName = LevelSelectionState.Instance.SelectedLevelName;
            Debug.Log($"[GameController] GetChosenLevelName: LevelSelectionState.SelectedLevelName = '{levelName}'");
            if (!string.IsNullOrWhiteSpace(levelName))
            {
                return levelName;
            }
        }
        else
        {
            Debug.LogWarning("[GameController] GetChosenLevelName: LevelSelectionState.Instance is null!");
        }

        return string.Empty;
    }

    /// <summary>
    /// Finds the level prefab that matches the provided name.
    /// </summary>
    /// <param name="levelName">Prefab name to locate.</param>
    private GameObject ResolveLevelPrefab(string levelName)
    {
        // Debug: Log all available level prefabs
        Debug.Log($"[GameController] ResolveLevelPrefab: Looking for '{levelName}'. Available prefabs: {string.Join(", ", levelPrefabs.Where(p => p != null).Select(p => $"'{p.name}'"))}");

        if (string.IsNullOrWhiteSpace(levelName))
        {
            Debug.Log("[GameController] ResolveLevelPrefab: levelName is empty, returning first prefab");
            return levelPrefabs.FirstOrDefault();
        }

        // First try exact match
        var found = levelPrefabs.FirstOrDefault(p => p != null && p.name == levelName);

        // If not found, try matching with spaces removed (to handle "Lv 2" vs "Lv2")
        if (found == null)
        {
            string normalizedName = levelName.Replace(" ", "");
            found = levelPrefabs.FirstOrDefault(p => p != null && p.name.Replace(" ", "") == normalizedName);
            if (found != null)
            {
                Debug.Log($"[GameController] ResolveLevelPrefab: Found '{found.name}' by normalized match for '{levelName}'");
            }
        }

        if (found == null)
        {
            Debug.LogWarning($"[GameController] ResolveLevelPrefab: No prefab found matching '{levelName}'");
        }
        return found;
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
            if (player.TryGetComponent<TeamMember>(out var teamMember))
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
        if (levelInstance == null)
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
        if (chestsSpawned)
        {
            return;
        }

        if (chestPrefab == null)
        {
            Debug.LogWarning("[GameController] Chest prefab not assigned; cannot spawn chests.");
            return;
        }

        Debug.Log($"[GameController] Spawning {chestSpawnPos.Count} chests locally...");

        foreach (var point in chestSpawnPos)
        {
            if (point == null)
            {
                continue;
            }

            // Spawn locally without network sync
            Instantiate(chestPrefab, point.position, point.rotation);
        }

        chestsSpawned = true;
    }

    private void SpawnFlags()
    {
        if (flagsSpawned)
        {
            return;
        }

        if (blueFlagPrefab == null || redFlagPrefab == null)
        {
            Debug.LogWarning("[GameController] Flag prefabs not assigned; cannot spawn flags.");
            return;
        }

        Debug.Log("[GameController] Spawning flags locally...");

        if (blueFlagPos != null)
        {
            // Spawn locally without network sync
            Instantiate(blueFlagPrefab, blueFlagPos.position, blueFlagPos.rotation);
        }

        if (redFlagPos != null)
        {
            // Spawn locally without network sync
            Instantiate(redFlagPrefab, redFlagPos.position, redFlagPos.rotation);
        }

        flagsSpawned = true;
    }

    /// <summary>
    /// Spawns the ItemSpawnManager on the server and syncs to clients.
    /// </summary>
    private void SpawnItemSpawnManager()
    {
        // Only spawn if we're the server and the prefab is assigned
        if (!IsServer)
        {
            return;
        }

        if (itemSpawnManagerPrefab == null)
        {
            Debug.LogWarning("[GameController] ItemSpawnManager prefab not assigned; cannot spawn item manager.");
            return;
        }

        // Check if ItemSpawnManager already exists
        if (ItemSpawnManager.Instance != null)
        {
            Debug.Log("[GameController] ItemSpawnManager already exists.");
            return;
        }

        Debug.Log("[GameController] Spawning ItemSpawnManager...");

        var instance = Instantiate(itemSpawnManagerPrefab);

        if (instance.TryGetComponent<NetworkObject>(out var networkObject))
        {
            networkObject.Spawn(true);
        }
        else
        {
            Debug.LogError("[GameController] ItemSpawnManager prefab is missing NetworkObject component!");
        }
    }

    /// <summary>
    /// Called when blue team score changes. Updates the UI.
    /// </summary>
    private void OnBlueScoreChanged(int oldValue, int newValue)
    {
        UpdateScoreUI();
    }

    /// <summary>
    /// Called when red team score changes. Updates the UI.
    /// </summary>
    private void OnRedScoreChanged(int oldValue, int newValue)
    {
        UpdateScoreUI();
    }

    /// <summary>
    /// Updates the score UI to show only the local player's team score.
    /// </summary>
    private void UpdateScoreUI()
    {
        if (localPlayerTeam == Team.None)
        {
            UpdateScoreLabels(0, 0);
            ClearFlagInstances();
            return;
        }

        Team otherTeam = GetOpposingTeam(localPlayerTeam);
        int ownScore = GetScore(localPlayerTeam);
        int otherScore = GetScore(otherTeam);

        UpdateScoreLabels(ownScore, otherScore);
        UpdateFlagInstance(ownFlagParent, localPlayerTeam, ref ownFlagInstance, true);
        UpdateFlagInstance(otherFlagParent, otherTeam, ref otherFlagInstance, false);
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

    private void UpdateScoreLabels(int ownScore, int otherScore)
    {
        if (ownScoreText != null)
        {
            ownScoreText.text = ownScore.ToString();
        }

        if (otherScoreText != null)
        {
            otherScoreText.text = otherScore.ToString();
        }
    }

    private void UpdateFlagInstance(GameObject parent, Team team, ref GameObject instance, bool isOwnFlag)
    {
        if (parent == null || team == Team.None)
        {
            if (instance != null)
            {
                Destroy(instance);
                instance = null;
            }
            return;
        }

        var canvas = parent.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Sprite selectedSprite = (team == Team.Blue) ? blueFlagSprite : redFlagSprite;
            if (instance != null)
            {
                Destroy(instance);
            }
            instance = new GameObject(team + "FlagUI", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            instance.transform.SetParent(parent.transform, false);
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = instance.GetComponent<UnityEngine.UI.Image>();
            img.sprite = selectedSprite;
            img.preserveAspect = true;
            img.color = Color.white;
        }
        else
        {
            if (instance != null)
            {
                Destroy(instance);
                instance = null;
            }
        }
    }

    private void ClearFlagInstances()
    {
        if (ownFlagInstance != null)
        {
            Destroy(ownFlagInstance);
            ownFlagInstance = null;
        }

        if (otherFlagInstance != null)
        {
            Destroy(otherFlagInstance);
            otherFlagInstance = null;
        }
    }

    private Team GetOpposingTeam(Team team)
    {
        if (team == Team.Blue)
        {
            return Team.Red;
        }
        if (team == Team.Red)
        {
            return Team.Blue;
        }

        return Team.None;
    }

    private GameObject GetFlagPrefab(Team team)
    {
        if (team == Team.Blue)
        {
            return blueFlagPrefab;
        }
        if (team == Team.Red)
        {
            return redFlagPrefab;
        }

        return null;
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

    private const string winTriggerName = "win";
    private const string loseTriggerName = "lose";
    private const string winStateName = "Win";
    private const string loseStateName = "Lose";
    private const string idleStateName = "Idle";

    /// <summary>
    /// Plays win/lose animations on all players via ClientRpc.
    /// </summary>
    [ClientRpc]
    private void PlayOutcomeAnimationsClientRpc(Team winningTeam)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
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
            yield return _waitForSeconds1;
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (lobbyScene != null)
        {
            lobbySceneName = lobbyScene.name;
        }
    }

#endif

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
