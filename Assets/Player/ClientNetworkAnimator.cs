using UnityEngine;
using Unity.Netcode.Components;

/// <summary>
/// Client-authoritative network animator that allows clients to control their own animations.
/// This is used for player characters where the local client should have authority over animations.
/// </summary>
public class ClientNetworkAnimator : NetworkAnimator
{
    /// <summary>
    /// Overrides the default server-authoritative behavior to allow client authority.
    /// </summary>
    /// <returns>False to indicate client authority instead of server authority.</returns>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}