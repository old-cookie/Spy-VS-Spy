using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TwoBarGame : MiniGame
{
    public Image bar1Fill;
    public Image bar2Fill;
    public Image targetLine1;
    public Image targetLine2;
    public Text statusText;
    public Text endText;

    public float fillSpeed = 0.3f;
    public float decaySpeed = 0.5f;
    public float minTargetFill = 0.5f;
    public float maxTargetFill = 0.8f;

    private float bar1FillValue = 0f;
    private float bar2FillValue = 0f;
    private float targetFill1;
    private float targetFill2;
    private bool gameEnded = false;

    private InputSystem_Actions inputActions;
    private InputAction directionAction;

    protected override void OnGameStart()
    {
        // 重置游戏状态
        gameEnded = false;
        bar1FillValue = 0f;
        bar2FillValue = 0f;
        
        // 随机生成目标填充值
        targetFill1 = Random.Range(minTargetFill, maxTargetFill);
        targetFill2 = Random.Range(minTargetFill, maxTargetFill);

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
        // 用 UPDownLeftRight action 处理所有方向
        directionAction = inputActions.MiniGame.UPDownLeftRight;
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

        var controlPath = context.control?.path ?? string.Empty;

        // 处理键盘箭头
        switch (controlPath)
        {
            case "/Keyboard/leftArrow":
                HandleLeftInput();
                return;
            case "/Keyboard/rightArrow":
                HandleRightInput();
                return;
        }

        // Fallback: 处理 gamepad/joystick 的方向值
        var dir = context.ReadValue<Vector2>();
        if (dir == Vector2.zero)
        {
            return;
        }

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            if (dir.x < 0)  // 左
            {
                HandleLeftInput();
            }
            else  // 右
            {
                HandleRightInput();
            }
        }
    }

    private void HandleLeftInput()
    {
        // 左箭头按下时立即增加
        bar1FillValue += fillSpeed * 0.1f;
        bar1FillValue = Mathf.Clamp01(bar1FillValue);
        Debug.Log($"Left Input: bar1 = {bar1FillValue:F2}");
    }

    private void HandleRightInput()
    {
        // 右箭头按下时立即增加
        bar2FillValue += fillSpeed * 0.1f;
        bar2FillValue = Mathf.Clamp01(bar2FillValue);
        Debug.Log($"Right Input: bar2 = {bar2FillValue:F2}");
    }

    protected override void Update()
    {
        if (gameEnded) return;

        // 缓慢衰减（没有按键时）
        bar1FillValue -= decaySpeed * Time.deltaTime * 0.5f;
        bar2FillValue -= decaySpeed * Time.deltaTime * 0.5f;

        bar1FillValue = Mathf.Clamp01(bar1FillValue);
        bar2FillValue = Mathf.Clamp01(bar2FillValue);

        // 检查是否两条都达到目标
        if (bar1FillValue >= targetFill1 && bar2FillValue >= targetFill2)
        {
            EndGame();
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
            float yPos = (targetFill1 - 0.5f) * 800f;  // -400 到 400
            targetLine1.rectTransform.anchoredPosition = new Vector2(0, yPos);
            Debug.Log($"TargetLine1 Y: {yPos}, targetFill1: {targetFill1}");
        }

        if (targetLine2 != null)
        {
            float yPos = (targetFill2 - 0.5f) * 800f;  // -400 到 400
            targetLine2.rectTransform.anchoredPosition = new Vector2(0, yPos);
            Debug.Log($"TargetLine2 Y: {yPos}, targetFill2: {targetFill2}");
        }

        // 更新状态文字
        if (statusText != null)
        {
            statusText.text = $"Bar1: {(bar1FillValue * 100):F0}%  |  Bar2: {(bar2FillValue * 100):F0}%";
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