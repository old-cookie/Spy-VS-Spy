using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A simple demo mini game that completes when player presses Space key.
/// Use this as a template for creating actual mini games.
/// </summary>
public class DemoMiniGame : MiniGame
{
    /// <summary>
    /// Time limit for the mini game in seconds. Set to 0 for no limit.
    /// </summary>
    [SerializeField]
    private float timeLimit = 10f;

    [Header("UI References")]
    /// <summary>
    /// Canvas for the mini game UI.
    /// </summary>
    [SerializeField]
    private Canvas miniGameCanvas;

    /// <summary>
    /// Timer component for managing countdown.
    /// </summary>
    [SerializeField]
    private MiniGameTimer timer;

    /// <summary>
    /// Button to finish/complete the mini game.
    /// </summary>
    [SerializeField]
    private Button btnFinish;

    /// <summary>
    /// Button to fail the mini game.
    /// </summary>
    [SerializeField]
    private Button btnFail;

    private void Awake()
    {
        // Setup button listeners
        if (btnFinish != null)
        {
            btnFinish.onClick.AddListener(OnFinishClicked);
        }

        if (btnFail != null)
        {
            btnFail.onClick.AddListener(OnFailClicked);
        }

        // Setup timer event
        if (timer != null)
        {
            timer.OnTimeUp += OnTimerExpired;
        }
    }

    private void OnDestroy()
    {
        // Cleanup button listeners
        if (btnFinish != null)
        {
            btnFinish.onClick.RemoveListener(OnFinishClicked);
        }

        if (btnFail != null)
        {
            btnFail.onClick.RemoveListener(OnFailClicked);
        }

        // Cleanup timer event
        if (timer != null)
        {
            timer.OnTimeUp -= OnTimerExpired;
        }
    }

    private void OnTimerExpired()
    {
        if (IsActive)
        {
            FailGame();
        }
    }

    protected override void OnGameStart()
    {
        base.OnGameStart();
        
        // Show UI
        if (miniGameCanvas != null)
        {
            miniGameCanvas.gameObject.SetActive(true);
        }

        // Start timer if time limit is set
        if (timer != null && timeLimit > 0f)
        {
            timer.SetDisplayVisible(true);
            timer.StartTimer(timeLimit);
        }
        
        Debug.Log($"[DemoMiniGame] Started - Time limit: {timeLimit}s");
    }

    private void OnFinishClicked()
    {
        if (!IsActive)
        {
            return;
        }

        CompleteGame();
    }

    private void OnFailClicked()
    {
        if (!IsActive)
        {
            return;
        }

        FailGame();
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void OnGameEnd(int result)
    {
        base.OnGameEnd(result);

        // Stop and hide timer
        if (timer != null)
        {
            timer.StopTimer();
            timer.SetDisplayVisible(false);
        }

        // Hide UI
        if (miniGameCanvas != null)
        {
            miniGameCanvas.gameObject.SetActive(false);
        }

        var resultText = result switch
        {
            1 => "You completed the mini game!",
            -1 => "Time's up! You failed.",
            0 => "You exited the mini game.",
            _ => "Mini game ended."
        };

        Debug.Log($"[DemoMiniGame] {resultText}");
    }
}
