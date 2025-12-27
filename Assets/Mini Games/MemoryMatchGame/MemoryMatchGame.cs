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
    private Button restartButton;

    [Header("Prefabs")]
    [SerializeField]
    private GameObject cardPrefab;

    [Header("Image List")]
    [SerializeField]
    private Sprite[] imageList = new Sprite[12];

    private readonly List<MemoryCard> cards = new();
    private Sprite[] images;
    private float remainingTime;
    private int matchedPairs = 0;
    private MemoryCard firstFlippedCard;
    private MemoryCard secondFlippedCard;
    private bool isCheckingMatch = false;
    private bool gameActive = true;

    private WaitForSeconds waitForMatchDelay;
    private WaitForSeconds waitForSeconds1_5;

    protected override void OnGameStart()
    {
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
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            Sprite temp = images[i];
            images[i] = images[randomIndex];
            images[randomIndex] = temp;
        }

        // Create card objects
        for (int i = 0; i < images.Length; i++)
        {
            GameObject cardObj = Instantiate(cardPrefab, gameBoard);
            MemoryCard card = cardObj.GetComponent<MemoryCard>();

            if (card == null)
            {
                Debug.LogError($"Card {i} does not have MemoryCard script!");
                continue;
            }

            card.Initialize(i, images[i], OnCardFlipped);
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
            firstFlippedCard.UnFlip();
            secondFlippedCard.UnFlip();
        }

        // Reset state
        firstFlippedCard = null;
        secondFlippedCard = null;
        isCheckingMatch = false;
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