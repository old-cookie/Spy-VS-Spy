using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Manages mini games that can be played when picking up a flag.
/// Attach to a GameObject in the scene and assign available mini games in the Inspector.
/// </summary>
public class MiniGameManager : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of the MiniGameManager.
    /// </summary>
    public static MiniGameManager Instance { get; private set; }

    /// <summary>
    /// List of available mini game prefabs that can be randomly selected.
    /// Assign mini game prefabs in the Inspector.
    /// </summary>
    [SerializeField]
    private List<MiniGame> availableMiniGamePrefabs = new();

    /// <summary>
    /// The currently active mini game instance.
    /// </summary>
    private MiniGame currentMiniGameInstance;

    /// <summary>
    /// The player currently playing a mini game.
    /// </summary>
    private PlayerController currentPlayer;

    /// <summary>
    /// Callback to execute when mini game ends with success (result = 1).
    /// </summary>
    private Action onMiniGameSuccess;

    /// <summary>
    /// Whether a mini game is currently being played.
    /// </summary>
    public bool IsMiniGameActive => currentMiniGameInstance != null && currentMiniGameInstance.IsActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Starts a random mini game for the specified player.
    /// </summary>
    /// <param name="player">The player controller to start the game for.</param>
    /// <param name="onSuccess">Optional callback to execute when mini game ends with success (result = 1).</param>
    /// <returns>True if a mini game was started, false otherwise.</returns>
    public bool StartRandomMiniGame(PlayerController player, Action onSuccess = null)
    {
        if (player == null)
        {
            Debug.LogWarning("[MiniGameManager] Cannot start mini game: player is null.");
            return false;
        }

        if (availableMiniGamePrefabs == null || availableMiniGamePrefabs.Count == 0)
        {
            Debug.LogWarning("[MiniGameManager] No mini games available.");
            return false;
        }

        if (IsMiniGameActive)
        {
            Debug.LogWarning("[MiniGameManager] A mini game is already active.");
            return false;
        }

        // Select a random mini game prefab
        var randomIndex = UnityEngine.Random.Range(0, availableMiniGamePrefabs.Count);
        var selectedPrefab = availableMiniGamePrefabs[randomIndex];

        if (selectedPrefab == null)
        {
            Debug.LogWarning("[MiniGameManager] Selected mini game prefab is null.");
            return false;
        }

        // Instantiate the mini game
        currentMiniGameInstance = Instantiate(selectedPrefab, transform);
        currentPlayer = player;
        onMiniGameSuccess = onSuccess;

        // Subscribe to the end event
        currentMiniGameInstance.OnMiniGameEnded += OnMiniGameEnded;

        // Notify player that mini game is starting
        player.SetPlayingMiniGame(true);

        // Start the mini game
        currentMiniGameInstance.StartGame(player);

        Debug.Log($"[MiniGameManager] Started mini game: {currentMiniGameInstance.MiniGameName}");
        return true;
    }

    /// <summary>
    /// Called when the current mini game ends.
    /// </summary>
    /// <param name="result">1 = completed, -1 = failed, 0 = exited via ESC</param>
    private void OnMiniGameEnded(int result)
    {
        if (currentMiniGameInstance != null)
        {
            currentMiniGameInstance.OnMiniGameEnded -= OnMiniGameEnded;
        }

        // Notify player that mini game has ended
        if (currentPlayer != null)
        {
            currentPlayer.SetPlayingMiniGame(false);
            currentPlayer.OnMiniGameResult(result);
        }

        var gameName = currentMiniGameInstance != null ? currentMiniGameInstance.MiniGameName : "Unknown";
        var resultText = result switch
        {
            1 => "completed",
            -1 => "failed",
            0 => "exited",
            _ => "unknown"
        };

        Debug.Log($"[MiniGameManager] Mini game '{gameName}' ended with result: {resultText} ({result})");

        // Execute success callback if player completed the game
        if (result == 1 && onMiniGameSuccess != null)
        {
            onMiniGameSuccess.Invoke();
        }

        // Destroy the mini game instance
        if (currentMiniGameInstance != null)
        {
            Destroy(currentMiniGameInstance.gameObject);
        }

        currentMiniGameInstance = null;
        currentPlayer = null;
        onMiniGameSuccess = null;
    }

    /// <summary>
    /// Exits the current mini game if one is active. Called when player presses ESC.
    /// </summary>
    public void ExitCurrentMiniGame()
    {
        if (currentMiniGameInstance != null && currentMiniGameInstance.IsActive)
        {
            currentMiniGameInstance.ExitGame();
        }
    }

    /// <summary>
    /// Gets the name of the currently active mini game.
    /// </summary>
    /// <returns>The name of the current mini game, or null if none is active.</returns>
    public string GetCurrentMiniGameName()
    {
        return currentMiniGameInstance != null ? currentMiniGameInstance.MiniGameName : null;
    }
}
