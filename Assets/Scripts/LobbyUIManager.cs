using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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
    /// <summary>
    /// Input field for entering the host IP address when joining a game.
    /// </summary>
    public InputField ipInput;

    /// <summary>
    /// Panel displayed at the start with host/join options.
    /// </summary>
    public GameObject startPanel;

    /// <summary>
    /// Panel displayed while waiting for players to join.
    /// </summary>
    public GameObject waitingPanel;

    /// <summary>
    /// Dropdown for picking which level prefab to load.
    /// </summary>
    public Dropdown levelDropdown;

    /// <summary>
    /// Available level prefabs (assign from Assets/Levels).
    /// </summary>
    [SerializeField]
    private List<GameObject> levelPrefabs = new List<GameObject>();

    /// <summary>
    /// Button to start the game, only visible to the host when enough players have joined.
    /// </summary>
    public GameObject startButton;

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

    /// <summary>
    /// Text displaying the current number of connected players.
    /// </summary>
    public Text playerNumText;

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
        startPanel.SetActive(true);
        waitingPanel.SetActive(false);
        startButton.SetActive(false);

        BuildLevelDropdownOptions();
    }

    private void Update()
    {
        if (waitingPanel.activeSelf)
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
        playerNumText.text = "Joined Players: " + NetworkManager.Singleton.ConnectedClients.Count;
    }

    /// <summary>
    /// Checks if the game can be started and shows the start button if conditions are met.
    /// </summary>
    private void CheckStartCondition()
    {
        bool readyToStart = NetworkManager.Singleton.ConnectedClients.Count == 2 && isHost && !string.IsNullOrWhiteSpace(selectedLevelName);
        startButton.SetActive(readyToStart);
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
        NetworkManager.Singleton.StartHost();
        startPanel.SetActive(false);
        waitingPanel.SetActive(true);
        isHost = true;
    }

    /// <summary>
    /// Called when the join button is clicked. Connects to the host and shows the waiting panel.
    /// </summary>
    public void JoinBtnOnClick()
    {
        if (ipInput.text != "")
        {
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipInput.text, 7777);
        }

        NetworkManager.Singleton.StartClient();
        startPanel.SetActive(false);
        waitingPanel.SetActive(true);
        isHost = false;
    }

    /// <summary>
    /// Called by the dropdown when the host selects a level option.
    /// </summary>
    /// <param name="index">Dropdown option index.</param>
    public void OnLevelDropdownChanged(int index)
    {
        if (levelDropdown == null)
        {
            return;
        }

        if (index < 0 || index >= levelDropdown.options.Count)
        {
            return;
        }

        string chosen = levelDropdown.options[index].text;
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

        levelDropdown.ClearOptions();
        levelDropdown.AddOptions(names);

        SetSelectedLevel(names[0]);
        levelDropdown.SetValueWithoutNotify(0);
    }

    /// <summary>
    /// Applies the chosen level name and syncs it to the selection state (host only).
    /// </summary>
    private void SetSelectedLevel(string levelName)
    {
        selectedLevelName = levelName;

        if (isHost && NetworkManager.Singleton.IsHost && LevelSelectionState.Instance != null)
        {
            LevelSelectionState.Instance.SetSelectedLevelName(levelName);
        }
    }

    /// <summary>
    /// Shows the level selection panel for the host once in the waiting state.
    /// </summary>
    private void UpdateLevelSelectPanelVisibility() { }

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
