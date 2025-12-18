using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Client-side fullscreen overlay used to blind the player for a short duration.
/// </summary>
public class PoopBlindEffect : MonoBehaviour
{
    private static PoopBlindEffect instance;
    private Canvas canvas;
    private Image overlay;
    private Coroutine activeRoutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        canvas = new GameObject("PoopBlindCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // very high to ensure on top
        DontDestroyOnLoad(canvas.gameObject);

        var rt = canvas.transform as RectTransform;
        overlay = new GameObject("Overlay").AddComponent<Image>();
        overlay.transform.SetParent(canvas.transform, false);
        var ort = overlay.rectTransform;
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;
        overlay.color = new Color(0.25f, 0.15f, 0.05f, 0f); // brownish, start transparent
        overlay.raycastTarget = false;
        overlay.enabled = false;
    }

    public static void Show(float duration)
    {
        if (instance == null)
        {
            var go = new GameObject("PoopBlindEffect");
            instance = go.AddComponent<PoopBlindEffect>();
        }

        instance.StartShow(duration);
    }

    private void StartShow(float duration)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }
        activeRoutine = StartCoroutine(DoBlind(duration));
    }

    private IEnumerator DoBlind(float duration)
    {
        overlay.enabled = true;
        // Fade in quickly
        yield return FadeTo(0.85f, 0.15f);
        yield return new WaitForSeconds(duration);
        // Fade out quickly
        yield return FadeTo(0f, 0.2f);
        overlay.enabled = false;
        activeRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float time)
    {
        var start = overlay.color;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            var a = Mathf.Lerp(start.a, targetAlpha, t / time);
            overlay.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }
        overlay.color = new Color(start.r, start.g, start.b, targetAlpha);
    }
}
