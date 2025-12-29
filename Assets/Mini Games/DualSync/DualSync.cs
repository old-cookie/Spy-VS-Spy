using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Mini-game where players synchronize two power bars by holding
/// the left and right arrow keys to keep both bars within their
/// target ranges. Score is gained by maintaining the target state
/// for required time; the game ends when reaching the score goal
/// or when time runs out.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DualSync : MiniGame
{
    private static readonly WaitForSeconds _waitForSeconds1_5 = new(1.5f);

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

    [Header("Audio")]
    /// <summary>
    /// Background music clip to play during gameplay.
    /// </summary>
    public AudioClip bgMusic;
    /// <summary>
    /// Sound effect when score is earned.
    /// </summary>
    public AudioClip scoreAchievedSfx;

    [Header("Audio Volume")]
    /// <summary>
    /// Background music volume (0.3 = 30%).
    /// </summary>
    public float bgMusicVolume = 0.3f;
    /// <summary>
    /// Score achieved sound effect volume.
    /// </summary>
    public float scoreVolume = 1.0f;

    [Header("Game Settings")]
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
    /// <summary>
    /// Required score to win the game.
    /// </summary>
    public int scoreGoal = 5;

    private float bar1FillValue = 0f;
    private float bar2FillValue = 0f;
    private float targetFill1;
    private float targetFill2;
    private bool gameEnded = false;
    private float timeInTarget = 0f;
    private int score = 0;
    private bool gameActive = true;

    private AudioSource audioSource;
    private InputSystem_Actions inputActions;

    /// <summary>
    /// Game start event: reset state, randomize targets, start timer, and refresh UI.
    /// </summary>
    protected override void OnGameStart()
    {
        // Reset game state
        gameEnded = false;
        gameActive = true;
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
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
        }

        // Play background music
        PlayBackgroundMusic();
    }

    /// <summary>
    /// Unity Start: initialize audio, input, randomize targets, and refresh UI.
    /// </summary>
    void Start()
    {
        // Initialize AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.volume = bgMusicVolume;

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

        // Play background music
        PlayBackgroundMusic();
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
        StopBackgroundMusic();
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
    /// Play the background music on loop.
    /// </summary>
    private void PlayBackgroundMusic()
    {
        if (audioSource != null && bgMusic != null)
        {
            audioSource.clip = bgMusic;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    /// <summary>
    /// Stop the background music.
    /// </summary>
    private void StopBackgroundMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Play a sound effect with the specified volume.
    /// </summary>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="volume">The volume level (default is 1.0).</param>
    private void PlaySfx(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// Timer timeout callback: end the game.
    /// </summary>
    private void OnTimerEnded()
    {
        gameActive = false;
        EndGameFail();
    }

    /// <summary>
    /// Handle long presses of left/right arrow keys to increase bar fill values.
    /// </summary>
    private void HandleLongPressInput()
    {
        if (gameEnded || !gameActive)
        {
            return;
        }

        // Read arrow input from InputSystem
        if (inputActions != null)
        {
            Vector2 arrowInput = inputActions.MiniGame.Arrow.ReadValue<Vector2>();

            // Left arrow (negative X)
            if (arrowInput.x < 0)
            {
                bar1FillValue += fillSpeed * Time.deltaTime;
                bar1FillValue = Mathf.Clamp01(bar1FillValue);
            }

            // Right arrow (positive X)
            if (arrowInput.x > 0)
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
        if (gameEnded || !gameActive) return;

        // Handle long-press input (per frame)
        HandleLongPressInput();

        // Slow decay when no keys are pressed
        bar1FillValue -= decaySpeed * Time.deltaTime * 0.1f;
        bar2FillValue -= decaySpeed * Time.deltaTime * 0.1f;

        bar1FillValue = Mathf.Clamp01(bar1FillValue);
        bar2FillValue = Mathf.Clamp01(bar2FillValue);

        // Check whether both bars are within target ranges
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

                // Play score achieved sound effect
                PlaySfx(scoreAchievedSfx, scoreVolume);

                // Check if score goal is reached
                if (score >= scoreGoal)
                {
                    gameActive = false;
                    EndGameWin();
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
            scoreText.text = $"Score: {score} / {scoreGoal}";
        }
    }

    /// <summary>
    /// End game with win result.
    /// </summary>
    private void EndGameWin()
    {
        gameEnded = true;
        StartCoroutine(ShowResultThenComplete(true));
    }

    /// <summary>
    /// End game with fail result.
    /// </summary>
    private void EndGameFail()
    {
        gameEnded = true;
        StartCoroutine(ShowResultThenComplete(false));
    }

    /// <summary>
    /// Show result text then complete or fail the game.
    /// </summary>
    private IEnumerator ShowResultThenComplete(bool success)
    {
        // Hide game UI
        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
        }

        // Show end text
        if (endText != null)
        {
            endText.gameObject.SetActive(true);
            if (success)
            {
                endText.text = "YOU WIN!";
                endText.color = Color.green;
            }
            else
            {
                endText.text = "YOU LOSE!";
                endText.color = Color.red;
            }
        }

        yield return _waitForSeconds1_5;

        if (success)
        {
            CompleteGame();
        }
        else
        {
            FailGame();
        }
    }
}