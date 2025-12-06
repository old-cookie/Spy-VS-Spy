using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class UPDownLeftRight : MiniGame
{
    private static readonly WaitForSeconds _waitForSeconds0_2 = new(0.2f);
    private static readonly WaitForSeconds _waitForSeconds1_5 = new(1.5f);
    public Text[] directionTexts;        // Four hint texts
    public Text scoreText;               // UI score text
    public Text endText;                 // End text (shows Finish/Fail)
    public Text timeText;                // Time display text
    
    [Header("Game Settings")]
    public float timeLimit = 30f;        // Time limit in seconds

    [Header("Audio")]
    public AudioClip bgMusic;              // Background music
    public AudioClip correctDirectionSfx;  // Per-direction hit sfx
    public AudioClip completeSequenceSfx;  // Sequence completed sfx
    public AudioClip wrongDirectionSfx;    // Wrong direction sfx

    [Header("Audio Volume")]
    public float bgMusicVolume = 0.3f;     // Background music volume (0.3 = 30%)
    public float correctVolume = 1.0f;     // Correct direction volume
    public float completeVolume = 1.0f;    // Sequence complete volume
    public float wrongVolume = 1.5f;       // Wrong direction volume

    private AudioSource audioSource;
    
    private List<int> sequence = new();
    private int playerIndex = 0;
    private int score = 0;

    private Color normalColor = Color.white;
    private Color correctColor = Color.green;
    private Color wrongColor = Color.red;

    private readonly string[] arrowSymbols = { "⇑", "⇓", "⇐", "⇒" };

    private bool gameEnded = false;  // Game finished flag
    private float remainingTime;     // Remaining time in seconds

    protected override void OnGameStart()
    {
        gameEnded = false;
        playerIndex = 0;
        score = 0;
        remainingTime = timeLimit;
        UpdateScore();
        UpdateTime();
        GenerateSequence();

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
        }

        if (timeText != null)
        {
            timeText.gameObject.SetActive(true);
        }

        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }
    }

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

        if (timeText != null)
        {
            timeText.gameObject.SetActive(true);
        }

        remainingTime = timeLimit;
        UpdateScore();
        UpdateTime();
        GenerateSequence();
    }



    protected override void Update()
    {
        if (gameEnded)
            return;

        // Update timer
        remainingTime -= Time.deltaTime;
        UpdateTime();

        if (remainingTime <= 0f)
        {
            TriggerFail();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))        CheckInput(0);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) CheckInput(1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) CheckInput(2);
        else if (Input.GetKeyDown(KeyCode.RightArrow))CheckInput(3);
    }

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

    void PlaySfx(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    IEnumerator FlashColor(Text txt, Color flashColor)
    {
        txt.color = flashColor;
        yield return _waitForSeconds0_2;
        txt.color = normalColor;
    }

    void UpdateScore()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    void UpdateTime()
    {
        if (timeText != null)
        {
            timeText.text = "Time: " + Mathf.CeilToInt(remainingTime) + "s";
        }
    }

    public void TriggerFinish()
    {
        StartCoroutine(ShowEndThenComplete("Finish"));
    }

    public void TriggerFail()
    {
        StartCoroutine(ShowEndThenComplete("Fail"));
    }

    IEnumerator ShowEndThenComplete(string message)
    {
        gameEnded = true;

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

        if (timeText != null)
        {
            timeText.gameObject.SetActive(false);
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