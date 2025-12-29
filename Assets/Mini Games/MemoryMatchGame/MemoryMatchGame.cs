using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Memory Match Mini Game - A memory matching card game
/// </summary>
public class MemoryMatchGame : MiniGame
{
    [Header("Game Settings")]
    [SerializeField]
    private int gridColumns = 4;

    [SerializeField]
    private int gridRows = 3;

    [SerializeField]
    private float matchDelay = 0.5f;

    [SerializeField]
    private float gameTimeLimit = 60f;

    [Header("UI References")]
    [SerializeField]
    private Transform gameBoard;

    [SerializeField]
    private Text timeText;

    [SerializeField]
    private Text matchesText;

    [SerializeField]
    private Text endText;

    [SerializeField]
    private Button restartButton;

    [Header("Prefabs")]
    [SerializeField]
    private GameObject cardPrefab;

    [Header("Image List")]
    [SerializeField]
    private Sprite[] imageList = new Sprite[12];

    [Header("Audio")]
    [SerializeField]
    private AudioClip bgMusic;
    [SerializeField]
    private AudioClip matchSfx;
    [SerializeField]
    private AudioClip mismatchSfx;
    [SerializeField]
    private AudioClip winSfx;
    [SerializeField]
    private AudioClip loseSfx;

    [Header("Audio Volume")]
    [SerializeField]
    private float bgMusicVolume = 0.3f;
    [SerializeField]
    private float matchVolume = 1.0f;
    [SerializeField]
    private float mismatchVolume = 1.0f;
    [SerializeField]
    private float winVolume = 1.0f;
    [SerializeField]
    private float loseVolume = 1.0f;

    private readonly List<MemoryCard> cards = new();
    private Sprite[] images;
    private float remainingTime;
    private int matchedPairs = 0;
    private MemoryCard firstFlippedCard;
    private MemoryCard secondFlippedCard;
    private bool isCheckingMatch = false;
    private bool gameActive = true;
    private AudioSource audioSource;

    private WaitForSeconds waitForMatchDelay;
    private WaitForSeconds waitForSeconds1_5;

    void Start()
    {
        Debug.Log("[MemoryMatchGame] Start() 被調用");

        // 初始化 AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.Log("[MemoryMatchGame] 沒有找到 AudioSource, 正在創建新的");
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            Debug.Log("[MemoryMatchGame] 找到現有的 AudioSource");
        }

        if (audioSource != null)
        {
            audioSource.volume = bgMusicVolume;
        }
    }

    void OnDestroy()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
        }

        // 停止背景音樂
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    protected override void OnGameStart()
    {
        Debug.Log("[MemoryMatchGame] OnGameStart() 被調用");

        // 重新確保 audioSource 存在
        if (audioSource == null)
        {
            Debug.Log("[MemoryMatchGame] audioSource 為 null, 重新獲取");
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                Debug.Log("[MemoryMatchGame] 仍然沒有 AudioSource, 創建新的");
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Initialize WaitForSeconds
        waitForMatchDelay = new WaitForSeconds(matchDelay);
        waitForSeconds1_5 = new WaitForSeconds(1.5f);

        remainingTime = gameTimeLimit;
        matchedPairs = 0;
        gameActive = true;
        isCheckingMatch = false;
        firstFlippedCard = null;
        secondFlippedCard = null;

        // Setup restart button
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartGame);
        }

        // 隱藏 endText，顯示其他 UI
        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }
        if (timeText != null)
        {
            timeText.gameObject.SetActive(true);
        }
        if (matchesText != null)
        {
            matchesText.gameObject.SetActive(true);
        }

        // 開始播放背景音樂
        if (audioSource != null && bgMusic != null)
        {
            Debug.Log($"[MemoryMatchGame] 開始播放背景音樂: {bgMusic.name}");
            audioSource.clip = bgMusic;
            audioSource.loop = true;
            audioSource.volume = bgMusicVolume;
            audioSource.Play();
            Debug.Log($"[MemoryMatchGame] 背景音樂播放狀態: {audioSource.isPlaying}");
        }
        else
        {
            if (audioSource == null)
            {
                Debug.LogError("[MemoryMatchGame] audioSource 為 null!");
            }
            if (bgMusic == null)
            {
                Debug.LogWarning("[MemoryMatchGame] bgMusic 為 null!");
            }
        }

        InitializeGame();
    }

    private void InitializeGame()
    {
        if (cardPrefab == null)
        {
            Debug.LogError("cardPrefab not assigned!");
            FailGame();
            return;
        }

        // Check images
        if (imageList == null || imageList.Length == 0)
        {
            Debug.LogError("Image list is empty!");
            FailGame();
            return;
        }

        ResetGameState();
        ResetUITexts();

        // Clear old cards
        foreach (Transform child in gameBoard)
        {
            Destroy(child.gameObject);
        }

        cards.Clear();

        // Initialize images array
        int totalCards = gridColumns * gridRows;
        images = new Sprite[totalCards];

        // Create card pairs
        int pairsCount = totalCards / 2;
        for (int i = 0; i < pairsCount; i++)
        {
            int imageIndex = i % imageList.Length;
            images[i] = imageList[imageIndex];
            images[pairsCount + i] = imageList[imageIndex];
        }

        // Shuffle order
        for (int i = images.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Sprite temp = images[i];
            images[i] = images[randomIndex];
            images[randomIndex] = temp;
        }

        // Create card objects
        for (int i = 0; i < images.Length; i++)
        {
            GameObject cardObj = Instantiate(cardPrefab, gameBoard);

            if (!cardObj.TryGetComponent<MemoryCard>(out var card))
            {
                Debug.LogError($"Card {i} does not have MemoryCard script!");
                continue;
            }

            card.Initialize(images[i], OnCardFlipped);
            cards.Add(card);
        }
    }

    private void ResetGameState()
    {
        matchedPairs = 0;
        remainingTime = gameTimeLimit;
        gameActive = true;
        isCheckingMatch = false;
        firstFlippedCard = null;
        secondFlippedCard = null;
    }

    private void ResetUITexts()
    {
        if (timeText != null)
        {
            timeText.text = $"Time: {Mathf.RoundToInt(remainingTime)}s";
        }

        if (matchesText != null)
        {
            matchesText.text = $"Matches: {matchedPairs}/{gridColumns * gridRows / 2}";
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!IsActive || !gameActive)
            return;

        // Update timer
        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0)
        {
            remainingTime = 0;
            gameActive = false;
            EndGameFail();
            return;
        }

        // Update UI
        if (timeText != null)
        {
            timeText.text = $"Time: {Mathf.RoundToInt(remainingTime)}s";
        }

        // Exit with ESC key - using new Input System
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ExitGame();
        }
    }

    private void OnCardFlipped(MemoryCard card)
    {
        if (!gameActive || isCheckingMatch)
            return;

        if (card.IsFlipped || card.IsMatched)
            return;

        // Flip card
        card.Flip();

        if (firstFlippedCard == null)
        {
            // First card
            firstFlippedCard = card;
        }
        else if (secondFlippedCard == null)
        {
            // Second card
            secondFlippedCard = card;
            isCheckingMatch = true;

            // Check if matched
            StartCoroutine(CheckMatch());
        }
    }

    private IEnumerator CheckMatch()
    {
        yield return waitForMatchDelay;

        if (firstFlippedCard.GetImage() == secondFlippedCard.GetImage())
        {
            // Match successful
            firstFlippedCard.SetMatched();
            secondFlippedCard.SetMatched();
            matchedPairs++;

            // 播放配對成功音效
            PlaySfx(matchSfx, matchVolume);

            // Update UI
            if (matchesText != null)
            {
                matchesText.text = $"Matches: {matchedPairs}/{gridColumns * gridRows / 2}";
            }

            // Check if all matched
            if (matchedPairs == gridColumns * gridRows / 2)
            {
                gameActive = false;
                EndGameWin();
            }
        }
        else
        {
            // Match failed, flip back
            // 播放配對失敗音效
            PlaySfx(mismatchSfx, mismatchVolume);

            firstFlippedCard.UnFlip();
            secondFlippedCard.UnFlip();
        }

        // Reset state
        firstFlippedCard = null;
        secondFlippedCard = null;
        isCheckingMatch = false;
    }

    private void PlaySfx(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && audioSource != null)
        {
            Debug.Log($"[MemoryMatchGame] 播放音效: {clip.name}");
            audioSource.PlayOneShot(clip, volume);
        }
        else
        {
            if (clip == null)
            {
                Debug.LogWarning("[MemoryMatchGame] AudioClip 為 null!");
            }
            if (audioSource == null)
            {
                Debug.LogError("[MemoryMatchGame] audioSource 為 null!");
            }
        }
    }

    private void EndGameWin()
    {
        gameActive = false;
        StartCoroutine(ShowResultThenComplete(true));
    }

    private void EndGameFail()
    {
        gameActive = false;
        StartCoroutine(ShowResultThenComplete(false));
    }

    private IEnumerator ShowResultThenComplete(bool success)
    {
        // 停止背景音樂
        if (audioSource != null && audioSource.isPlaying)
        {
            Debug.Log("[MemoryMatchGame] 停止背景音樂");
            audioSource.Stop();
        }

        // 播放贏或輸的音效
        if (success)
        {
            PlaySfx(winSfx, winVolume);
        }
        else
        {
            PlaySfx(loseSfx, loseVolume);
        }

        // 隱藏遊戲 UI
        if (timeText != null)
        {
            timeText.gameObject.SetActive(false);
        }
        if (matchesText != null)
        {
            matchesText.gameObject.SetActive(false);
        }

        // 顯示結束文字
        if (endText != null)
        {
            endText.gameObject.SetActive(true);
            if (success)
            {
                endText.text = $"Success!\nMatches: {matchedPairs}/{gridColumns * gridRows / 2}";
                endText.color = new Color(0, 0.7f, 0, 1);
            }
            else
            {
                endText.text = $"Time's Up!\nMatches: {matchedPairs}/{gridColumns * gridRows / 2}";
                endText.color = Color.red;
            }
        }

        yield return waitForSeconds1_5;

        if (success)
        {
            CompleteGame();
        }
        else
        {
            FailGame();
        }
    }

    private void RestartGame()
    {
        InitializeGame();
    }
}