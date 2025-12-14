using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DualSync : MiniGame
{
    public Image bar1Fill;
    public Image bar2Fill;
    public Image targetLine1;
    public Image targetLine2;
    public Text statusText;
    public Text scoreText;
    public Text endText;
    public MiniGameTimer gameTimer;

    public float fillSpeed = 0.01f;
    public float decaySpeed = 0.5f;
    public float minTargetFill = 0.5f;
    public float maxTargetFill = 0.8f;
    public float targetTimeRequired = 1f;  // 在目标维持2秒
    public float scorePerTarget = 1f;      // 每次维持2秒获得1分
    public float gameTimeLimit = 30f;      // 游戏时间限制（秒）

    private float bar1FillValue = 0f;
    private float bar2FillValue = 0f;
    private float targetFill1;
    private float targetFill2;
    private bool gameEnded = false;
    private float timeInTarget = 0f;      // 在目标区域的时间
    private int score = 0;                 // 当前分数

    private InputSystem_Actions inputActions;

    protected override void OnGameStart()
    {
        // 重置游戏状态
        gameEnded = false;
        bar1FillValue = 0f;
        bar2FillValue = 0f;
        timeInTarget = 0f;
        score = 0;
        
        // 随机生成目标填充值
        targetFill1 = Random.Range(minTargetFill, maxTargetFill);
        targetFill2 = Random.Range(minTargetFill, maxTargetFill);

        // 启动计时器
        if (gameTimer != null)
        {
            gameTimer.OnTimeUp += OnTimerEnded;
            gameTimer.StartTimer(gameTimeLimit);
        }

        // 初始化 UI
        UpdateUI();
        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
        }
    }

    void Start()
    {
        SetupInput();

        // 随机生成目标填充值
        targetFill1 = Random.Range(minTargetFill, maxTargetFill);
        targetFill2 = Random.Range(minTargetFill, maxTargetFill);

        // 初始化 UI
        UpdateUI();
        if (endText != null)
        {
            endText.gameObject.SetActive(false);
        }
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

    private void SetupInput()
    {
        if (inputActions != null)
        {
            return;
        }

        inputActions = new InputSystem_Actions();
    }

    private void EnableInput()
    {
        inputActions?.MiniGame.Enable();
    }

    private void DisableInput()
    {
        inputActions?.MiniGame.Disable();
        inputActions?.Dispose();
        inputActions = null;
    }

    private void OnTimerEnded()
    {
        Debug.Log($"⏰ 时间到！游戏失败。总分数: {score}");
        FailGame();
    }

    private void HandleLongPressInput()
    {
        if (gameEnded)
        {
            return;
        }

        // 直接檢查鍵盤按鍵
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                bar1FillValue += fillSpeed * Time.deltaTime;
                bar1FillValue = Mathf.Clamp01(bar1FillValue);
            }

            if (Keyboard.current.rightArrowKey.isPressed)
            {
                bar2FillValue += fillSpeed * Time.deltaTime;
                bar2FillValue = Mathf.Clamp01(bar2FillValue);
            }
        }
    }

    protected override void Update()
    {
        if (gameEnded) return;

        // 处理长按输入（每帧读取）
        HandleLongPressInput();

        // 缓慢衰减（没有按键时）
        bar1FillValue -= decaySpeed * Time.deltaTime * 0.1f;
        bar2FillValue -= decaySpeed * Time.deltaTime * 0.1f;

        bar1FillValue = Mathf.Clamp01(bar1FillValue);
        bar2FillValue = Mathf.Clamp01(bar2FillValue);

        // 检查是否两条都在目标范围内（宽松范围：±0.15）
        bool bar1InTarget = bar1FillValue >= targetFill1 - 0.05f && bar1FillValue <= targetFill1 + 0.15f;
        bool bar2InTarget = bar2FillValue >= targetFill2 - 0.05f && bar2FillValue <= targetFill2 + 0.15f;

        if (bar1InTarget && bar2InTarget)
        {
            // 在目标范围内，累积时间
            timeInTarget += Time.deltaTime;

            // 每targetTimeRequired秒获得1分
            if (timeInTarget >= targetTimeRequired)
            {
                score += 1;  // 每次触发只加1分
                timeInTarget -= targetTimeRequired;  // 减去已计数的时间，允许继续计数
                Debug.Log($"✓ 得分! 总分数: {score}");
                
                // 检查是否达到5分，如果达到则完成游戏
                if (score >= 5)
                {
                    Debug.Log($"🎉 游戏完成！总分数: {score}");
                    CompleteGame();
                    return;
                }
                
                // 获得分数后，生成新的目标值并重置条
                targetFill1 = Random.Range(minTargetFill, maxTargetFill);
                targetFill2 = Random.Range(minTargetFill, maxTargetFill);
                bar1FillValue = 0f;
                bar2FillValue = 0f;
            }
        }
        else
        {
            // 离开目标范围，重置计时
            if (timeInTarget > 0)
            {
                Debug.Log($"离开目标范围 - Bar1: {bar1FillValue:F2} (目标:{targetFill1:F2}), Bar2: {bar2FillValue:F2} (目标:{targetFill2:F2})");
            }
            timeInTarget = 0f;
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        // 更新能量条填充
        if (bar1Fill != null)
        {
            bar1Fill.fillAmount = bar1FillValue;
        }
        
        if (bar2Fill != null)
        {
            bar2Fill.fillAmount = bar2FillValue;
        }

        // 更新目标线位置（因为 Bar 容器 Anchor 是 Center）
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

        // 更新状态文字 - 显示条的进度
        if (statusText != null)
        {
            float remainingTime = gameTimer != null ? gameTimer.RemainingTime : gameTimeLimit;
            statusText.text = $"Bar1: {(bar1FillValue * 100):F0}%  |  Bar2: {(bar2FillValue * 100):F0}%\nTime In Target: {timeInTarget:F1}s / {targetTimeRequired}s\nGame Time: {remainingTime:F1}s";
        }

        // 更新分数文字
        if (scoreText != null)
        {
            scoreText.text = $"Score: {score}";
        }
    }

    void EndGame()
    {
        gameEnded = true;
        if (endText != null)
        {
            endText.text = "✓ 完成！";
            endText.gameObject.SetActive(true);
        }
        
        // 调用 MiniGame 的完成方法
        CompleteGame();
    }
}