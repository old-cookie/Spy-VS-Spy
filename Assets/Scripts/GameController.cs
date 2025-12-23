using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
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
    private UIDocument uiDocument;

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
    /// The local player's team, used to determine which score to display.
    /// </summary>
    private Team localPlayerTeam = Team.None;

    /// <summary>
    /// Singleton instance for easy access from other scripts.
    /// </summary>
    public static GameController Instance { get; private set; }

    /// <summary>
    /// Prevents multiple win triggers once a team has reached the target score.
    /// </summary>
    private bool matchEnded;

    [Header("Chests")]
    [SerializeField]
    private GameObject chestPrefab;
    // Chests are placed directly in scenes; no runtime spawning.

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

    // Flags are placed directly in scenes; no runtime spawning.

    private VisualElement ownFlagParent;
    private VisualElement otherFlagParent;
    private VisualElement otherTeamContainer;
    private Label ownScoreText;
    private Label otherScoreText;
    private Label vsLabel;
    private VisualElement endGameContainer;
    private Button btnEnd;
    private VisualElement pauseMenu;
    private Button btnPauseContinue;
    private Button btnPauseEnd;
    private VisualElement itemInfoPanel;
    private Label itemNameText;
    private Label itemDescriptionText;

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
    private bool pauseMenuVisible;
    private InputSystem_Actions inputActions;

    /// <summary>
    /// Whether the pause menu is currently open on this client.
    /// </summary>
    public bool IsPauseMenuOpen => pauseMenuVisible;

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

        InitializeUI();
        InitializeInput();
    }

    private void InitializeUI()
    {
        if (uiDocument == null)
        {
            return;
        }

        var root = uiDocument.rootVisualElement;
        ownFlagParent = root.Q<VisualElement>("OwnFlagParent");
        otherFlagParent = root.Q<VisualElement>("OtherFlagParent");
        otherTeamContainer = root.Q<VisualElement>("OtherTeamContainer");
        ownScoreText = root.Q<Label>("OwnScoreText");
        otherScoreText = root.Q<Label>("OtherScoreText");
        vsLabel = root.Q<Label>("vsLabel");
        endGameContainer = root.Q<VisualElement>("EndGameContainer");
        btnEnd = root.Q<Button>("BtnEnd");
        pauseMenu = root.Q<VisualElement>("PauseMenu");
        btnPauseContinue = root.Q<Button>("BtnPauseContinue");
        btnPauseEnd = root.Q<Button>("BtnPauseEnd");
        itemInfoPanel = root.Q<VisualElement>("ItemInfoPanel");
        itemNameText = root.Q<Label>("ItemNameText");
        itemDescriptionText = root.Q<Label>("ItemDescriptionText");

        if (btnEnd != null)
        {
            btnEnd.clicked += OnBtnEndClicked;
        }

        if (btnPauseContinue != null)
        {
            btnPauseContinue.clicked += OnPauseContinueClicked;
        }

        if (btnPauseEnd != null)
        {
            btnPauseEnd.clicked += OnPauseEndClicked;
        }

        if (endGameContainer != null)
        {
            endGameContainer.style.display = DisplayStyle.None;
        }

        SetPauseMenuVisible(false);

        // Ensure item info panel starts hidden
        if (itemInfoPanel != null)
        {
            itemInfoPanel.style.display = DisplayStyle.None;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        TeardownInput();
    }

    private void InitializeInput()
    {
        if (inputActions != null)
        {
            return;
        }

        inputActions = new InputSystem_Actions();
        // Only need the UI map for this controller
        inputActions.UI.Enable();
        inputActions.UI.ItemDescription.performed += OnItemDescriptionPerformed;
        inputActions.UI.ItemDescription.canceled += OnItemDescriptionCanceled;
    }

    private void TeardownInput()
    {
        if (inputActions == null)
        {
            return;
        }

        inputActions.UI.ItemDescription.performed -= OnItemDescriptionPerformed;
        inputActions.UI.ItemDescription.canceled -= OnItemDescriptionCanceled;
        inputActions.UI.Disable();
        inputActions.Dispose();
        inputActions = null;
    }

    private void OnItemDescriptionPerformed(InputAction.CallbackContext ctx)
    {
        ShowItemInfo();
    }

    private void OnItemDescriptionCanceled(InputAction.CallbackContext ctx)
    {
        HideItemInfo();
    }

    private PlayerController GetLocalPlayerController()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj != null)
            {
                var pc = playerObj.GetComponent<PlayerController>();
                if (pc != null)
                {
                    return pc;
                }
            }
        }

        // Fallback: search for local player
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && p.IsLocalPlayer)
            {
                return p;
            }
        }
        return null;
    }

    private void ShowItemInfo()
    {
        if (itemInfoPanel == null)
        {
            return;
        }

        var pc = GetLocalPlayerController();
        if (pc == null)
        {
            return;
        }

        var item = pc.GetHeldItem();
        if (item == null)
        {
            // No item: ignore per requirement
            return;
        }

        if (itemNameText != null)
        {
            itemNameText.text = item.ItemType;
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.text = item.ItemDescription;
        }

        itemInfoPanel.style.display = DisplayStyle.Flex;
    }

    private void HideItemInfo()
    {
        if (itemInfoPanel == null)
        {
            return;
        }
        itemInfoPanel.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Shows or hides the pause menu on this client.
    /// </summary>
    /// <param name="visible">True to show the menu, false to hide it.</param>
    /// <returns>True if the menu is visible after the call.</returns>
    public bool SetPauseMenuVisible(bool visible)
    {
        if (pauseMenu == null)
        {
            pauseMenuVisible = false;
            return false;
        }

        pauseMenuVisible = visible;
        pauseMenu.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        return pauseMenuVisible;
    }

    /// <summary>
    /// Toggles the pause menu visibility on this client.
    /// </summary>
    /// <returns>The new visibility state.</returns>
    public bool TogglePauseMenu()
    {
        return SetPauseMenuVisible(!pauseMenuVisible);
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
        // No dynamic chest/flag spawning
        exitTriggered = false;
        SetPauseMenuVisible(false);

        if (endGameContainer != null)
        {
            endGameContainer.style.display = DisplayStyle.None;
        }

        UpdateScoreUI();
        UpdateUIForGameMode();

        // Use coroutine to wait for LevelSelectionState.Instance before spawning level
        StartCoroutine(WaitForLevelSelectionAndSpawn());
    }

    /// <summary>
    /// Waits for LevelSelectionState.Instance to be available, then initializes the level and spawns players.
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

        // Level scene is already loaded directly from lobby, just initialize
        if (IsServer)
        {
            CacheSpawnPositions();
            SpawnItemSpawnManager();

            if (LevelSelectionState.Instance != null)
            {
                LevelSelectionState.Instance.ClearWinningTeam();
            }
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
        matchEnded = false;
        // No dynamic chest/flag spawning
        exitTriggered = false;

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }
    }

    // TrySpawnSelectedLevel removed - level scenes are now loaded directly from lobby

    /// <summary>
    /// Spawns player objects for all connected clients at their designated spawn positions.
    /// Host (index 0) is assigned to Blue team, other players to Red team.
    /// </summary>
    private void SpawnAllPlayers()
    {
        if (spawnPos == null || spawnPos.Count < NetworkManager.Singleton.ConnectedClientsIds.Count)
        {
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
    /// Attempts to bind spawn positions from the loaded level scene.
    /// Looks for objects named "p1Spawn" and "p2Spawn" (case-sensitive).
    /// </summary>
    private void CacheSpawnPositions()
    {
        // Find all transforms in the currently loaded scenes
        var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);

        var p1 = allTransforms.FirstOrDefault(t => t.name == "p1Spawn");
        var p2 = allTransforms.FirstOrDefault(t => t.name == "p2Spawn");
        // Chest and flag positions are not needed; placed directly in scenes.

        spawnPos ??= new List<Transform>();
        spawnPos.Clear();
        // No chest positions list

        if (p1 != null)
        {
            spawnPos.Add(p1);
        }
        if (p2 != null)
        {
            spawnPos.Add(p2);
        }

        // No chest positions aggregation

    }
    // Removed dynamic chest/flag spawning

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
            return;
        }

        // Check if ItemSpawnManager already exists
        if (ItemSpawnManager.Instance != null)
        {
            return;
        }

        var instance = Instantiate(itemSpawnManagerPrefab);

        if (instance.TryGetComponent<NetworkObject>(out var networkObject))
        {
            networkObject.Spawn(true);
        }
        else
        {
            return;
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
            UpdateFlagDisplay(ownFlagParent, null);
            UpdateFlagDisplay(otherFlagParent, null);
            return;
        }

        Team otherTeam = GetOpposingTeam(localPlayerTeam);
        int ownScore = GetScore(localPlayerTeam);
        int otherScore = GetScore(otherTeam);

        UpdateScoreLabels(ownScore, otherScore);
        UpdateFlagDisplay(ownFlagParent, localPlayerTeam == Team.Blue ? blueFlagSprite : redFlagSprite);
        UpdateFlagDisplay(otherFlagParent, otherTeam == Team.Blue ? blueFlagSprite : redFlagSprite);
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

    private void UpdateFlagDisplay(VisualElement parent, Sprite flagSprite)
    {
        if (parent == null)
        {
            return;
        }

        parent.Clear();

        if (flagSprite != null)
        {
            parent.style.backgroundImage = new StyleBackground(flagSprite);
            parent.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            parent.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            parent.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            parent.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }
        else
        {
            parent.style.backgroundImage = null;
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
        foreach (var player in players)
        {
            var teamMember = player.GetComponent<TeamMember>();
            Team playerTeam = teamMember != null ? teamMember.CurrentTeam : Team.None;
            bool isWinner = playerTeam == winningTeam && winningTeam != Team.None;
            player.PlayOutcomeAnimation(isWinner, winTriggerName, loseTriggerName, winStateName, loseStateName, idleStateName);
        }

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
        if (endGameContainer != null)
        {
            endGameContainer.style.display = DisplayStyle.Flex;
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
            TriggerReturnToLobbyRpc();
        }
    }

    /// <summary>
    /// Updates the btnEnd label text with remaining seconds.
    /// </summary>
    private void UpdateBtnEndLabel(int secondsRemaining)
    {
        if (btnEnd != null)
        {
            btnEnd.text = $"Go back to lobby ({secondsRemaining}s)";
        }
    }

    /// <summary>
    /// Handles the pause menu continue button.
    /// </summary>
    private void OnPauseContinueClicked()
    {
        SetPauseMenuVisible(false);
    }

    /// <summary>
    /// Handles the pause menu end button.
    /// </summary>
    private void OnPauseEndClicked()
    {
        SetPauseMenuVisible(false);

        if (exitTriggered)
        {
            return;
        }

        if (IsHost)
        {
            TriggerReturnToLobbyRpc();
        }
        else
        {
            RequestReturnToLobbyRpc();
        }
    }

    /// <summary>
    /// Called when btnEnd is clicked. Only host can trigger this.
    /// </summary>
    private void OnBtnEndClicked()
    {
        if (!IsHost)
        {
            return;
        }

        if (exitTriggered)
        {
            return;
        }

        TriggerReturnToLobbyRpc();
    }

    /// <summary>
    /// Clients request the host to return everyone to the lobby.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestReturnToLobbyRpc()
    {
        if (!IsServer)
        {
            return;
        }

        if (exitTriggered)
        {
            return;
        }

        TriggerReturnToLobbyRpc();
    }

    /// <summary>
    /// RPC to trigger all clients and host to return to lobby.
    /// </summary>
    [Rpc(SendTo.Everyone)]
    private void TriggerReturnToLobbyRpc()
    {
        if (exitTriggered)
        {
            return;
        }

        exitTriggered = true;

        SetPauseMenuVisible(false);

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        UpdateBtnEndLabel(0);

        if (btnEnd != null)
        {
            btnEnd.SetEnabled(false);
        }

        ShutdownNetworkAndLoadLobby();
    }

    /// <summary>
    /// Shuts down the network and loads the lobby scene.
    /// </summary>
    private void ShutdownNetworkAndLoadLobby()
    {
        // Unload any loaded level scenes before returning to lobby
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.name != "GameScene" && scene.name != lobbySceneName)
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

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
    /// Updates UI layout based on game mode (single-player vs multiplayer).
    /// In single-player, hides the opposing team's score display and VS label.
    /// </summary>
    private void UpdateUIForGameMode()
    {
        int playerCount = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients.Count : 0;
        bool isSinglePlayer = playerCount == 1;

        if (otherTeamContainer != null)
        {
            otherTeamContainer.style.display = isSinglePlayer ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (vsLabel != null)
        {
            vsLabel.style.display = isSinglePlayer ? DisplayStyle.None : DisplayStyle.Flex;
        }

        Debug.Log($"[GameController] Game mode: {(isSinglePlayer ? "Single-Player" : "Multiplayer")} ({playerCount} players connected)");
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
