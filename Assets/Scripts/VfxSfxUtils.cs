using UnityEngine;

public static class VfxSfxUtils
{
    public static void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        // Use our persistent runner so we can also support sequences in other calls.
        VfxSfxPlayer.Instance.PlayOneShotAtPoint(clip, position, volume);
    }

    /// <summary>
    /// Plays a sequence of clips at the same position, one after another.
    /// If sequenceClips is null/empty, falls back to the single clip.
    /// </summary>
    public static void PlaySequenceAtPoint(AudioClip singleClip, AudioClip[] sequenceClips, Vector3 position, float volume = 1f)
    {
        if (sequenceClips != null && sequenceClips.Length > 0)
        {
            VfxSfxPlayer.Instance.PlaySequenceAtPoint(sequenceClips, position, volume);
            return;
        }

        PlayAtPoint(singleClip, position, volume);
    }
}
