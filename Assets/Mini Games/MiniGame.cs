using UnityEngine;
using System;

/// <summary>
/// Base class for mini games that can be played when picking up a flag.
/// </summary>
public abstract class MiniGame : MonoBehaviour
{
    /// <summary>
    /// The display name of this mini game, set in the Inspector.
    /// </summary>
    [SerializeField]
    private string miniGameName = "Mini Game";

    /// <summary>
    /// Gets the name of this mini game.
    /// </summary>
    public string MiniGameName => miniGameName;

    /// <summary>
    /// Event fired when the mini game ends.
    /// Result: 1 = completed successfully, -1 = failed, 0 = exited via ESC
    /// </summary>
    public event Action<int> OnMiniGameEnded;

    /// <summary>
    /// Whether this mini game is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// The player controller that started this mini game.
    /// </summary>
    protected PlayerController CurrentPlayer { get; private set; }

    /// <summary>
    /// Starts the mini game for the specified player.
    /// </summary>
    /// <param name="player">The player controller starting the game.</param>
    public virtual void StartGame(PlayerController player)
    {
        if (IsActive)
        {
            return;
        }

        CurrentPlayer = player;
        IsActive = true;
        gameObject.SetActive(true);
        OnGameStart();
    }

    /// <summary>
    /// Called when the game starts. Override to implement game-specific initialization.
    /// </summary>
    protected virtual void OnGameStart()
    {
    }

    /// <summary>
    /// Ends the mini game with the specified result.
    /// </summary>
    /// <param name="result">1 = completed, -1 = failed, 0 = exited</param>
    protected void EndGame(int result)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        OnGameEnd(result);
        OnMiniGameEnded?.Invoke(result);
        CurrentPlayer = null;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Called when the game ends. Override to implement game-specific cleanup.
    /// </summary>
    /// <param name="result">The result of the game.</param>
    protected virtual void OnGameEnd(int result)
    {
    }

    /// <summary>
    /// Called to exit the game via ESC key. Returns result 0.
    /// </summary>
    public void ExitGame()
    {
        if (!IsActive)
        {
            return;
        }

        EndGame(0);
    }

    /// <summary>
    /// Called when the player completes the game successfully. Returns result 1.
    /// </summary>
    protected void CompleteGame()
    {
        EndGame(1);
    }

    /// <summary>
    /// Called when the player fails the game. Returns result -1.
    /// </summary>
    protected void FailGame()
    {
        EndGame(-1);
    }

    /// <summary>
    /// Update is called once per frame. Handle ESC input here if needed.
    /// </summary>
    protected virtual void Update()
    {
        if (!IsActive)
        {
            return;
        }

        // ESC key handling is done by PlayerController/MiniGameManager
        // Derived classes should implement their own game logic here
    }
}
