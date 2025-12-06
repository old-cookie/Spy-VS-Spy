using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Manages countdown timer for mini games with optional UI display.
/// </summary>
public class MiniGameTimer : MonoBehaviour
{
    /// <summary>
    /// Optional UI text element to display the remaining time.
    /// </summary>
    public Text timeText;

    /// <summary>
    /// Event fired when the timer runs out.
    /// </summary>
    public event Action OnTimeUp;

    /// <summary>
    /// The remaining time in seconds.
    /// </summary>
    public float RemainingTime { get; private set; }

    /// <summary>
    /// Whether the timer is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// The initial time limit for the current game session.
    /// </summary>
    private float timeLimit;

    /// <summary>
    /// Starts or restarts the timer with the specified time limit.
    /// </summary>
    /// <param name="seconds">The time limit in seconds.</param>
    public void StartTimer(float seconds)
    {
        timeLimit = seconds;
        RemainingTime = seconds;
        IsRunning = true;
        UpdateDisplay();
    }

    /// <summary>
    /// Stops the timer.
    /// </summary>
    public void StopTimer()
    {
        IsRunning = false;
    }

    /// <summary>
    /// Resets the timer to the initial time limit.
    /// </summary>
    public void ResetTimer()
    {
        RemainingTime = timeLimit;
        IsRunning = false;
        UpdateDisplay();
    }

    /// <summary>
    /// Update is called once per frame. Counts down the timer.
    /// </summary>
    void Update()
    {
        if (!IsRunning)
        {
            return;
        }

        RemainingTime -= Time.deltaTime;
        UpdateDisplay();

        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            IsRunning = false;
            UpdateDisplay();
            OnTimeUp?.Invoke();
        }
    }

    /// <summary>
    /// Updates the time display text if available.
    /// </summary>
    private void UpdateDisplay()
    {
        if (timeText != null)
        {
            timeText.text = "Time: " + Mathf.CeilToInt(RemainingTime) + "s";
        }
    }

    /// <summary>
    /// Shows or hides the time display text.
    /// </summary>
    /// <param name="visible">Whether the text should be visible.</param>
    public void SetDisplayVisible(bool visible)
    {
        if (timeText != null)
        {
            timeText.gameObject.SetActive(visible);
        }
    }
}
