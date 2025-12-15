using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages the lobby UI for hosting and joining multiplayer games.
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    private UIDocument uiDocument;

    private VisualElement mainPanel;
    private VisualElement modePanel;
    private VisualElement singlePlayPanel;
    private VisualElement multiPlayPanel;
    private VisualElement hostWaitingPanel;
    private VisualElement clientWaitingPanel;
    private TextField ipInput;
    private DropdownField levelDropdown;
    private DropdownField singleLevelDropdown;
    private Button startGameButton;
    private Button singlePlayButton;
    private Button multiPlayButton;
    private Button singleStartButton;
    private Button startButton;
    private Button hostButton;
    private Button joinButton;
    private Button backFromModeButton;
    private Button backFromSingleButton;
    private Button backFromMultiButton;
    private Button backFromHostButton;
    private Button backFromClientButton;
    private Label playerNumLabel;
    private Label clientPlayerNumLabel;

    /// <summary>
    /// Available level scenes (assign from Assets/Scenes or Assets/Levels).
    /// </summary>
#if UNITY_EDITOR
    [SerializeField]
    private SceneAsset[] levelScenes;
#endif

    /// <summary>
    /// Scene names extracted from levelScenes, used at runtime.
    /// </summary>
    [SerializeField, HideInInspector]
    private List<string> levelSceneNames = new List<string>();

    // startButton handled via UI Toolkit

#if UNITY_EDITOR
    // gameScene removed - now loading level scenes directly
#endif

    // playerNumLabel handled via UI Toolkit

    /// <summary>
    /// Whether this client is the host of the game.
    /// </summary>
    public bool isHost = false;

    /// <summary>
    /// Currently selected level prefab name.
    /// </summary>
    private string selectedLevelName = string.Empty;

    private void Start()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("[LobbyUIManager] UIDocument not found on GameObject.");
            return;
        }

        var root = uiDocument.rootVisualElement;
        mainPanel = root.Q<VisualElement>("mainPanel");
        modePanel = root.Q<VisualElement>("modePanel");
        singlePlayPanel = root.Q<VisualElement>("singlePlayPanel");
        multiPlayPanel = root.Q<VisualElement>("multiPlayPanel");
        hostWaitingPanel = root.Q<VisualElement>("hostWaitingPanel");
        clientWaitingPanel = root.Q<VisualElement>("clientWaitingPanel");
        ipInput = root.Q<TextField>("ipInput");
        levelDropdown = root.Q<DropdownField>("levelDropdown");
        singleLevelDropdown = root.Q<DropdownField>("singleLevelDropdown");
        startGameButton = root.Q<Button>("startGameButton");
        singlePlayButton = root.Q<Button>("singlePlayButton");
        multiPlayButton = root.Q<Button>("multiPlayButton");
        singleStartButton = root.Q<Button>("singleStartButton");
        startButton = root.Q<Button>("startButton");
        hostButton = root.Q<Button>("hostButton");
        joinButton = root.Q<Button>("joinButton");
        backFromModeButton = root.Q<Button>("backFromModeButton");
        backFromSingleButton = root.Q<Button>("backFromSingleButton");
        backFromMultiButton = root.Q<Button>("backFromMultiButton");
        backFromHostButton = root.Q<Button>("backFromHostButton");
        backFromClientButton = root.Q<Button>("backFromClientButton");
        playerNumLabel = root.Q<Label>("playerNumText");
        clientPlayerNumLabel = root.Q<Label>("clientPlayerNumText");

        // Show main panel first
        ShowMainPanel();

        // Bind button events
        if (startGameButton != null) startGameButton.clicked += StartGameBtnOnClick;
        if (singlePlayButton != null) singlePlayButton.clicked += SinglePlayBtnOnClick;
        if (multiPlayButton != null) multiPlayButton.clicked += MultiPlayBtnOnClick;
        if (singleStartButton != null) singleStartButton.clicked += SingleStartBtnOnClick;
        if (hostButton != null) hostButton.clicked += HostBtnOnClick;
        if (joinButton != null) joinButton.clicked += JoinBtnOnClick;
        if (startButton != null) startButton.clicked += StartBtnOnClick;
        if (backFromModeButton != null) backFromModeButton.clicked += BackFromModeBtnOnClick;
        if (backFromSingleButton != null) backFromSingleButton.clicked += BackFromSingleBtnOnClick;
        if (backFromMultiButton != null) backFromMultiButton.clicked += BackFromMultiBtnOnClick;
        if (backFromHostButton != null) backFromHostButton.clicked += BackFromHostBtnOnClick;
        if (backFromClientButton != null) backFromClientButton.clicked += BackFromClientBtnOnClick;

        // Bind dropdown change
        if (levelDropdown != null)
        {
            levelDropdown.RegisterValueChangedCallback(evt =>
            {
                OnLevelDropdownChanged(levelDropdown.choices.IndexOf(evt.newValue));
            });
        }

        if (singleLevelDropdown != null)
        {
            singleLevelDropdown.RegisterValueChangedCallback(evt =>
            {
                OnSingleLevelDropdownChanged(singleLevelDropdown.choices.IndexOf(evt.newValue));
            });
        }

        BuildLevelDropdownOptions();
    }

    private void Update()
    {
        bool hostWaiting = hostWaitingPanel != null && hostWaitingPanel.resolvedStyle.display != DisplayStyle.None;
        bool clientWaiting = clientWaitingPanel != null && clientWaitingPanel.resolvedStyle.display != DisplayStyle.None;

        if (hostWaiting || clientWaiting)
        {
            UpdatePlayerCount();
            CheckStartCondition();
        }
    }

    /// <summary>
    /// Updates the player count text display.
    /// </summary>
    private void UpdatePlayerCount()
    {
        int playerCount = NetworkManager.Singleton.ConnectedClients.Count;
        
        if (playerNumLabel != null)
        {
            playerNumLabel.text = "Joined Players: " + playerCount;
        }

        if (clientPlayerNumLabel != null)
        {
            clientPlayerNumLabel.text = "Joined Players: " + playerCount;
        }
    }

    /// <summary>
    /// Called when back button is clicked from mode panel. Returns to main panel.
    /// </summary>
    private void BackFromModeBtnOnClick()
    {
        ShowMainPanel();
    }

    /// <summary>
    /// Called when back button is clicked from single-play panel. Returns to mode panel.
    /// </summary>
    private void BackFromSingleBtnOnClick()
    {
        ShowModePanel();
    }

    /// <summary>
    /// Called when back button is clicked from multi-play panel. Returns to mode panel.
    /// </summary>
    private void BackFromMultiBtnOnClick()
    {
        ShowModePanel();
    }

    /// <summary>
    /// Called when back button is clicked from host waiting panel. Shuts down host and returns to mode panel.
    /// </summary>
    private void BackFromHostBtnOnClick()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        isHost = false;
        ShowModePanel();
    }

    /// <summary>
    /// Called when back button is clicked from client waiting panel. Shuts down client and returns to mode panel.
    /// </summary>
    private void BackFromClientBtnOnClick()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        isHost = false;
        ShowModePanel();
    }

    /// <summary>
    /// Shows the main panel with Start Game button.
    /// </summary>
    private void ShowMainPanel()
    {
        SetVisible(mainPanel, true);
        SetVisible(modePanel, false);
        SetVisible(singlePlayPanel, false);
        SetVisible(multiPlayPanel, false);
        SetVisible(hostWaitingPanel, false);
        SetVisible(clientWaitingPanel, false);
    }

    /// <summary>
    /// Shows the mode selection panel (Single Play / Multi Play).
    /// </summary>
    private void ShowModePanel()
    {
        SetVisible(mainPanel, false);
        SetVisible(modePanel, true);
        SetVisible(singlePlayPanel, false);
        SetVisible(multiPlayPanel, false);
        SetVisible(hostWaitingPanel, false);
        SetVisible(clientWaitingPanel, false);
    }

    /// <summary>
    /// Shows the single-player panel.
    /// </summary>
    private void ShowSinglePlayPanel()
    {
        SetVisible(mainPanel, false);
        SetVisible(modePanel, false);
        SetVisible(singlePlayPanel, true);
        SetVisible(multiPlayPanel, false);
        SetVisible(hostWaitingPanel, false);
        SetVisible(clientWaitingPanel, false);
    }

    /// <summary>
    /// Shows the multi-player mode selection panel (Host / Join).
    /// </summary>
    private void ShowMultiPlayPanel()
    {
        SetVisible(mainPanel, false);
        SetVisible(modePanel, false);
        SetVisible(singlePlayPanel, false);
        SetVisible(multiPlayPanel, true);
        SetVisible(hostWaitingPanel, false);
        SetVisible(clientWaitingPanel, false);
    }

    /// <summary>
    /// Called when the Start Game button is clicked. Shows mode selection.
    /// </summary>
    private void StartGameBtnOnClick()
    {
        ShowModePanel();
    }

    /// <summary>
    /// Called when Single Play button is clicked. Shows single-player panel.
    /// </summary>
    private void SinglePlayBtnOnClick()
    {
        ShowSinglePlayPanel();
    }

    /// <summary>
    /// Called when Multi Play button is clicked. Shows multi-play options.
    /// </summary>
    private void MultiPlayBtnOnClick()
    {
        ShowMultiPlayPanel();
    }

    /// <summary>
    /// Called when the single-player start button is clicked.
    /// </summary>
    private void SingleStartBtnOnClick()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", 7777);
        }

        NetworkManager.Singleton.StartHost();
        isHost = true;

        // Get selected level from single-play dropdown
        if (singleLevelDropdown != null && !string.IsNullOrWhiteSpace(singleLevelDropdown.value))
        {
            SetSelectedLevel(singleLevelDropdown.value);
        }
        else if (singleLevelDropdown != null && singleLevelDropdown.choices != null && singleLevelDropdown.choices.Count > 0)
        {
            SetSelectedLevel(singleLevelDropdown.choices[0]);
        }
        else
        {
            SetSelectedLevel("Lv1");
        }

        // Sync selection to state and load the scene immediately
        StartCoroutine(WaitAndSyncLevelSelection());

        string levelToLoad = string.IsNullOrWhiteSpace(selectedLevelName) ? "Lv1" : selectedLevelName;
        Debug.Log($"[LobbyUIManager] Single-player loading level scene: {levelToLoad}");
        NetworkManager.Singleton.SceneManager.LoadScene(levelToLoad, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    /// <summary>
    /// Called when the host button is clicked. Starts the network host and shows the host waiting panel.
    /// </summary>
    public void HostBtnOnClick()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", 7777);
        }
        NetworkManager.Singleton.StartHost();
        isHost = true;

        ShowHostWaitingPanel();

        // Sync the already selected level to LevelSelectionState now that we are the host
        StartCoroutine(WaitAndSyncLevelSelection());
    }

    /// <summary>
    /// Called when the join button is clicked. Connects to the host and shows the client waiting panel.
    /// </summary>
    public void JoinBtnOnClick()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            var addr = (ipInput != null && !string.IsNullOrEmpty(ipInput.value)) ? ipInput.value : "127.0.0.1";
            transport.SetConnectionData(addr, 7777);
        }

        NetworkManager.Singleton.StartClient();
        isHost = false;

        ShowClientWaitingPanel();
    }

    /// <summary>
    /// Shows the host waiting panel with level dropdown and start button.
    /// </summary>
    private void ShowHostWaitingPanel()
    {
        SetVisible(mainPanel, false);
        SetVisible(modePanel, false);
        SetVisible(singlePlayPanel, false);
        SetVisible(multiPlayPanel, false);
        SetVisible(hostWaitingPanel, true);
        SetVisible(clientWaitingPanel, false);
    }

    /// <summary>
    /// Shows the client waiting panel (just player count).
    /// </summary>
    private void ShowClientWaitingPanel()
    {
        SetVisible(mainPanel, false);
        SetVisible(modePanel, false);
        SetVisible(singlePlayPanel, false);
        SetVisible(multiPlayPanel, false);
        SetVisible(hostWaitingPanel, false);
        SetVisible(clientWaitingPanel, true);
    }

    /// <summary>
    /// Called when the start button is clicked. Loads the selected level scene for all clients.
    /// </summary>
    public void StartBtnOnClick()
    {
        if (isHost)
        {
            string levelToLoad = string.IsNullOrWhiteSpace(selectedLevelName) ? "Lv1" : selectedLevelName;
            Debug.Log($"[LobbyUIManager] Loading level scene: {levelToLoad}");
            NetworkManager.Singleton.SceneManager.LoadScene(levelToLoad, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Checks if the game can be started and shows the start button if conditions are met.
    /// </summary>
    private void CheckStartCondition()
    {
        bool readyToStart = NetworkManager.Singleton.ConnectedClients.Count == 2 && isHost && !string.IsNullOrWhiteSpace(selectedLevelName);
        SetVisible(startButton, readyToStart);
    }

    /// <summary>
    /// Called by the dropdown when the host selects a level option.
    /// </summary>
    /// <param name="index">Dropdown option index.</param>
    public void OnLevelDropdownChanged(int index)
    {
        Debug.Log($"[LobbyUIManager] OnLevelDropdownChanged called with index: {index}");
        
        if (levelDropdown == null)
        {
            return;
        }

        var choices = levelDropdown.choices ?? new List<string>();
        if (index < 0 || index >= choices.Count)
        {
            return;
        }

        string chosen = choices[index];
        Debug.Log($"[LobbyUIManager] Level selected: {chosen}");
        SetSelectedLevel(chosen);
    }

    /// <summary>
    /// Called when the single-player dropdown changes selection.
    /// </summary>
    /// <param name="index">Dropdown option index.</param>
    public void OnSingleLevelDropdownChanged(int index)
    {
        if (singleLevelDropdown == null)
        {
            return;
        }

        var choices = singleLevelDropdown.choices ?? new List<string>();
        if (index < 0 || index >= choices.Count)
        {
            return;
        }

        string chosen = choices[index];
        SetSelectedLevel(chosen);
    }

    /// <summary>
    /// Builds dropdown options from assigned level scenes.
    /// </summary>
    private void BuildLevelDropdownOptions()
    {
        var names = levelSceneNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        if (names.Count == 0)
        {
            names.Add("Lv1");
        }

        if (levelDropdown != null)
        {
            levelDropdown.choices = names;
            levelDropdown.SetValueWithoutNotify(names[0]);
        }

        if (singleLevelDropdown != null)
        {
            singleLevelDropdown.choices = names;
            singleLevelDropdown.SetValueWithoutNotify(names[0]);
        }

        SetSelectedLevel(names[0]);
    }

    /// <summary>
    /// Applies the chosen level name and syncs it to the selection state (host only).
    /// </summary>
    private void SetSelectedLevel(string levelName)
    {
        selectedLevelName = levelName;
        SyncSelectedLevelToState();
    }

    /// <summary>
    /// Syncs the current selectedLevelName to LevelSelectionState if we are the host.
    /// </summary>
    private void SyncSelectedLevelToState()
    {
        if (isHost && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && LevelSelectionState.Instance != null)
        {
            LevelSelectionState.Instance.SetSelectedLevelName(selectedLevelName);
            Debug.Log($"[LobbyUIManager] Synced level selection: {selectedLevelName}");
        }
    }

    /// <summary>
    /// Coroutine to wait for LevelSelectionState.Instance to be available, then sync the level.
    /// </summary>
    private IEnumerator WaitAndSyncLevelSelection()
    {
        // Wait until LevelSelectionState.Instance is available (NetworkObject spawned)
        float timeout = 5f;
        float elapsed = 0f;
        while (LevelSelectionState.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (LevelSelectionState.Instance != null)
        {
            SyncSelectedLevelToState();
        }
        else
        {
            Debug.LogWarning("[LobbyUIManager] LevelSelectionState.Instance not found after timeout. Level selection may not sync correctly.");
        }
    }

    /// <summary>
    /// Shows or hides the level dropdown.
    /// </summary>
    /// <param name="visible">Whether the dropdown should be visible.</param>
    private void SetDropdownVisible(bool visible)
    {
        if (levelDropdown != null)
        {
            levelDropdown.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void SetVisible(VisualElement element, bool visible)
    {
        if (element == null) return;
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetVisible(Button element, bool visible)
    {
        if (element == null) return;
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Called when values are changed in the inspector. Extracts scene names from levelScenes.
    /// </summary>
    private void OnValidate()
    {
        // Extract scene names from levelScenes
        levelSceneNames.Clear();
        if (levelScenes != null)
        {
            foreach (var sceneAsset in levelScenes)
            {
                if (sceneAsset != null)
                {
                    levelSceneNames.Add(sceneAsset.name);
                }
            }
        }
    }
#endif
}
