using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Memory Match Mini Game - 配對記憶遊戲
/// </summary>
public class MemoryMatchGame : MiniGame
{
    [Header("遊戲設定")]
    [SerializeField]
    private int gridColumns = 4;

    [SerializeField]
    private int gridRows = 3;

    [SerializeField]
    private float flipDuration = 0.3f;

    [SerializeField]
    private float matchDelay = 0.5f;

    [SerializeField]
    private float gameTimeLimit = 60f;

    [Header("UI 參考")]
    [SerializeField]
    private Transform gameBoard;

    [SerializeField]
    private Text timeText;

    [SerializeField]
    private Text matchesText;

    [SerializeField]
    private Button restartButton;

    [Header("預製體")]
    [SerializeField]
    private GameObject cardPrefab;

    [Header("圖片列表")]
    [SerializeField]
    private Sprite[] imageList = new Sprite[12];

    private List<MemoryCard> cards = new List<MemoryCard>();
    private Sprite[] images;
    private float remainingTime;
    private int matchedPairs = 0;
    private MemoryCard firstFlippedCard;
    private MemoryCard secondFlippedCard;
    private bool isCheckingMatch = false;
    private bool gameActive = true;

    private WaitForSeconds waitForFlipDuration;
    private WaitForSeconds waitForMatchDelay;
    private WaitForSeconds waitForSeconds1_5;

    protected override void OnGameStart()
    {
        Debug.Log("=== Memory Match Game Started ===");

        // 初始化 WaitForSeconds
        waitForFlipDuration = new WaitForSeconds(flipDuration);
        waitForMatchDelay = new WaitForSeconds(matchDelay);
        waitForSeconds1_5 = new WaitForSeconds(1.5f);

        remainingTime = gameTimeLimit;
        matchedPairs = 0;
        gameActive = true;
        isCheckingMatch = false;
        firstFlippedCard = null;
        secondFlippedCard = null;

        // 設定重新開始按鈕
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartGame);
        }

        InitializeGame();
    }

    private void InitializeGame()
    {
        Debug.Log("=== 遊戲初始化開始 ===");

        if (cardPrefab == null)
        {
            Debug.LogError("cardPrefab 未指定！");
            FailGame();
            return;
        }

        // 檢查圖片
        if (imageList == null || imageList.Length == 0)
        {
            Debug.LogError("圖片列表為空！");
            FailGame();
            return;
        }

        ResetGameState();
        ResetUITexts();

        // 清空舊卡片
        foreach (Transform child in gameBoard)
        {
            Destroy(child.gameObject);
        }

        cards.Clear();

        // 初始化 images 陣列
        int totalCards = gridColumns * gridRows;
        images = new Sprite[totalCards];

        // 建立卡片對
        int pairsCount = totalCards / 2;
        for (int i = 0; i < pairsCount; i++)
        {
            int imageIndex = i % imageList.Length;
            images[i] = imageList[imageIndex];
            images[pairsCount + i] = imageList[imageIndex];
        }

        // 打亂順序
        for (int i = images.Length - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            Sprite temp = images[i];
            images[i] = images[randomIndex];
            images[randomIndex] = temp;
        }

        // 建立卡片物件
        for (int i = 0; i < images.Length; i++)
        {
            GameObject cardObj = Instantiate(cardPrefab, gameBoard);
            MemoryCard card = cardObj.GetComponent<MemoryCard>();

            if (card == null)
            {
                Debug.LogError($"卡片 {i} 沒有 MemoryCard 腳本！");
                continue;
            }

            card.Initialize(i, images[i], OnCardFlipped);
            cards.Add(card);

            Debug.Log($"建立卡片 {i}");
        }

        Debug.Log($"遊戲已初始化，共 {cards.Count} 張卡片");
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

        // 更新計時器
        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0)
        {
            remainingTime = 0;
            gameActive = false;
            EndGameFail();
            return;
        }

        // 更新 UI
        if (timeText != null)
        {
            timeText.text = $"Time: {Mathf.RoundToInt(remainingTime)}s";
        }

        // ESC 鍵退出 - 使用新 Input System
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

        // 翻轉卡片
        card.Flip();

        if (firstFlippedCard == null)
        {
            // 第一張卡片
            firstFlippedCard = card;
        }
        else if (secondFlippedCard == null)
        {
            // 第二張卡片
            secondFlippedCard = card;
            isCheckingMatch = true;

            // 檢查是否配對
            StartCoroutine(CheckMatch());
        }
    }

    private IEnumerator CheckMatch()
    {
        yield return waitForMatchDelay;

        if (firstFlippedCard.GetImage() == secondFlippedCard.GetImage())
        {
            // 配對成功
            firstFlippedCard.SetMatched();
            secondFlippedCard.SetMatched();
            matchedPairs++;

            Debug.Log($"配對成功！ {matchedPairs}/{gridColumns * gridRows / 2}");

            // 更新 UI
            if (matchesText != null)
            {
                matchesText.text = $"Matches: {matchedPairs}/{gridColumns * gridRows / 2}";
            }

            // 檢查是否全部配對
            if (matchedPairs == gridColumns * gridRows / 2)
            {
                gameActive = false;
                EndGameWin();
            }
        }
        else
        {
            // 配對失敗，翻回去
            firstFlippedCard.UnFlip();
            secondFlippedCard.UnFlip();

            Debug.Log("配對失敗！");
        }

        // 重置狀態
        firstFlippedCard = null;
        secondFlippedCard = null;
        isCheckingMatch = false;
    }

    private void EndGameWin()
    {
        Debug.Log("=== 遊戲成功 ===");
        gameActive = false;

        StartCoroutine(ShowResultThenComplete(true));
    }

    private void EndGameFail()
    {
        Debug.Log("=== 遊戲失敗 ===");
        gameActive = false;

        StartCoroutine(ShowResultThenComplete(false));
    }

    private IEnumerator ShowResultThenComplete(bool success)
    {
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
        Debug.Log("重新開始遊戲");
        InitializeGame();
    }
}