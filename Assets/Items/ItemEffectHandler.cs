using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Handles item effects applied to the player, such as speed boosts and slow downs.
/// </summary>
public class ItemEffectHandler : NetworkBehaviour
{
    /// <summary>
    /// Multiplier applied to player speed when a cookie is consumed.
    /// </summary>
    [Header("Speed Boost Settings (Cookie)")]
    [SerializeField, Range(1f, 5f)]
    private float speedBoostMultiplier = 2f;

    /// <summary>
    /// Duration in seconds for the speed boost effect.
    /// </summary>
    [SerializeField, Min(0f)]
    private float speedBoostDuration = 5f;

    /// <summary>
    /// Multiplier applied to slow down other players when a banana is used.
    /// </summary>
    [Header("Slow Down Settings (Banana)")]
    [SerializeField, Range(0.1f, 1f)]
    private float slowDownMultiplier = 0.5f;

    /// <summary>
    /// Duration in seconds for the slow down effect.
    /// </summary>
    [SerializeField, Min(0f)]
    private float slowDownDuration = 3f;

    private float speedBoostTimer;
    private float slowDownTimer;
    private float activeBoostMultiplier = 1f;
    private float activeSlowMultiplier = 1f;

    /// <summary>
    /// Gets the current combined speed multiplier from all active effects.
    /// </summary>
    public float CurrentSpeedMultiplier => activeBoostMultiplier * activeSlowMultiplier;

    private void Update()
    {
        UpdateEffectTimers();
    }

    /// <summary>
    /// Applies an effect based on the consumed item type.
    /// </summary>
    /// <param name="itemType">The type identifier of the consumed item.</param>
    public void ApplyEffect(string itemType)
    {
        if (string.IsNullOrEmpty(itemType))
        {
            return;
        }

        switch (itemType)
        {
            case "cookie":
                ApplySpeedBoost();
                break;
            case "banana":
                ApplySlowDownToOthersServerRpc();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Updates the effect timers and resets multipliers when effects expire.
    /// </summary>
    private void UpdateEffectTimers()
    {
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer = Mathf.Max(0f, speedBoostTimer - Time.deltaTime);
            if (speedBoostTimer <= 0f)
            {
                activeBoostMultiplier = 1f;
            }
        }

        if (slowDownTimer > 0f)
        {
            slowDownTimer = Mathf.Max(0f, slowDownTimer - Time.deltaTime);
            if (slowDownTimer <= 0f)
            {
                activeSlowMultiplier = 1f;
            }
        }
    }

    /// <summary>
    /// Applies the speed boost effect to this player.
    /// </summary>
    private void ApplySpeedBoost()
    {
        activeBoostMultiplier = speedBoostMultiplier;
        speedBoostTimer = speedBoostDuration;
    }

    /// <summary>
    /// Server RPC to apply slow down effect to all other players.
    /// </summary>
    [ServerRpc]
    private void ApplySlowDownToOthersServerRpc()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var player in players)
        {
            var handler = player.GetComponent<ItemEffectHandler>();
            if (handler != null && handler != this)
            {
                handler.ApplySlowDownClientRpc();
            }
        }
    }

    /// <summary>
    /// Client RPC to receive and apply the slow down effect on the local player.
    /// </summary>
    [ClientRpc]
    public void ApplySlowDownClientRpc()
    {
        if (!IsLocalPlayer)
        {
            return;
        }

        ApplySlowDown();
    }

    /// <summary>
    /// Applies the slow down effect to this player.
    /// </summary>
    private void ApplySlowDown()
    {
        activeSlowMultiplier = slowDownMultiplier;
        slowDownTimer = slowDownDuration;
    }
}
