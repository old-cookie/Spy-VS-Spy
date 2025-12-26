using UnityEngine;
using UnityEngine.UI;
using System;

public class MemoryCard : MonoBehaviour
{
    private int cardIndex;
    private Sprite cardImage;
    private bool isFlipped = false;
    private bool isMatched = false;
    private Action<MemoryCard> onFlipped;
    private Image imageDisplay;
    private Image cardBackground;
    private Button cardButton;
    private Text questionMarkText;  // 加這一行

    public bool IsFlipped => isFlipped;
    public bool IsMatched => isMatched;

    public void Initialize(int index, Sprite image, Action<MemoryCard> callback)
    {
        cardIndex = index;
        cardImage = image;
        onFlipped = callback;

        // 找到 Image 元件（用來顯示圖片）
        imageDisplay = GetComponentInChildren<Image>();
        if (imageDisplay == null)
        {
            Debug.LogError("找不到 Image 元件！");
            return;
        }

        // 找到問號 Text 元件
        questionMarkText = GetComponentInChildren<Text>();

        // 卡片背景 Image
        cardBackground = GetComponent<Image>();
        cardButton = GetComponent<Button>();

        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClick);
        }

        // 初始狀態：背面
        ShowBack();
    }

    public void Flip()
    {
        if (isMatched || isFlipped)
            return;

        isFlipped = true;
        ShowFront();
    }

    public void UnFlip()
    {
        if (isMatched)
            return;

        isFlipped = false;
        ShowBack();
    }

    public void SetMatched()
    {
        isMatched = true;
        isFlipped = true;
        ShowFront();

        if (cardButton != null)
        {
            cardButton.interactable = false;
        }
    }

    public Sprite GetImage() => cardImage;

    private void ShowFront()
    {
        // 隱藏問號
        if (questionMarkText != null)
        {
            questionMarkText.enabled = false;
        }

        if (imageDisplay != null && cardImage != null)
        {
            imageDisplay.sprite = cardImage;
            imageDisplay.color = Color.white;
        }

        if (cardBackground != null)
        {
            cardBackground.color = Color.white;
        }
    }

    private void ShowBack()
    {
        // 顯示問號
        if (questionMarkText != null)
        {
            questionMarkText.enabled = true;
        }

        if (imageDisplay != null)
        {
            imageDisplay.sprite = null;
            imageDisplay.color = Color.clear;
        }

        if (cardBackground != null)
        {
            cardBackground.color = new Color(0.5f, 0.5f, 0.8f);
        }
    }

    private void OnCardClick()
    {
        if (!isFlipped && !isMatched)
        {
            onFlipped?.Invoke(this);
        }
    }

    private void OnDestroy()
    {
        if (cardButton != null)
        {
            cardButton.onClick.RemoveListener(OnCardClick);
        }
    }
}