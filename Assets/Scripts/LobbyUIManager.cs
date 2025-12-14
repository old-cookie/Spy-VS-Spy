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

    private VisualElement startPanel;
    private VisualElement waitingPanel;
    private TextField ipInput;
    private DropdownField levelDropdown;
    private Button startButton;
    private Button hostButton;
    private Button joinButton;
    private Label playerNumLabel;

    /// <summary>
    /// Available level prefabs (assign from Assets/Levels).
    /// </summary>
    [SerializeField]
    private List<GameObject> levelPrefabs = new List<GameObject>();

    // startButton handled via UI Toolkit

#if UNITY_EDITOR
    /// <summary>
    /// Reference to the game scene asset for drag-and-drop assignment in the editor.
    /// </summary>
    [SerializeField]
    private SceneAsset gameScene;
#endif

    /// <summary>
    /// The name of the game scene to load when starting the game.
    /// </summary>
    [SerializeField, HideInInspector]
    private string gameSceneName;

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
        startPanel = root.Q<VisualElement>("startPanel");
        waitingPanel = root.Q<VisualElement>("waitingPanel");
        ipInput = root.Q<TextField>("ipInput");
        levelDropdown = root.Q<DropdownField>("levelDropdown");
        startButton = root.Q<Button>("startButton");
        hostButton = root.Q<Button>("hostButton");
        joinButton = root.Q<Button>("joinButton");
        playerNumLabel = root.Q<Label>("playerNumText");

        SetVisible(startPanel, true);
        SetVisible(waitingPanel, false);
        SetVisible(startButton, false);

        // Bind button events
        if (hostButton != null) hostButton.clicked += HostBtnOnClick;
        if (joinButton != null) joinButton.clicked += JoinBtnOnClick;
        if (startButton != null) startButton.clicked += StartBtnOnClick;

        // Bind dropdown change
        if (levelDropdown != null)
        {
            levelDropdown.RegisterValueChangedCallback(evt =>
            {
                OnLevelDropdownChanged(levelDropdown.choices.IndexOf(evt.newValue));
            });
        }

        BuildLevelDropdownOptions();
    }

    private void Update()
    {
        if (waitingPanel != null && waitingPanel.resolvedStyle.display != DisplayStyle.None)
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
        if (playerNumLabel != null)
        {
            playerNumLabel.text = "Joined Players: " + NetworkManager.Singleton.ConnectedClients.Count;
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
    /// Called when the start button is clicked. Loads the game scene for all clients.
    /// </summary>
    public void StartBtnOnClick()
    {
        if (isHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Called when the host button is clicked. Starts the network host and shows the waiting panel.
    /// </summary>
    public void HostBtnOnClick()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", 7777);
        }
        NetworkManager.Singleton.StartHost();
        SetVisible(startPanel, false);
        SetVisible(waitingPanel, true);
        isHost = true;

        // Host can see and use the dropdown to select levels
        SetDropdownVisible(true);

        // Sync the already selected level to LevelSelectionState now that we are the host
        // Use coroutine to wait for LevelSelectionState.Instance to be available
        StartCoroutine(WaitAndSyncLevelSelection());
    }

    /// <summary>
    /// Called when the join button is clicked. Connects to the host and shows the waiting panel.
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
        SetVisible(startPanel, false);
        SetVisible(waitingPanel, true);

        // Clients cannot see or change level selection, only host can
        SetDropdownVisible(false);
        isHost = false;
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
    /// Builds dropdown options from assigned level prefabs.
    /// </summary>
    private void BuildLevelDropdownOptions()
    {
        if (levelDropdown == null)
        {
            return;
        }

        var names = levelPrefabs
            .Where(p => p != null)
            .Select(p => p.name)
            .ToList();

        if (names.Count == 0)
        {
            names.Add("Demo");
        }

        levelDropdown.choices = names;
        levelDropdown.SetValueWithoutNotify(names[0]);
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
    /// Called when values are changed in the inspector. Syncs the game scene name from the scene asset.
    /// </summary>
    private void OnValidate()
    {
        if (gameScene != null)
        {
            gameSceneName = gameScene.name;
        }
    }
#endif
}
