using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FakeFloor : MonoBehaviour
{
    /// <summary>
    /// Temporarily hides a floor after a blinking warning, then restores it after a delay.
    /// </summary>
    [Header("Timings")]
    /// <summary>Time spent blinking before the floor disappears.</summary>
    public float warningDuration = 3f;

    /// <summary>Cooldown before the floor resets after disappearing.</summary>
    public float resetDelay = 10f;

    [Header("Blinking")]
    /// <summary>Slowest blink interval at the start of the warning.</summary>
    public float blinkIntervalStart = 0.5f;

    /// <summary>Fastest blink interval near the end of the warning.</summary>
    public float blinkIntervalEnd = 0.05f;

    private const string PlayerTag = "Player";

    private Renderer[] renderers;
    private Collider[] colliders;
    private bool isRunning;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        colliders = GetComponents<Collider>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isRunning)
        {
            return;
        }

        if (collision.collider.CompareTag(PlayerTag))
        {
            StartCoroutine(FakeFloorRoutine());
        }
    }

    private IEnumerator FakeFloorRoutine()
    {
        isRunning = true;

        float elapsed = 0f;
        bool visible = true;

        /// <remarks>Warning phase: blink faster as time approaches <see cref="warningDuration"/>.</remarks>
        while (elapsed < warningDuration)
        {
            SetVisible(visible);
            visible = !visible;

            float t = warningDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / warningDuration);
            float interval = Mathf.Lerp(blinkIntervalStart, blinkIntervalEnd, t);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        /// <remarks>Drop: hide and disable collisions.</remarks>
        SetVisible(false);
        SetCollidersEnabled(false);

        /// <remarks>Wait for reset.</remarks>
        yield return new WaitForSeconds(resetDelay);

        /// <remarks>Reset floor.</remarks>
        SetCollidersEnabled(true);
        SetVisible(true);
        isRunning = false;
    }

    private void SetVisible(bool state)
    {
        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                r.enabled = state;
            }
        }
    }

    private void SetCollidersEnabled(bool state)
    {
        foreach (Collider c in colliders)
        {
            if (c != null)
            {
                c.enabled = state;
            }
        }
    }
}
