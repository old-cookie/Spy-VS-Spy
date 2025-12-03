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
    /// Title text display.
    /// </summary>
    [SerializeField]
    private Text txtTitle;

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

    private float remainingTime;

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
    }

    protected override void OnGameStart()
    {
        base.OnGameStart();
        remainingTime = timeLimit;
        
        // Show UI
        if (miniGameCanvas != null)
        {
            miniGameCanvas.gameObject.SetActive(true);
        }

        UpdateUI();
        
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

    private void UpdateUI()
    {
        if (txtTitle != null && timeLimit > 0f)
        {
            txtTitle.text = $"Time: {remainingTime:F1}s";
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!IsActive)
        {
            return;
        }

        // Check time limit
        if (timeLimit > 0f)
        {
            remainingTime -= Time.deltaTime;
            UpdateUI();
            
            if (remainingTime <= 0f)
            {
                FailGame();
            }
        }
    }

    protected override void OnGameEnd(int result)
    {
        base.OnGameEnd(result);

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
