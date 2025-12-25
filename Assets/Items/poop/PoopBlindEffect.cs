using UnityEngine;
using UnityEngine.UI;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Client-side fullscreen overlay used to blind the player for a short duration.
/// </summary>
public class PoopBlindEffect : MonoBehaviour
{
    private static PoopBlindEffect instance;
    private Canvas canvas;
    private RawImage overlay;
    private Coroutine activeRoutine;

    [SerializeField]
    private Sprite blindImage; // Assign your blind effect image here in Inspector

    [SerializeField]
    private Texture2D blindTexture; // If your image is not a Sprite, assign a Texture2D here

    private const string DefaultBlindAssetName = "BlindImage";

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
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue; // ensure on top
        DontDestroyOnLoad(canvas.gameObject);

        canvas.gameObject.AddComponent<CanvasScaler>();
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        var rt = canvas.transform as RectTransform;
        overlay = new GameObject("Overlay").AddComponent<RawImage>();
        overlay.transform.SetParent(canvas.transform, false);
        var ort = overlay.rectTransform;
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;

        overlay.uvRect = new Rect(0, 0, 1, 1);
        
        EnsureBlindSprite();

        // Prefer explicit Texture2D, else use Sprite's underlying texture
        var tex = blindTexture;
        if (tex == null && blindImage != null)
        {
            tex = blindImage.texture;
        }
        
        // Set image texture and make it transparent initially
        if (tex != null)
        {
            overlay.texture = tex;
            overlay.color = new Color(1f, 1f, 1f, 0f); // White, start transparent
            Debug.Log($"[PoopBlindEffect] Using texture '{tex.name}'");
        }
        else
        {
            Debug.LogWarning("[PoopBlindEffect] No blind image found. Assign 'blindImage' on a PoopBlindEffect in your scene, or ensure the sprite is included in the build.");
            // Fallback to colored overlay if no image found
            overlay.texture = Texture2D.whiteTexture;
            overlay.color = new Color(0.05f, 0.02f, 0.01f, 0f);
        }
        
        overlay.raycastTarget = false;
        overlay.enabled = false;
    }

    public static void Show(float duration)
    {
        if (instance == null)
        {
            instance = FindExistingInstance();
            if (instance == null)
            {
                var go = new GameObject("PoopBlindEffect");
                instance = go.AddComponent<PoopBlindEffect>();
            }
        }

        if (!instance.gameObject.activeInHierarchy)
        {
            instance.gameObject.SetActive(true);
        }
        if (!instance.enabled)
        {
            instance.enabled = true;
        }

        if (instance.overlay == null)
        {
            instance.BuildOverlay();
        }

        Debug.Log($"[PoopBlindEffect] Show called. duration={duration}");
        instance.StartShow(duration);
    }

    private static PoopBlindEffect FindExistingInstance()
    {
        // Prefer a scene-placed instance so you can assign the sprite in the Inspector.
#if UNITY_2023_1_OR_NEWER
        var found = Object.FindFirstObjectByType<PoopBlindEffect>(FindObjectsInactive.Include);
        return found;
#else
        // Unity 2022+: FindObjectsOfType(includeInactive: true)
        var foundAll = Object.FindObjectsOfType<PoopBlindEffect>(true);
        return (foundAll != null && foundAll.Length > 0) ? foundAll[0] : null;
#endif
    }

    private void EnsureBlindSprite()
    {
        if (blindImage != null || blindTexture != null) return;

        // 1) Resources load (works only if asset is under a Resources folder)
        blindTexture = Resources.Load<Texture2D>(DefaultBlindAssetName);
        if (blindTexture != null) return;

        // 2) Sprite from Resources (if imported as Sprite)
        blindImage = Resources.Load<Sprite>(DefaultBlindAssetName);
        if (blindImage != null) return;

        // 3) Best-effort: find any already-loaded Texture/Sprite with name containing "BlindImage"
        var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        if (sprites != null)
        {
            foreach (var s in sprites)
            {
                if (s == null) continue;
                if (s.name.Contains(DefaultBlindAssetName))
                {
                    blindImage = s;
                    return;
                }
            }
        }

        var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
        if (textures != null)
        {
            foreach (var t in textures)
            {
                if (t == null) continue;
                if (t.name.Contains(DefaultBlindAssetName))
                {
                    blindTexture = t;
                    return;
                }
            }
        }

#if UNITY_EDITOR
        // 4) Editor-only: load asset by name even if it's not in Resources.
        // This helps when you have e.g. Assets/Items/poop/BlindImage.png.jpg.
        var guids = AssetDatabase.FindAssets(DefaultBlindAssetName);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                blindImage = sprite;
                Debug.Log($"[PoopBlindEffect] Loaded sprite from AssetDatabase: {path}");
                return;
            }

            var tex2d = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex2d != null)
            {
                blindTexture = tex2d;
                Debug.Log($"[PoopBlindEffect] Loaded texture from AssetDatabase: {path}");
                return;
            }
        }
#endif
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
        yield return FadeTo(1f, 0.12f);
        yield return new WaitForSecondsRealtime(duration);
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
            t += Time.unscaledDeltaTime;
            var a = Mathf.Lerp(start.a, targetAlpha, t / time);
            overlay.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }
        overlay.color = new Color(start.r, start.g, start.b, targetAlpha);
    }
}
