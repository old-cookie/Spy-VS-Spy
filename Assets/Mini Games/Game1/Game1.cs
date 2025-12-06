using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;

public class Game1 : MiniGame
{
    public Image[] directionImages;      // 4 个提示图
    public Sprite[] directionSprites;    // 上下左右图
    public TMP_Text scoreText;           // UI 显示分数
    
    [Header("Game End Buttons")]
    public Button btnFinish;  // 完成按钮（成功时显示）
    public Button btnFail;    // 失败按钮（失败时显示）

    [Header("Audio")]
    public AudioClip bgMusic;              // 背景音乐
    public AudioClip correctDirectionSfx;  // 按中一个方向的音效
    public AudioClip completeSequenceSfx;  // 完成一组的音效
    public AudioClip wrongDirectionSfx;    // 按错方向的音效

    [Header("Audio Volume")]
    public float bgMusicVolume = 0.3f;     // 背景音乐的音量（0.3 = 30%）
    public float correctVolume = 1.0f;     // 按中方向的音量
    public float completeVolume = 1.0f;    // 完成一组的音量
    public float wrongVolume = 1.5f;       // 错误音效的音量（更大）

    private AudioSource audioSource;
    
    private List<int> sequence = new List<int>();
    private int playerIndex = 0;
    private int score = 0;

    private Color normalColor = Color.white;
    private Color correctColor = Color.green;
    private Color wrongColor = Color.red;

    private bool gameEnded = false;  // 游戏是否已结束

    void Start()
    {
        // 初始化音频源
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 立即设置 AudioSource 的基础属性
        audioSource.volume = bgMusicVolume;

        // 播放背景音乐
        if (bgMusic != null)
        {
            audioSource.clip = bgMusic;
            audioSource.loop = true;
            audioSource.Play();
            
            Debug.Log("[Game1] 背景音乐已播放，音量: " + audioSource.volume);
        }

        // 初始化时隐藏两个按钮
        if (btnFinish != null)
        {
            btnFinish.gameObject.SetActive(false);
            btnFinish.onClick.AddListener(OnFinishClicked);
        }

        if (btnFail != null)
        {
            btnFail.gameObject.SetActive(false);
            btnFail.onClick.AddListener(OnFailClicked);
        }

        UpdateScore();
        GenerateSequence();
    }

    void OnDestroy()
    {
        // 清理按钮监听
        if (btnFinish != null)
        {
            btnFinish.onClick.RemoveListener(OnFinishClicked);
        }

        if (btnFail != null)
        {
            btnFail.onClick.RemoveListener(OnFailClicked);
        }
    }

    void Update()
    {
        if (gameEnded)
            return;

        if (Input.GetKeyDown(KeyCode.UpArrow))        CheckInput(0);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) CheckInput(1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) CheckInput(2);
        else if (Input.GetKeyDown(KeyCode.RightArrow))CheckInput(3);
    }

    void GenerateSequence()
    {
        sequence.Clear();

        for (int i = 0; i < 4; i++)
        {
            int r = Random.Range(0, directionSprites.Length);
            sequence.Add(r);
        }

        for (int i = 0; i < 4; i++)
        {
            directionImages[i].sprite = directionSprites[sequence[i]];
            directionImages[i].color = normalColor;
            directionImages[i].enabled = true;
        }

        playerIndex = 0;

        Debug.Log("新顺序：" + 
            sequence[0] + "," + sequence[1] + "," + sequence[2] + "," + sequence[3]);
    }

    void CheckInput(int input)
    {
        int correctAnswer = sequence[playerIndex];

        // 正确输入
        if (input == correctAnswer)
        {
            StartCoroutine(FlashColor(directionImages[playerIndex], correctColor));

            // 播放按中方向的音效
            PlaySfx(correctDirectionSfx, correctVolume);

            playerIndex++;

            // 全部都按对
            if (playerIndex == 4)
            {
                score += 40;
                UpdateScore();

                Debug.Log("全部按对！ +40 分，总分=" + score);

                // 播放完成一组的音效
                PlaySfx(completeSequenceSfx, completeVolume);

                // 检查是否达到 160 分，自动完成游戏
                if (score >= 160)
                {
                    gameEnded = true;
                    Debug.Log("[Game1] 达到 160 分！显示 Finish 按钮");
                    ShowFinishButton(); 
                    return;
                }

                GenerateSequence();
            }
        }
        else
        {
            // 按错 → 当前位置闪红
            StartCoroutine(FlashColor(directionImages[playerIndex], wrongColor));

            // 播放按错方向的音效（更大声）
            PlaySfx(wrongDirectionSfx, wrongVolume);

            score -= 10;
            UpdateScore();

            Debug.Log("按错！ -10 分，总分=" + score);

            GenerateSequence();
        }
    }

    // 播放音效
    void PlaySfx(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    // 让图片闪一下颜色
    IEnumerator FlashColor(Image img, Color flashColor)
    {
        img.color = flashColor;
        yield return new WaitForSeconds(0.2f);
        img.color = normalColor;
    }

    // 更新 UI 分数
    void UpdateScore()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    // 显示完成按钮（分数 >= 160）
    void ShowFinishButton()
    {
        Debug.Log("[Game1] 显示 Finish 按钮");
        
        // 隐藏 Fail 按钮
        if (btnFail != null)
        {
            btnFail.gameObject.SetActive(false);
        }

        // 隐藏 Score Text
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
            Debug.Log("[Game1] Score Text 已隐藏");
        }

        // 显示 Finish 按钮
        if (btnFinish != null)
        {
            btnFinish.gameObject.SetActive(true);
        }
    }

    // 显示失败按钮（分数 < 160 或其他失败条件）
    void ShowFailButton()
    {
        Debug.Log("[Game1] 显示 Fail 按钮");
        
        // 隐藏 Finish 按钮
        if (btnFinish != null)
        {
            btnFinish.gameObject.SetActive(false);
        }

        // 显示 Fail 按钮
        if (btnFail != null)
        {
            btnFail.gameObject.SetActive(true);
        }
    }

    // Finish 按钮点击事件
    void OnFinishClicked()
    {
        Debug.Log("[Game1] Finish 按钮被点击");
        CompleteGame();
    }

    // Fail 按钮点击事件
    void OnFailClicked()
    {
        Debug.Log("[Game1] Fail 按钮被点击");
        FailGame();
    }

    // 游戏完成
    void CompleteGame()
    {
        Debug.Log("[Game1] 游戏完成！最终分数: " + score);
        base.CompleteGame();  // 调用基类的 CompleteGame()
    }
}