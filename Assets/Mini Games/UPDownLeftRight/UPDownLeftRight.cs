using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// A mini game where players must follow a sequence of directional inputs (Up, Down, Left, Right).
/// Players have a time limit to achieve the target score by completing sequences correctly.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class UPDownLeftRight : MiniGame
{
    private static readonly WaitForSeconds _waitForSeconds0_2 = new(0.2f);
    private static readonly WaitForSeconds _waitForSeconds1_5 = new(1.5f);
    
    /// <summary>
    /// Four hint texts displaying the directional arrows.
    /// </summary>
    public Text[] directionTexts;
    
    /// <summary>
    /// UI text displaying the current score.
    /// </summary>
    public Text scoreText;
    
    /// <summary>
    /// End text displaying "Finish" or "Fail" at game conclusion.
    /// </summary>
    public Text endText;
    
    /// <summary>
    /// Timer component for managing countdown.
    /// </summary>
    public MiniGameTimer gameTimer;
    
    [Header("Game Settings")]
    /// <summary>
    /// Time limit for completing the game in seconds.
    /// </summary>
    public float timeLimit = 30f;

    [Header("Audio")]
    /// <summary>
    /// Background music clip.
    /// </summary>
    public AudioClip bgMusic;
    
    /// <summary>
    /// Sound effect for each correct directional input.
    /// </summary>
    public AudioClip correctDirectionSfx;
    
    /// <summary>
    /// Sound effect when a full sequence is completed.
    /// </summary>
    public AudioClip completeSequenceSfx;
    
    /// <summary>
    /// Sound effect for incorrect directional input.
    /// </summary>
    public AudioClip wrongDirectionSfx;

    [Header("Audio Volume")]
    /// <summary>
    /// Background music volume (0.3 = 30%).
    /// </summary>
    public float bgMusicVolume = 0.3f;
    
    /// <summary>
    /// Correct direction sound effect volume.
    /// </summary>
    public float correctVolume = 1.0f;
    
    /// <summary>
    /// Sequence complete sound effect volume.
    /// </summary>
    public float completeVolume = 1.0f;
    
    /// <summary>
    /// Wrong direction sound effect volume.
    /// </summary>
    public float wrongVolume = 1.5f;

    private AudioSource audioSource;
    
    private readonly List<int> sequence = new();
    private int playerIndex = 0;
    private int score = 0;

    private Color normalColor = Color.white;
    private Color correctColor = Color.green;
    private Color wrongColor = Color.red;

    private readonly string[] arrowSymbols = { "⇑", "⇓", "⇐", "⇒" };

    /// <summary>
    /// Whether the game has finished.
    /// </summary>
    private bool gameEnded = false;

    private InputSystem_Actions inputActions;
    private InputAction directionAction;

    /// <summary>
    /// Called when the game starts. Initializes game state and UI.
    /// </summary>
    protected override void OnGameStart()
    {
        gameEnded = false;
        playerIndex = 0;
        score = 0;
        UpdateScore();
        GenerateSequence();

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
        }

        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }

        if (gameTimer != null)
        {
            gameTimer.SetDisplayVisible(true);
            gameTimer.StartTimer(timeLimit);
            gameTimer.OnTimeUp += TriggerFail;
        }
    }

    /// <summary>
    /// Unity Start method. Initializes audio and starts the game.
    /// </summary>
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.volume = bgMusicVolume;

        if (bgMusic != null)
        {
            audioSource.clip = bgMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }

        UpdateScore();
        GenerateSequence();

        SetupInput();
    }

    private void OnEnable()
    {
        SetupInput();
        EnableInput();
    }

    private void OnDisable()
    {
        DisableInput();
    }

    /// <summary>
    /// Update is called once per frame. Handles timer countdown and input detection.
    /// </summary>
    protected override void Update()
    {
        if (gameEnded)
            return;
    }

    private void SetupInput()
    {
        if (inputActions != null)
        {
            return;
        }

        inputActions = new InputSystem_Actions();
        directionAction = inputActions.MiniGame.Arrow;
        directionAction.performed += OnDirectionPerformed;
    }

    private void EnableInput()
    {
        directionAction?.Enable();
        inputActions?.MiniGame.Enable();
    }

    private void DisableInput()
    {
        if (directionAction != null)
        {
            directionAction.performed -= OnDirectionPerformed;
            directionAction.Disable();
        }

        inputActions?.MiniGame.Disable();
        inputActions?.Dispose();
        inputActions = null;
        directionAction = null;
    }

    private void OnDirectionPerformed(InputAction.CallbackContext context)
    {
        if (gameEnded)
        {
            return;
        }

        var dir = context.ReadValue<Vector2>();
        if (dir == Vector2.zero)
        {
            return;
        }

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            CheckInput(dir.x > 0 ? 3 : 2);
        }
        else
        {
            CheckInput(dir.y > 0 ? 0 : 1);
        }
    }

    /// <summary>
    /// Generates a new random sequence of four directions and displays them.
    /// </summary>
    void GenerateSequence()
    {
        if (directionTexts == null || directionTexts.Length < 4)
        {
            return;
        }

        sequence.Clear();

        for (int i = 0; i < 4; i++)
        {
            int r = Random.Range(0, arrowSymbols.Length);
            sequence.Add(r);
        }

        for (int i = 0; i < 4; i++)
        {
            directionTexts[i].text = arrowSymbols[sequence[i]];
            directionTexts[i].color = normalColor;
            directionTexts[i].gameObject.SetActive(true);
        }

        playerIndex = 0;

    }

    /// <summary>
    /// Checks if the player's input matches the expected direction in the sequence.
    /// </summary>
    /// <param name="input">The directional input (0=Up, 1=Down, 2=Left, 3=Right).</param>
    void CheckInput(int input)
    {
        int correctAnswer = sequence[playerIndex];

        if (input == correctAnswer)
        {
            StartCoroutine(FlashColor(directionTexts[playerIndex], correctColor));

            PlaySfx(correctDirectionSfx, correctVolume);

            playerIndex++;

            if (playerIndex == 4)
            {
                score += 40;
                UpdateScore();

                PlaySfx(completeSequenceSfx, completeVolume);

                if (score >= 160)
                {
                    gameEnded = true;
                    TriggerFinish();
                    return;
                }

                GenerateSequence();
            }
        }
        else
        {
            StartCoroutine(FlashColor(directionTexts[playerIndex], wrongColor));

            PlaySfx(wrongDirectionSfx, wrongVolume);

            score -= 10;
            UpdateScore();

            GenerateSequence();
        }
    }

    /// <summary>
    /// Plays a sound effect with the specified volume.
    /// </summary>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="volume">The volume level (default is 1.0).</param>
    void PlaySfx(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// Coroutine that flashes a text element to a specified color briefly.
    /// </summary>
    /// <param name="txt">The text element to flash.</param>
    /// <param name="flashColor">The color to flash.</param>
    IEnumerator FlashColor(Text txt, Color flashColor)
    {
        txt.color = flashColor;
        yield return _waitForSeconds0_2;
        txt.color = normalColor;
    }

    /// <summary>
    /// Updates the score display text.
    /// </summary>
    void UpdateScore()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    /// <summary>
    /// Triggers the finish state for the game.
    /// </summary>
    public void TriggerFinish()
    {
        StartCoroutine(ShowEndThenComplete("Finish"));
    }

    /// <summary>
    /// Triggers the fail state for the game.
    /// </summary>
    public void TriggerFail()
    {
        StartCoroutine(ShowEndThenComplete("Fail"));
    }

    /// <summary>
    /// Coroutine that displays the end message and then completes or fails the game.
    /// </summary>
    /// <param name="message">The message to display ("Finish" or "Fail").</param>
    IEnumerator ShowEndThenComplete(string message)
    {
        gameEnded = true;

        if (gameTimer != null)
        {
            gameTimer.StopTimer();
            gameTimer.SetDisplayVisible(false);
            gameTimer.OnTimeUp -= TriggerFail;
        }

        // Hide direction texts
        if (directionTexts != null)
        {
            foreach (var txt in directionTexts)
            {
                if (txt != null)
                {
                    txt.gameObject.SetActive(false);
                }
            }
        }

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
        }

        if (endText != null)
        {
            endText.text = message;
            endText.gameObject.SetActive(true);
        }

        yield return _waitForSeconds1_5;

        // Hide end text before ending the game
        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }

        if (message == "Finish")
        {
            CompleteGame();
        }
        else
        {
            FailGame();
        }
    }

}