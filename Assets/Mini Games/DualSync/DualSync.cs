using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Mini-game where players synchronize two power bars by holding
/// the left and right arrow keys to keep both bars within their
/// target ranges. Score is gained by maintaining the target state
/// for required time; the game ends when reaching the score goal
/// or when time runs out.
/// </summary>
public class DualSync : MiniGame
{
    /// <summary>
    /// The fill Image for the first power bar.
    /// </summary>
    public Image bar1Fill;
    /// <summary>
    /// The fill Image for the second power bar.
    /// </summary>
    public Image bar2Fill;
    /// <summary>
    /// Target line UI for the first bar.
    /// </summary>
    public Image targetLine1;
    /// <summary>
    /// Target line UI for the second bar.
    /// </summary>
    public Image targetLine2;
    /// <summary>
    /// Status text displaying progress, target time, and remaining time.
    /// </summary>
    public Text statusText;
    /// <summary>
    /// Text displaying the current score.
    /// </summary>
    public Text scoreText;
    /// <summary>
    /// Text shown when the game ends.
    /// </summary>
    public Text endText;
    /// <summary>
    /// Mini-game timer controlling the overall time limit and timeout event.
    /// </summary>
    public MiniGameTimer gameTimer;

    /// <summary>
    /// Fill speed per second while the key is held.
    /// </summary>
    public float fillSpeed = 0.01f;
    /// <summary>
    /// Decay speed per second when no input is pressed (used with a factor).
    /// </summary>
    public float decaySpeed = 0.5f;
    /// <summary>
    /// Minimum target fill value (0–1).
    /// </summary>
    public float minTargetFill = 0.5f;
    /// <summary>
    /// Maximum target fill value (0–1).
    /// </summary>
    public float maxTargetFill = 0.8f;
    /// <summary>
    /// Seconds required within the target range to earn one score.
    /// </summary>
    public float targetTimeRequired = 1f;
    /// <summary>
    /// Score awarded each time the required target time is reached.
    /// </summary>
    public float scorePerTarget = 1f;
    /// <summary>
    /// Overall game time limit in seconds.
    /// </summary>
    public float gameTimeLimit = 30f;

    private float bar1FillValue = 0f;
    private float bar2FillValue = 0f;
    private float targetFill1;
    private float targetFill2;
    private bool gameEnded = false;
    private float timeInTarget = 0f;
    private int score = 0;

    private InputSystem_Actions inputActions;

    /// <summary>
    /// Game start event: reset state, randomize targets, start timer, and refresh UI.
    /// </summary>
    protected override void OnGameStart()
    {
        // Reset game state
        gameEnded = false;
        bar1FillValue = 0f;
        bar2FillValue = 0f;
        timeInTarget = 0f;
        score = 0;

        // Randomize target fill values
        targetFill1 = Random.Range(minTargetFill, maxTargetFill);
        targetFill2 = Random.Range(minTargetFill, maxTargetFill);

        // Start the timer
        if (gameTimer != null)
        {
            gameTimer.OnTimeUp += OnTimerEnded;
            gameTimer.StartTimer(gameTimeLimit);
        }

        // Initialize UI
        UpdateUI();
        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Unity Start: initialize input, randomize targets, and refresh UI.
    /// </summary>
    void Start()
    {
        SetupInput();

        // Randomize target fill values
        targetFill1 = Random.Range(minTargetFill, maxTargetFill);
        targetFill2 = Random.Range(minTargetFill, maxTargetFill);

        // Initialize UI
        UpdateUI();
        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Enable input when the component is enabled.
    /// </summary>
    private void OnEnable()
    {
        SetupInput();
        EnableInput();
    }

    /// <summary>
    /// Disable input and release resources when the component is disabled.
    /// </summary>
    private void OnDisable()
    {
        DisableInput();
    }

    /// <summary>
    /// Create the input actions instance if it does not exist.
    /// </summary>
    private void SetupInput()
    {
        if (inputActions != null)
        {
            return;
        }

        inputActions = new InputSystem_Actions();
    }

    /// <summary>
    /// Enable the mini-game input action map.
    /// </summary>
    private void EnableInput()
    {
        inputActions?.MiniGame.Enable();
    }

    /// <summary>
    /// Disable the mini-game input action map and dispose resources.
    /// </summary>
    private void DisableInput()
    {
        inputActions?.MiniGame.Disable();
        inputActions?.Dispose();
        inputActions = null;
    }

    /// <summary>
    /// Timer timeout callback: declare failure and end the flow.
    /// </summary>
    private void OnTimerEnded()
    {
        FailGame();
    }

    /// <summary>
    /// Handle long presses of left/right arrow keys to increase bar fill values.
    /// </summary>
    private void HandleLongPressInput()
    {
        if (gameEnded)
        {
            return;
        }

        // Directly check keyboard keys
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                bar1FillValue += fillSpeed * Time.deltaTime;
                bar1FillValue = Mathf.Clamp01(bar1FillValue);
            }

            if (Keyboard.current.rightArrowKey.isPressed)
            {
                bar2FillValue += fillSpeed * Time.deltaTime;
                bar2FillValue = Mathf.Clamp01(bar2FillValue);
            }
        }
    }

    /// <summary>
    /// Per-frame update: read input, apply decay, check target-range time for scoring, then update UI.
    /// </summary>
    protected override void Update()
    {
        if (gameEnded) return;

        // Handle long-press input (per frame)
        HandleLongPressInput();

        // Slow decay when no keys are pressed
        bar1FillValue -= decaySpeed * Time.deltaTime * 0.1f;
        bar2FillValue -= decaySpeed * Time.deltaTime * 0.1f;

        bar1FillValue = Mathf.Clamp01(bar1FillValue);
        bar2FillValue = Mathf.Clamp01(bar2FillValue);

        // Check whether both bars are within target ranges (lenient: ±0.15)
        bool bar1InTarget = bar1FillValue >= targetFill1 - 0.05f && bar1FillValue <= targetFill1 + 0.15f;
        bool bar2InTarget = bar2FillValue >= targetFill2 - 0.05f && bar2FillValue <= targetFill2 + 0.15f;

        if (bar1InTarget && bar2InTarget)
        {
            // Accumulate time while within target ranges
            timeInTarget += Time.deltaTime;

            // Earn 1 point each targetTimeRequired seconds
            if (timeInTarget >= targetTimeRequired)
            {
                score += 1;  // Add 1 point per trigger
                timeInTarget -= targetTimeRequired;  // Subtract counted time, allow continuous counting

                // If reaching 5 points, complete the game
                if (score >= 5)
                {
                    CompleteGame();
                    return;
                }

                // After scoring, randomize targets and reset bars
                targetFill1 = Random.Range(minTargetFill, maxTargetFill);
                targetFill2 = Random.Range(minTargetFill, maxTargetFill);
                bar1FillValue = 0f;
                bar2FillValue = 0f;
            }
        }
        else
        {            
            // Reset time in target if either bar is out of range
            timeInTarget = 0f;
        }

        UpdateUI();
    }

    /// <summary>
    /// Update all UI: bar fills, target line positions, status text, and score.
    /// </summary>
    void UpdateUI()
    {
        // Update bar fills
        if (bar1Fill != null)
        {
            bar1Fill.fillAmount = bar1FillValue;
        }

        if (bar2Fill != null)
        {
            bar2Fill.fillAmount = bar2FillValue;
        }

        // Update target line positions (Bar container anchor is Center)
        if (targetLine1 != null)
        {
            float yPos = (targetFill1 - 0.5f) * 800f;
            targetLine1.rectTransform.anchoredPosition = new Vector2(0, yPos);
        }

        if (targetLine2 != null)
        {
            float yPos = (targetFill2 - 0.5f) * 800f;
            targetLine2.rectTransform.anchoredPosition = new Vector2(0, yPos);
        }

        // Update status text - show bar progress
        if (statusText != null)
        {
            float remainingTime = gameTimer != null ? gameTimer.RemainingTime : gameTimeLimit;
            statusText.text = $"Bar1: {bar1FillValue * 100:F0}%  |  Bar2: {bar2FillValue * 100:F0}%\nTime In Target: {timeInTarget:F1}s / {targetTimeRequired}s\nGame Time: {remainingTime:F1}s";
        }

        // Update score text
        if (scoreText != null)
        {
            scoreText.text = $"Score: {score}";
        }
    }
}