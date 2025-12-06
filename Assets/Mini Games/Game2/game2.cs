using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class WhackAMoleGame : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text scoreText;
    public Button[] holeBtns;
    public Image[] moleImages;
    public Button finishBtn;
    public Image gamePanel;

    [Header("Mole Sprites")]
    public Sprite goodMoleSprite;
    public Sprite badMoleSprite;

    [Header("Colors")]
    public Color emptyHoleColor = new Color(0.6f, 0.4f, 0.2f);

    [Header("Game Settings")]
    public float moleAppearInterval = 0.5f; // ✅ 改为 0.5 秒
    public int molesToSpawnPerTurn = 3; // ✅ 新增：每次出现多少个地鼠
    public int targetScore = 30;
    public AudioClip goodSfx;
    public AudioClip badSfx;
    public AudioClip completeSfx;

    private int currentScore = 0;
    private HashSet<int> activeMoleIds = new HashSet<int>(); // ✅ 改为 HashSet 存储多个地鼠
    private bool gameRunning = false;
    private bool gameFinished = false;
    private Coroutine moleSpawnerCoroutine;
    private AudioSource audioSource;

    private enum MoleType { Good, Bad, None }
    private MoleType[] moleTypes;

    void Start()
    {
        // 初始化 AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // ✅ 验证数组长度
        if (holeBtns == null || holeBtns.Length == 0)
        {
            Debug.LogError("[ERROR] holeBtns 数组为空！请在 Inspector 中配置 8 个按钮！");
            return;
        }

        if (moleImages == null || moleImages.Length == 0)
        {
            Debug.LogError("[ERROR] moleImages 数组为空！请在 Inspector 中配置 8 个 Image！");
            return;
        }

        if (holeBtns.Length != moleImages.Length)
        {
            Debug.LogError($"[ERROR] holeBtns 数量 ({holeBtns.Length}) 和 moleImages 数量 ({moleImages.Length}) 不相等！");
            return;
        }

        Debug.Log($"[SUCCESS] 检测到 {holeBtns.Length} 个地洞和 {moleImages.Length} 个地鼠图片");

        // 初始化地鼠类型数组
        moleTypes = new MoleType[holeBtns.Length];
        for (int i = 0; i < moleTypes.Length; i++)
        {
            moleTypes[i] = MoleType.None;
        }

        // 绑定按钮事件
        for (int i = 0; i < holeBtns.Length; i++)
        {
            int id = i;
            holeBtns[i].onClick.AddListener(() => OnMoleClicked(id));
            
            Image holeImage = holeBtns[i].GetComponent<Image>();
            if (holeImage != null)
            {
                holeImage.color = emptyHoleColor;
            }
            
            moleImages[i].enabled = false;
        }

        if (finishBtn != null)
        {
            finishBtn.onClick.AddListener(FinishGame);
        }

        UpdateScoreDisplay();
        if (finishBtn != null)
        {
            finishBtn.gameObject.SetActive(false);
        }

        Debug.Log("[WhackAMole] 游戏初始化完成");
        StartGameAutomatically();
    }

    void OnDestroy()
    {
        if (holeBtns == null) return;
        
        for (int i = 0; i < holeBtns.Length; i++)
        {
            holeBtns[i].onClick.RemoveAllListeners();
        }
        if (finishBtn != null)
        {
            finishBtn.onClick.RemoveAllListeners();
        }
    }

    void StartGameAutomatically()
    {
        currentScore = 0;
        gameRunning = true;
        gameFinished = false;
        activeMoleIds.Clear();
        UpdateScoreDisplay();
        if (finishBtn != null)
        {
            finishBtn.gameObject.SetActive(false);
        }

        Debug.Log("[WhackAMole] 游戏自动开始");

        if (moleSpawnerCoroutine != null)
        {
            StopCoroutine(moleSpawnerCoroutine);
        }
        moleSpawnerCoroutine = StartCoroutine(MoleSpawner());
    }

    IEnumerator MoleSpawner()
    {
        while (gameRunning && !gameFinished)
        {
            yield return new WaitForSeconds(moleAppearInterval);

            if (!gameRunning || gameFinished) break;

            // ✅ 隐藏所有当前的地鼠
            List<int> molesToHide = new List<int>(activeMoleIds);
            foreach (int moleId in molesToHide)
            {
                HideMole(moleId);
            }

            // ✅ 随机生成多个地鼠
            int molesToSpawn = Mathf.Min(molesToSpawnPerTurn, holeBtns.Length);
            List<int> availableHoles = new List<int>();
            for (int i = 0; i < holeBtns.Length; i++)
            {
                availableHoles.Add(i);
            }

            // 随机打乱顺序
            for (int i = availableHoles.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                int temp = availableHoles[i];
                availableHoles[i] = availableHoles[randomIndex];
                availableHoles[randomIndex] = temp;
            }

            // 选择前 molesToSpawn 个地洞生成地鼠
            for (int i = 0; i < molesToSpawn; i++)
            {
                int holeId = availableHoles[i];
                MoleType type = Random.value < 0.5f ? MoleType.Good : MoleType.Bad;
                ShowMole(holeId, type);
            }

            Debug.Log($"[WhackAMole] 本轮生成了 {molesToSpawn} 个地鼠");
        }
    }

    void ShowMole(int holeId, MoleType type)
    {
        // ✅ 添加边界检查
        if (holeId < 0 || holeId >= moleImages.Length)
        {
            Debug.LogError($"[ERROR] holeId {holeId} 超出范围！moleImages 长度: {moleImages.Length}");
            return;
        }

        activeMoleIds.Add(holeId);
        moleTypes[holeId] = type;

        if (moleImages[holeId] != null)
        {
            if (type == MoleType.Good)
            {
                moleImages[holeId].sprite = goodMoleSprite;
            }
            else if (type == MoleType.Bad)
            {
                moleImages[holeId].sprite = badMoleSprite;
            }

            moleImages[holeId].enabled = true;
        }

        Debug.Log($"[WhackAMole] 地鼠出现在地洞 {holeId}，类型: {type}");
    }

    void HideMole(int holeId)
    {
        if (holeId < 0 || holeId >= holeBtns.Length) return;

        moleTypes[holeId] = MoleType.None;
        if (moleImages[holeId] != null)
        {
            moleImages[holeId].enabled = false;
        }

        activeMoleIds.Remove(holeId);
    }

    void OnMoleClicked(int holeId)
    {
        if (!gameRunning || gameFinished) return;
        if (moleTypes[holeId] == MoleType.None) return;

        int points = 0;
        AudioClip sfx = null;

        if (moleTypes[holeId] == MoleType.Good)
        {
            points = 10;
            sfx = goodSfx;
        }
        else if (moleTypes[holeId] == MoleType.Bad)
        {
            points = -10;
            sfx = badSfx;
        }

        currentScore += points;
        currentScore = Mathf.Max(0, currentScore);

        Debug.Log($"[WhackAMole] 点击地洞 {holeId}，得分: {points}，总分: {currentScore}");

        if (sfx != null && audioSource != null)
        {
            audioSource.PlayOneShot(sfx);
        }

        HideMole(holeId);
        UpdateScoreDisplay();

        if (currentScore >= targetScore)
        {
            CompleteGame();
        }
    }

    void CompleteGame()
    {
        gameRunning = false;
        gameFinished = true;

        Debug.Log($"[WhackAMole] 游戏完成！最终分数: {currentScore}");

        if (moleSpawnerCoroutine != null)
        {
            StopCoroutine(moleSpawnerCoroutine);
        }

        for (int i = 0; i < holeBtns.Length; i++)
        {
            HideMole(i);
        }

        if (completeSfx != null && audioSource != null)
        {
            audioSource.PlayOneShot(completeSfx);
        }

        if (finishBtn != null)
        {
            finishBtn.gameObject.SetActive(true);
        }
    }

    void FinishGame()
    {
        Debug.Log("[WhackAMole] Finish 按钮被点击");
    }

    void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore;
        }
    }
}