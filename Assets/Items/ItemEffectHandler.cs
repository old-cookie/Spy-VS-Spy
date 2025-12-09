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

    /// <summary>
    /// Multiplier applied to jump force when a super drink is consumed.
    /// </summary>
    [Header("Jump Boost Settings (Super Drink)")]
    [SerializeField, Range(1f, 5f)]
    private float jumpBoostMultiplier = 1.5f;

    /// <summary>
    /// Duration in seconds for the jump boost effect.
    /// </summary>
    [SerializeField, Min(0f)]
    private float jumpBoostDuration = 4f;

    /// <summary>
    /// Multiplier applied to slow down other players when rust gear is used.
    /// </summary>
    [Header("Slow Down Settings (Rust Gear)")]
    [SerializeField, Range(0.1f, 1f)]
    private float rustGearSlowDownMultiplier = 0.2f;

    /// <summary>
    /// Duration in seconds for the rust gear slow down effect.
    /// </summary>
    [SerializeField, Min(0f)]
    private float rustGearSlowDownDuration = 10f;

    private float speedBoostTimer;
    private float slowDownTimer;
    private float jumpBoostTimer;
    private float activeBoostMultiplier = 1f;
    private float activeSlowMultiplier = 1f;
    private float activeJumpMultiplier = 1f;
    private PlayerController playerController;

    /// <summary>
    /// Gets the current combined speed multiplier from all active effects.
    /// </summary>
    public float CurrentSpeedMultiplier => activeBoostMultiplier * activeSlowMultiplier;

    /// <summary>
    /// Gets the current jump force multiplier from active jump boost effects.
    /// </summary>
    public float CurrentJumpMultiplier => activeJumpMultiplier;

    private void Awake()
    {
        CachePlayerController();
    }

    /// <summary>
    /// Caches the PlayerController component reference.
    /// </summary>
    private void CachePlayerController()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }
    }

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

        // Cannot apply effects while playing mini game
        if (IsPlayingMiniGame())
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
            case "super drink":
                ApplyJumpBoost();
                break;
            case "rust gear":
                ApplyRustGearSlowDownServerRpc();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Checks if the player is currently playing a mini game.
    /// </summary>
    /// <returns>True if playing mini game, false otherwise.</returns>
    private bool IsPlayingMiniGame()
    {
        CachePlayerController();
        return playerController != null && playerController.IsPlayingMiniGame();
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

        if (jumpBoostTimer > 0f)
        {
            jumpBoostTimer = Mathf.Max(0f, jumpBoostTimer - Time.deltaTime);
            if (jumpBoostTimer <= 0f)
            {
                activeJumpMultiplier = 1f;
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

        // Cannot be affected by effects while playing mini game
        if (IsPlayingMiniGame())
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

    /// <summary>
    /// Applies the jump boost effect to this player.
    /// </summary>
    private void ApplyJumpBoost()
    {
        activeJumpMultiplier = jumpBoostMultiplier;
        jumpBoostTimer = jumpBoostDuration;
    }

    /// <summary>
    /// Server RPC to apply rust gear slow down effect to all other players.
    /// </summary>
    [ServerRpc]
    private void ApplyRustGearSlowDownServerRpc()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var player in players)
        {
            var handler = player.GetComponent<ItemEffectHandler>();
            if (handler != null && handler != this)
            {
                handler.ApplyRustGearSlowDownClientRpc();
            }
        }
    }

    /// <summary>
    /// Client RPC to receive and apply the rust gear slow down effect on the local player.
    /// </summary>
    [ClientRpc]
    public void ApplyRustGearSlowDownClientRpc()
    {
        if (!IsLocalPlayer)
        {
            return;
        }

        // Cannot be affected by effects while playing mini game
        if (IsPlayingMiniGame())
        {
            return;
        }

        ApplyRustGearSlowDown();
    }

    /// <summary>
    /// Applies the rust gear slow down effect to this player.
    /// </summary>
    private void ApplyRustGearSlowDown()
    {
        activeSlowMultiplier = rustGearSlowDownMultiplier;
        slowDownTimer = rustGearSlowDownDuration;
    }
}
