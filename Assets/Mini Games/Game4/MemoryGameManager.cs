using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class MemoryGameManager : MiniGame
{
    [SerializeField] private TextMeshProUGUI numberDisplay;
    [SerializeField] private TextMeshProUGUI readyText;
    [SerializeField] private TextMeshProUGUI text1;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private Button startButton;
    [SerializeField] private MiniGameTimer gameTimer;
    
    // 在 Inspector 上設定的文字
    [SerializeField] private string readyTextContent = "Ready.\nPress Start";
    [SerializeField] private string inputPromptText = "Type the numbers\n(0-9 keys)";
    
    // 結果彈窗
    private GameObject resultPopup;
    private TextMeshProUGUI resultText;
    private Button resultButton;

    private List<int> targetNumbers = new List<int>();
    private List<int> inputNumbers = new List<int>();
    
    private float elapsedTime = 0f;
    private bool isRunning = false;
    private bool isInputting = false;
    private bool isFinished = false;

    public float showDuration = 1f;
    public float timeLimit = 15f;

    void Start()
    {
        Debug.Log("=== MemoryGameManager Start ===");
        
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGameButtonPressed);
            Debug.Log("Button listener added");
            startButton.interactable = true;
        }

        if (numberDisplay != null)
        {
            numberDisplay.text = "";
        }

        if (readyText != null)
        {
            readyText.text = readyTextContent;
        }

        if (text1 != null)
        {
            text1.text = "";
        }

        if (timeText != null)
        {
            timeText.text = "";
        }

        // 創建結果彈窗
        CreateResultPopup();
    }

    /// <summary>
    /// 創建結果彈窗 UI
    /// </summary>
    private void CreateResultPopup()
    {
        Canvas popupCanvas = FindObjectOfType<Canvas>();
        if (popupCanvas == null)
        {
            Debug.LogError("No Canvas found!");
            return;
        }

        // 創建彈窗面板
        resultPopup = new GameObject("ResultPopup");
        resultPopup.transform.SetParent(popupCanvas.transform, false);
        
        Image panelImage = resultPopup.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);

        RectTransform popupRect = resultPopup.GetComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;

        // 創建內容面板
        GameObject contentPanel = new GameObject("ContentPanel");
        contentPanel.transform.SetParent(resultPopup.transform, false);
        
        Image contentImage = contentPanel.AddComponent<Image>();
        contentImage.color = new Color(1, 1, 1, 1);

        RectTransform contentRect = contentPanel.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(400, 300);

        // 創建結果文字
        GameObject resultTextObj = new GameObject("ResultText");
        resultTextObj.transform.SetParent(contentPanel.transform, false);
        
        resultText = resultTextObj.AddComponent<TextMeshProUGUI>();
        resultText.text = "YOU WIN!";
        resultText.fontSize = 80;
        resultText.alignment = TextAlignmentOptions.Center;
        resultText.color = Color.black;

        RectTransform resultTextRect = resultTextObj.GetComponent<RectTransform>();
        resultTextRect.sizeDelta = new Vector2(400, 150);
        resultTextRect.anchoredPosition = new Vector2(0, 50);

        // 創建按鈕
        GameObject buttonObj = new GameObject("OKButton");
        buttonObj.transform.SetParent(contentPanel.transform, false);
        
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.6f, 1f, 1f);

        resultButton = buttonObj.AddComponent<Button>();
        resultButton.targetGraphic = buttonImage;

        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(200, 60);
        buttonRect.anchoredPosition = new Vector2(0, -80);

        // 按鈕文字
        GameObject buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        
        TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "OK";
        buttonText.fontSize = 40;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.white;

        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.sizeDelta = new Vector2(200, 60);

        resultPopup.SetActive(false);
    }

    private void StartGameButtonPressed()
    {
        Debug.Log("START BUTTON CLICKED!");
        StopAllCoroutines();
        StartGame();
    }

    private void StartGame()
    {
        Debug.Log("=== GAME STARTED ===");
        
        isRunning = true;
        isFinished = false;
        isInputting = false;
        elapsedTime = 0f;

        targetNumbers.Clear();
        inputNumbers.Clear();

        for (int i = 0; i < 6; i++)
        {
            targetNumbers.Add(Random.Range(0, 10));
        }

        string numbersStr = "";
        foreach (int num in targetNumbers)
        {
            numbersStr += num + " ";
        }
        
        Debug.Log($"Generated numbers: {numbersStr}");

        if (numberDisplay != null)
        {
            numberDisplay.text = numbersStr;
            numberDisplay.fontSize = 80;
        }

        if (readyText != null)
        {
            readyText.text = "";
        }

        if (text1 != null)
        {
            text1.text = "";
        }

        if (timeText != null)
        {
            timeText.text = "";
        }

        if (startButton != null)
        {
            startButton.interactable = false;
        }

        if (gameTimer != null)
        {
            gameTimer.OnTimeUp += OnTimerEnded;
            gameTimer.StartTimer(timeLimit);
        }

        StartCoroutine(BlinkNumbers());
    }

    private IEnumerator BlinkNumbers()
    {
        Debug.Log("Blinking...");
        float blinkTime = 0f;

        while (blinkTime < showDuration)
        {
            blinkTime += Time.deltaTime;
            float alpha = (Mathf.Sin(blinkTime * 10f) + 1f) / 2f;
            alpha = 0.5f + alpha * 0.5f;

            if (numberDisplay != null)
            {
                Color color = numberDisplay.color;
                color.a = alpha;
                numberDisplay.color = color;
            }

            yield return null;
        }

        if (numberDisplay != null)
        {
            Color finalColor = numberDisplay.color;
            finalColor.a = 1f;
            numberDisplay.color = finalColor;
            numberDisplay.text = "";
        }

        Debug.Log("Blink complete, ready for input");
    }

    private void OnTimerEnded()
    {
        Debug.Log("Time up!");
        if (!isFinished)
        {
            EndGame(false);
        }
    }

    protected override void Update()
    {
        if (!isRunning || isFinished) return;

        elapsedTime += Time.deltaTime;

        if (elapsedTime > timeLimit)
        {
            EndGame(false);
            return;
        }

        if (!isInputting && elapsedTime > showDuration)
        {
            isInputting = true;
            Debug.Log("Ready for input");
        }

        HandleInput();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (isInputting)
        {
            // 更新 text1 - 顯示輸入的數字
            if (text1 != null)
            {
                string inputStr = "";
                foreach (int num in inputNumbers)
                {
                    inputStr += num + " ";
                }

                if (inputStr.Length > 0)
                {
                    text1.text = inputStr;
                }
                else
                {
                    text1.text = inputPromptText;
                }
            }

            // 更新 timeText - 顯示時間和進度
            if (timeText != null)
            {
                float remainingTime = timeLimit - elapsedTime;
                timeText.text = $"Time: {remainingTime:F1}s\n({inputNumbers.Count}/6)";
            }
        }
    }

    private void HandleInput()
    {
        if (!isInputting || isFinished) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            Debug.LogError("Keyboard not available!");
            return;
        }

        Key[] numberKeys = new Key[]
        {
            Key.Digit0,
            Key.Digit1,
            Key.Digit2,
            Key.Digit3,
            Key.Digit4,
            Key.Digit5,
            Key.Digit6,
            Key.Digit7,
            Key.Digit8,
            Key.Digit9
        };

        for (int i = 0; i < 10; i++)
        {
            if (keyboard[numberKeys[i]].wasPressedThisFrame)
            {
                Debug.Log($"Key {i} pressed!");
                inputNumbers.Add(i);

                UpdateDisplay();

                if (inputNumbers.Count == 6)
                {
                    CheckAnswer();
                    return;
                }

                return;
            }
        }
    }

    private void CheckAnswer()
    {
        Debug.Log("Checking answer...");

        string targetStr = "";
        string yourStr = "";

        foreach (int num in targetNumbers)
        {
            targetStr += num;
        }

        foreach (int num in inputNumbers)
        {
            yourStr += num;
        }

        Debug.Log($"Target: {targetStr}, You: {yourStr}");

        if (targetStr == yourStr)
        {
            Debug.Log("All correct!");
            EndGame(true);
        }
        else
        {
            Debug.Log("Wrong!");
            EndGame(false);
        }
    }

    private void EndGame(bool success)
    {
        isRunning = false;
        isFinished = true;

        if (gameTimer != null)
        {
            gameTimer.OnTimeUp -= OnTimerEnded;
            gameTimer.StopTimer();
        }

        if (resultPopup != null)
        {
            resultPopup.SetActive(true);
            
            if (success)
            {
                resultText.text = "YOU WIN!";
                resultText.color = new Color(0, 0.7f, 0, 1);
                Debug.Log("GAME WON!");
            }
            else
            {
                resultText.text = "YOU LOSE!";
                resultText.color = new Color(1, 0, 0, 1);
                Debug.Log("GAME LOST!");
            }

            resultButton.onClick.RemoveAllListeners();
            resultButton.onClick.AddListener(() => OnResultButtonClicked(success));
        }

        if (numberDisplay != null)
        {
            numberDisplay.text = "";
        }

        if (readyText != null)
        {
            readyText.text = readyTextContent;
        }

        if (text1 != null)
        {
            text1.text = "";
        }

        if (timeText != null)
        {
            timeText.text = "";
        }

        if (startButton != null)
        {
            startButton.interactable = true;
        }
    }

    private void OnResultButtonClicked(bool success)
    {
        Debug.Log($"Result button clicked. Success: {success}");
        
        if (resultPopup != null)
        {
            resultPopup.SetActive(false);
        }

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