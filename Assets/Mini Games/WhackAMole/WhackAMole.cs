using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls the whack-a-mole mini-game lifecycle, spawning moles and tracking score/time.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WhackAMole : MiniGame
{
    private static readonly WaitForSeconds WaitResult = new(1.5f);

    [Header("UI Elements")]
    /// <summary>Displays the current score.</summary>
    public Text scoreText;
    /// <summary>Displays the remaining time.</summary>
    public Text timerText;
    /// <summary>Displays the final result.</summary>
    public Text resultText;
    /// <summary>Button prefab used as a mole hole.</summary>
    public Button holePrefab;
    /// <summary>Optional parent for spawned holes.</summary>
    public RectTransform holeParent;
    /// <summary>Panel used as the spawn area and visibility toggle.</summary>
    public Image gamePanel;

    [Header("Mole Sprites")]
    /// <summary>Sprite used for good moles.</summary>
    public Sprite goodMoleSprite;
    /// <summary>Sprite used for bad moles.</summary>
    public Sprite badMoleSprite;

    [Header("Game Settings")]
    /// <summary>Seconds between spawn cycles.</summary>
    public float moleAppearInterval = 0.5f;
    /// <summary>How many moles appear each spawn cycle.</summary>
    public int molesToSpawnPerTurn = 3;
    /// <summary>Minimum spacing between spawned holes.</summary>
    public float minHoleSpacing = 120f;
    /// <summary>Padding inside the spawn area to avoid edges.</summary>
    public Vector2 spawnPadding = new Vector2(40f, 40f);
    /// <summary>Score required to finish with success.</summary>
    public int targetScore = 30;
    /// <summary>Time limit for the round in seconds.</summary>
    public float timeLimit = 30f;

    [Header("Audio")]
    /// <summary>Background music clip.</summary>
    public AudioClip bgMusic;
    /// <summary>Sound played when hitting a good mole.</summary>
    public AudioClip goodSfx;
    /// <summary>Sound played when hitting a bad mole.</summary>
    public AudioClip badSfx;
    /// <summary>Sound played when the round completes.</summary>
    public AudioClip completeSfx;

    [Header("Audio Volume")]
    /// <summary>Background music volume (0.3 = 30%).</summary>
    public float bgMusicVolume = 0.3f;
    /// <summary>Good mole sound effect volume.</summary>
    public float goodVolume = 1.0f;
    /// <summary>Bad mole sound effect volume.</summary>
    public float badVolume = 1.0f;
    /// <summary>Complete sound effect volume.</summary>
    public float completeVolume = 1.0f;

    private int currentScore = 0;
    private float remainingTime = 0f;
    private bool gameRunning = false;
    private bool gameFinished = false;
    private Coroutine moleSpawnerCoroutine;
    private AudioSource audioSource;

    private class ActiveHole
    {
        public int id;
        public Button button;
        public MoleType type;
        public RectTransform rect;
    }

    private readonly Dictionary<int, ActiveHole> activeHoles = new();
    private int nextHoleId = 0;

    private enum MoleType { Good, Bad }

    /// <summary>
    /// Initializes audio and UI state when the component is created.
    /// </summary>
    void Start()
    {
        Debug.Log("[WhackAMole] Start() 被調用");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.Log("[WhackAMole] 沒有找到 AudioSource, 正在創建新的");
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            Debug.Log("[WhackAMole] 找到現有的 AudioSource");
        }

        Debug.Log($"[WhackAMole] bgMusic: {(bgMusic != null ? bgMusic.name : "null")}");
        Debug.Log($"[WhackAMole] bgMusicVolume: {bgMusicVolume}");

        audioSource.volume = bgMusicVolume;

        if (holePrefab == null)
        {
            Debug.LogWarning("[WhackAMole] holePrefab 沒有設定！");
            return;
        }

        UpdateScoreDisplay();
        UpdateTimerDisplay();
        ShowResult("");
    }

    /// <summary>
    /// Play a sound effect with the specified volume.
    /// </summary>
    void PlaySfx(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// Cleans up spawned holes when the object is destroyed.
    /// </summary>
    void OnDestroy()
    {
        ClearActiveHoles();
    }

    /// <summary>
    /// Resets state and starts the spawn loop when the game begins.
    /// </summary>
    protected override void OnGameStart()
    {
        Debug.Log("[WhackAMole] OnGameStart() 被調用");

        // 重新確保 audioSource 存在
        if (audioSource == null)
        {
            Debug.Log("[WhackAMole] audioSource 為 null, 重新獲取");
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                Debug.Log("[WhackAMole] 仍然沒有 AudioSource, 創建新的");
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        currentScore = 0;
        remainingTime = timeLimit;
        gameRunning = true;
        gameFinished = false;
        ClearActiveHoles();
        UpdateScoreDisplay();
        UpdateTimerDisplay();
        ShowResult("");

        // 開始播放背景音樂
        if (audioSource != null)
        {
            Debug.Log("[WhackAMole] audioSource 不為 null");

            if (bgMusic != null)
            {
                Debug.Log($"[WhackAMole] 開始播放背景音樂: {bgMusic.name}");
                audioSource.clip = bgMusic;
                audioSource.loop = true;
                audioSource.volume = bgMusicVolume;
                Debug.Log($"[WhackAMole] 設定音量: {audioSource.volume}");
                audioSource.Play();
                Debug.Log($"[WhackAMole] 播放狀態: {audioSource.isPlaying}");
            }
            else
            {
                Debug.LogError("[WhackAMole] bgMusic 為 null!");
            }
        }
        else
        {
            Debug.LogError("[WhackAMole] audioSource 仍為 null!");
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
        }

        if (gamePanel != null)
        {
            gamePanel.gameObject.SetActive(true);
        }

        if (moleSpawnerCoroutine != null)
        {
            StopCoroutine(moleSpawnerCoroutine);
        }
        moleSpawnerCoroutine = StartCoroutine(MoleSpawner());
    }

    /// <summary>
    /// Coroutine that periodically spawns moles while the game is running.
    /// </summary>
    IEnumerator MoleSpawner()
    {
        while (gameRunning && !gameFinished)
        {
            yield return new WaitForSeconds(moleAppearInterval);

            if (!gameRunning || gameFinished) break;

            SpawnHolesForTurn();
        }
    }

    /// <summary>
    /// Updates the countdown timer and completes the game when time elapses.
    /// </summary>
    protected override void Update()
    {
        if (!gameRunning || gameFinished || !IsActive)
        {
            return;
        }

        remainingTime -= Time.deltaTime;
        if (remainingTime < 0f)
        {
            remainingTime = 0f;
        }
        UpdateTimerDisplay();

        if (remainingTime <= 0f)
        {
            CompleteGame(false);
        }
    }

    /// <summary>
    /// Spawns a set of holes for the current turn within the allowed area.
    /// </summary>
    void SpawnHolesForTurn()
    {
        ClearActiveHoles();

        RectTransform parent = holeParent != null ? holeParent : (gamePanel != null ? gamePanel.rectTransform : transform as RectTransform);
        if (parent == null)
        {
            return;
        }

        Rect rect = parent.rect;
        RectTransform prefabRect = holePrefab.GetComponent<RectTransform>();
        float sizeX = prefabRect != null ? prefabRect.sizeDelta.x : minHoleSpacing;
        float sizeY = prefabRect != null ? prefabRect.sizeDelta.y : minHoleSpacing;
        float halfX = sizeX * 0.5f;
        float halfY = sizeY * 0.5f;
        float spacing = Mathf.Max(minHoleSpacing, Mathf.Max(sizeX, sizeY));

        float minX = rect.xMin + spawnPadding.x + halfX;
        float maxX = rect.xMax - spawnPadding.x - halfX;
        float minY = rect.yMin + spawnPadding.y + halfY;
        float maxY = rect.yMax - spawnPadding.y - halfY;

        if (minX > maxX)
        {
            float center = (rect.xMin + rect.xMax) * 0.5f;
            minX = maxX = center;
        }
        if (minY > maxY)
        {
            float center = (rect.yMin + rect.yMax) * 0.5f;
            minY = maxY = center;
        }

        List<Vector2> placedPositions = new();
        int spawnCount = Mathf.Max(1, molesToSpawnPerTurn);

        for (int i = 0; i < spawnCount; i++)
        {
            bool placed = false;
            Vector2 pos = Vector2.zero;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                float x = Random.Range(minX, maxX);
                float y = Random.Range(minY, maxY);
                pos = new Vector2(x, y);

                bool overlaps = false;
                for (int p = 0; p < placedPositions.Count; p++)
                {
                    if (Vector2.Distance(pos, placedPositions[p]) < spacing)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                continue;
            }

            MoleType type = Random.value < 0.5f ? MoleType.Good : MoleType.Bad;
            CreateHole(type, pos, parent);
            placedPositions.Add(pos);
        }
    }

    /// <summary>
    /// Instantiates a hole button and registers it as active.
    /// </summary>
    void CreateHole(MoleType type, Vector2 anchoredPos, RectTransform parent)
    {
        Button btn = Instantiate(holePrefab, parent);
        btn.interactable = true;
        int holeId = nextHoleId++;
        btn.onClick.AddListener(() => OnMoleClicked(holeId));

        Image img = btn.image;
        if (img != null)
        {
            img.raycastTarget = true;
            img.sprite = type == MoleType.Good ? goodMoleSprite : badMoleSprite;
            img.color = Color.white;
        }

        if (btn.TryGetComponent<RectTransform>(out var rect))
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.localScale = Vector3.one;
        }

        activeHoles[holeId] = new ActiveHole
        {
            id = holeId,
            button = btn,
            type = type,
            rect = rect
        };
    }

    /// <summary>
    /// Destroys all active hole instances and clears the registry.
    /// </summary>
    void ClearActiveHoles()
    {
        foreach (var kv in activeHoles)
        {
            if (kv.Value != null && kv.Value.button != null)
            {
                Destroy(kv.Value.button.gameObject);
            }
        }
        activeHoles.Clear();
    }

    /// <summary>
    /// Handles player clicks on a mole, updating score and completion state.
    /// </summary>
    void OnMoleClicked(int holeId)
    {
        if (!gameRunning || gameFinished)
        {
            return;
        }
        if (!activeHoles.TryGetValue(holeId, out var hole))
        {
            return;
        }

        int points = 0;
        AudioClip sfx = null;
        float volume = 1.0f;

        if (hole.type == MoleType.Good)
        {
            points = 10;
            sfx = goodSfx;
            volume = goodVolume;
        }
        else if (hole.type == MoleType.Bad)
        {
            points = -10;
            sfx = badSfx;
            volume = badVolume;
        }

        currentScore += points;
        currentScore = Mathf.Max(0, currentScore);

        PlaySfx(sfx, volume);

        if (hole.button != null)
        {
            Destroy(hole.button.gameObject);
        }
        activeHoles.Remove(holeId);
        UpdateScoreDisplay();

        if (currentScore >= targetScore)
        {
            CompleteGame(true);
        }
    }

    /// <summary>
    /// Ends the round, stops spawning, and shows the result.
    /// </summary>
    void CompleteGame(bool success)
    {
        Debug.Log("[WhackAMole] CompleteGame() 被調用");

        gameRunning = false;
        gameFinished = true;

        if (moleSpawnerCoroutine != null)
        {
            StopCoroutine(moleSpawnerCoroutine);
        }

        // 停止背景音樂並播放完成音效
        if (audioSource != null && audioSource.isPlaying)
        {
            Debug.Log("[WhackAMole] 停止背景音樂");
            audioSource.Stop();
        }

        PlaySfx(completeSfx, completeVolume);
        ClearActiveHoles();

        if (gamePanel != null)
        {
            gamePanel.gameObject.SetActive(false);
        }

        ShowResult(success ? "You Win!!!" : "Failed");

        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }

        StartCoroutine(ResultAndEnd(success));
    }

    /// <summary>
    /// Refreshes the score UI with the current value.
    /// </summary>
    void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore;
        }
    }

    /// <summary>
    /// Refreshes the timer UI with the remaining time.
    /// </summary>
    void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            timerText.text = "Time: " + Mathf.CeilToInt(remainingTime) + "s";
        }
    }

    /// <summary>
    /// Shows or hides the result text based on the provided message.
    /// </summary>
    void ShowResult(string message)
    {
        if (resultText != null)
        {
            resultText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            resultText.text = message;
        }
    }

    /// <summary>
    /// Waits briefly, then notifies the base class of the outcome.
    /// </summary>
    IEnumerator ResultAndEnd(bool success)
    {
        yield return WaitResult;

        int result = success ? 1 : -1;
        EndGame(result);
    }
}