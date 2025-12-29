using System.Collections;
using UnityEngine;

public sealed class VfxSfxPlayer : MonoBehaviour
{
    private static VfxSfxPlayer instance;

    public static VfxSfxPlayer Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            var go = new GameObject("VfxSfxPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<VfxSfxPlayer>();
            return instance;
        }
    }

    public void PlayOneShotAtPoint(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null)
        {
            return;
        }

        var go = new GameObject($"OneShotAudio_{clip.name}");
        go.transform.position = position;

        var source = go.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.volume = Mathf.Clamp01(volume);
        source.clip = clip;
        source.Play();

        Destroy(go, Mathf.Max(0.1f, clip.length));
    }

    public void PlaySequenceAtPoint(AudioClip[] clips, Vector3 position, float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        StartCoroutine(PlaySequenceCoroutine(clips, position, volume));
    }

    private IEnumerator PlaySequenceCoroutine(AudioClip[] clips, Vector3 position, float volume)
    {
        var go = new GameObject("SequenceAudio");
        go.transform.position = position;

        var source = go.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.volume = Mathf.Clamp01(volume);

        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            source.clip = clip;
            source.Play();

            var wait = Mathf.Max(0.01f, clip.length);
            yield return new WaitForSeconds(wait);
        }

        Destroy(go);
    }
}
