using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Text;
public class NumberMemory : MiniGame
{
    private static readonly WaitForSeconds _waitForSeconds1_5 = new(1.5f);
    [Header("UI References")]
    [SerializeField] private Text numberDisplay;
    [SerializeField] private Text readyText;
    [SerializeField] private Text text1;
    [SerializeField] private Text timeText;
    [SerializeField] private Text endText;
    [SerializeField] private Button startButton;
    [SerializeField] private MiniGameTimer gameTimer;
    // Cache number keys
    private static readonly Key[] NumberKeys =
    {
        Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5
    };
    [Header("Game Settings")]
    [SerializeField] private float showDuration = 2.5f;
    [SerializeField] private float timeLimit = 2.5f;
    private readonly int numberCount = NumberKeys.Length;
    [SerializeField] private float blinkSpeed = 10f;
    private readonly string inputPromptText = "Type the numbers (0-9 keys)";
    private readonly List<int> targetNumbers = new();
    private readonly List<int> inputNumbers = new();
    private readonly StringBuilder stringBuilder = new();
    private float elapsedTime;
    private bool isRunning;
    private bool isInputting;
    private bool isFinished;
    void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGameButtonPressed);
            startButton.interactable = true;
        }
        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }
        ResetUITexts();
    }
    void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGameButtonPressed);
        }
        if (gameTimer != null)
        {
            gameTimer.OnTimeUp -= OnTimerEnded;
        }
    }
    private void StartGameButtonPressed()
    {
        StopAllCoroutines();
        StartGame();
    }
    private void StartGame()
    {
        ResetGameState();
        ResetUITexts();
        GenerateTargetNumbers();
        string numbersStr = BuildNumberString(targetNumbers);
        if (numberDisplay != null)
        {
            numberDisplay.text = numbersStr;
        }
        if (readyText != null)
        {
            readyText.gameObject.SetActive(false);
        }
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }
        if (gameTimer != null)
        {
            gameTimer.SetDisplayVisible(true);
            gameTimer.OnTimeUp += OnTimerEnded;
            gameTimer.StartTimer(timeLimit);
        }
        StartCoroutine(BlinkNumbers());
    }
    private void ResetGameState()
    {
        isRunning = true;
        isFinished = false;
        isInputting = false;
        elapsedTime = 0f;
        targetNumbers.Clear();
        inputNumbers.Clear();
    }
    private void GenerateTargetNumbers()
    {
        for (int i = 0; i < numberCount; i++)
        {
            targetNumbers.Add(Random.Range(0, 10));
        }
    }
    private void ResetUITexts()
    {
        if (numberDisplay != null) numberDisplay.text = "";
        if (text1 != null) text1.text = "";
        if (timeText != null) timeText.text = "";
    }
    private IEnumerator BlinkNumbers()
    {
        if (numberDisplay == null) yield break;
        float blinkTime = 0f;
        Color originalColor = numberDisplay.color;

        while (blinkTime < showDuration)
        {
            blinkTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0.5f, 1f, (Mathf.Sin(blinkTime * blinkSpeed) + 1f) * 0.5f);
            Color color = originalColor;
            color.a = alpha;
            numberDisplay.color = color;
            yield return null;
        }
        // Restore and clear
        numberDisplay.color = originalColor;
        numberDisplay.text = "";
    }
    private void OnTimerEnded()
    {
        if (!isFinished)
        {
            EndGame(false);
        }
    }
    protected override void Update()
    {
        if (!isRunning || isFinished) return;

        if (!isInputting && elapsedTime > showDuration)
        {
            isInputting = true;
        }
        elapsedTime += Time.deltaTime;
        HandleInput();
        UpdateDisplay();
    }
    private void UpdateDisplay()
    {
        if (!isInputting) return;
        // Update input display
        if (text1 != null)
        {
            text1.text = inputNumbers.Count > 0
                ? BuildNumberString(inputNumbers)
                : inputPromptText;
        }
        // Update time display
        if (timeText != null && gameTimer != null)
        {
            timeText.text = "Time: " + Mathf.CeilToInt(gameTimer.RemainingTime) + "s";
        }
    }
    private string BuildNumberString(List<int> numbers)
    {
        stringBuilder.Clear();
        foreach (int num in numbers)
        {
            stringBuilder.Append(num).Append(' ');
        }
        return stringBuilder.ToString();
    }
    private void HandleInput()
    {
        if (!isInputting || isFinished) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        for (int i = 0; i < NumberKeys.Length; i++)
        {
            if (keyboard[NumberKeys[i]].wasPressedThisFrame)
            {
                OnNumberInput(i);
                return;
            }
        }
    }
    private void OnNumberInput(int number)
    {
        inputNumbers.Add(number);
        UpdateDisplay();
        if (inputNumbers.Count >= numberCount)
        {
            CheckAnswer();
        }
    }
    private void CheckAnswer()
    {
        bool isCorrect = IsAnswerCorrect();
        EndGame(isCorrect);
    }
    private bool IsAnswerCorrect()
    {
        if (targetNumbers.Count != inputNumbers.Count) return false;
        for (int i = 0; i < targetNumbers.Count; i++)
        {
            if (targetNumbers[i] != inputNumbers[i]) return false;
        }
        return true;
    }
    private void EndGame(bool success)
    {
        isRunning = false;
        isFinished = true;
        StopGameTimer();
        ShowResultPopup(success);
    }
    private void StopGameTimer()
    {
        if (gameTimer != null)
        {
            gameTimer.OnTimeUp -= OnTimerEnded;
            gameTimer.StopTimer();
        }
    }
    private void ShowResultPopup(bool success)
    {
        StartCoroutine(ShowEndThenComplete(success ? "YOU WIN!" : "YOU LOSE!", success));
    }
    private IEnumerator ShowEndThenComplete(string message, bool success)
    {
        // Hide game UI
        if (numberDisplay != null) numberDisplay.gameObject.SetActive(false);
        if (readyText != null) readyText.gameObject.SetActive(false);
        if (text1 != null) text1.gameObject.SetActive(false);
        if (timeText != null) timeText.gameObject.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(false);
        // Show result text
        if (endText != null)
        {
            endText.text = message;
            endText.color = success ? new Color(0, 0.7f, 0, 1) : Color.red;
            endText.gameObject.SetActive(true);
        }
        yield return _waitForSeconds1_5;
        // Hide result text
        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }
        // Complete or fail the game
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