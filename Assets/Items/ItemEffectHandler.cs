using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(PlayerController))]

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

    /// <summary>
    /// Radius to search for nearby players to steal items from. Set to 0 or negative for unlimited range.
    /// </summary>
    [Header("Magnet Settings (Item Stealing)")]
    //private float itemStealRadius = 0f;

    [Header("Swap Remote Settings")]
    [SerializeField, Min(0f)]
    private float swapRemoteRange = 0f; // 0 = unlimited

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
            case "magnet":
                ApplyItemStealServerRpc();
                break;
            case "teleport":
                TeleportToSpawnServerRpc();
                break;
            case "swap remote":
                SwapRemoteServerRpc(swapRemoteRange);
                break;
            case "poop":
                // Handled by PoopItem.Consume() spawning projectile.
                break;
            case "bomb":
                // Bomb is handled by Bomb.cs Consume() method
                // No additional effect needed here
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

    /// <summary>
    /// Server RPC to steal an item from the opposing team player.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ApplyItemStealServerRpc()
    {
        CachePlayerController();
        if (playerController == null)
        {
            Debug.LogWarning("PlayerController not found. Cannot apply item steal.");
            return;
        }

        // Get this player's team
        var teamMember = playerController.GetComponent<TeamMember>();
        if (teamMember == null)
        {
            Debug.LogWarning("TeamMember not found. Cannot apply item steal.");
            return;
        }

        Team playerTeam = teamMember.CurrentTeam;
        if (playerTeam == Team.None)
        {
            Debug.LogWarning("Player is not on a team. Cannot apply item steal.");
            return;
        }

        // Determine opposing team
        Team opposingTeam = playerTeam == Team.Blue ? Team.Red : Team.Blue;
        Debug.Log($"Player team: {playerTeam}, Opposing team: {opposingTeam}");

        // Find the opposing team player (no range limit)
        var allPlayers = GameObject.FindGameObjectsWithTag("Player");
        Debug.Log($"Total players found: {allPlayers.Length}");

        var stealerNetworkObjectId = GetComponent<NetworkObject>().NetworkObjectId;

        foreach (var player in allPlayers)
        {
            var otherPlayerController = player.GetComponent<PlayerController>();
            var otherTeamMember = player.GetComponent<TeamMember>();

            if (otherPlayerController != null && otherTeamMember != null && otherTeamMember != teamMember)
            {
                Debug.Log($"Checking player: {player.name}, Team: {otherTeamMember.CurrentTeam}");

                // Check if this is the opposing team player
                if (otherTeamMember.IsOnTeam(opposingTeam))
                {
                    Debug.Log($"Player {player.name} is on the opposing team.");

                    if (otherPlayerController.HasHeldItem())
                    {
                        Debug.Log($"Player {player.name} has a held item. Stealing item.");

                        // Use RPC to steal from the opposing team player
                        otherPlayerController.StealHeldItemServerRpc(stealerNetworkObjectId);
                        Debug.Log("Item steal RPC sent successfully.");
                        return;
                    }
                    else
                    {
                        Debug.Log($"Player {player.name} does not have a held item.");
                    }
                }
            }
        }

        Debug.LogWarning("No valid target found for item steal.");
    }

    /// <summary>
    /// Server RPC to teleport the player back to their spawn position.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TeleportToSpawnServerRpc()
    {
        CachePlayerController();
        if (playerController == null)
        {
            Debug.LogWarning("PlayerController not found. Cannot apply teleport effect.");
            return;
        }

        // Get the player's network object
        var playerNetworkObject = GetComponent<NetworkObject>();
        if (playerNetworkObject == null)
        {
            Debug.LogWarning("NetworkObject not found on ItemEffectHandler.");
            return;
        }

        // Call the teleport on all clients for this player
        TeleportToSpawnClientRpc(playerNetworkObject.NetworkObjectId);
    }

    /// <summary>
    /// Client RPC to teleport the player back to spawn on all clients.
    /// </summary>
    [ClientRpc]
    private void TeleportToSpawnClientRpc(ulong playerNetworkObjectId)
    {
        // Get the player network object
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out var playerNetworkObject))
        {
            Debug.LogWarning($"[ItemEffectHandler] Player with NetworkObjectId {playerNetworkObjectId} not found.");
            return;
        }

        var targetPlayerController = playerNetworkObject.GetComponent<PlayerController>();
        if (targetPlayerController == null)
        {
            Debug.LogWarning("[ItemEffectHandler] PlayerController not found on target player.");
            return;
        }

        // Teleport the player back to spawn
        targetPlayerController.TeleportToSpawn();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SwapRemoteServerRpc(float range)
    {
        CachePlayerController();
        if (playerController == null)
        {
            Debug.LogWarning("[ItemEffectHandler] PlayerController not found. Cannot swap.");
            return;
        }

        var myNetObj = playerController.GetComponent<NetworkObject>() ?? playerController.GetComponentInParent<NetworkObject>();
        if (myNetObj == null)
        {
            Debug.LogWarning("[ItemEffectHandler] NetworkObject not found on player. Cannot swap.");
            return;
        }

        var myTeamMember = playerController.GetComponent<TeamMember>() ?? playerController.GetComponentInChildren<TeamMember>() ?? playerController.GetComponentInParent<TeamMember>();
        if (myTeamMember == null || myTeamMember.CurrentTeam == Team.None)
        {
            Debug.LogWarning("[ItemEffectHandler] TeamMember not found or Team.None. Cannot swap.");
            return;
        }

        var myPos = playerController.transform.position;
        var rangeSqr = range <= 0f ? float.MaxValue : range * range;

        PlayerController bestEnemyTarget = null;
        float bestEnemyDistSqr = float.MaxValue;

        PlayerController bestAnyTarget = null;
        float bestAnyDistSqr = float.MaxValue;

        PlayerController[] allPlayers;
    #if UNITY_2023_1_OR_NEWER
        allPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
    #else
        allPlayers = Object.FindObjectsOfType<PlayerController>();
    #endif
        foreach (var otherPc in allPlayers)
        {
            if (otherPc == null || otherPc == playerController) continue;

            var otherNet = otherPc.GetComponent<NetworkObject>() ?? otherPc.GetComponentInParent<NetworkObject>();
            if (otherNet == null) continue;

            var d = otherPc.transform.position - myPos;
            var distSqr = d.sqrMagnitude;
            if (distSqr > rangeSqr) continue;

            // Track nearest player regardless of team (fallback)
            if (distSqr < bestAnyDistSqr)
            {
                bestAnyDistSqr = distSqr;
                bestAnyTarget = otherPc;
            }

            var otherTeam = otherPc.GetComponent<TeamMember>() ?? otherPc.GetComponentInChildren<TeamMember>() ?? otherPc.GetComponentInParent<TeamMember>();
            if (otherTeam == null) continue;
            if (otherTeam.CurrentTeam == Team.None) continue;
            if (otherTeam.CurrentTeam == myTeamMember.CurrentTeam) continue;

            if (distSqr < bestEnemyDistSqr)
            {
                bestEnemyDistSqr = distSqr;
                bestEnemyTarget = otherPc;
            }
        }

        var bestTarget = bestEnemyTarget != null ? bestEnemyTarget : bestAnyTarget;
        if (bestTarget == null)
        {
            Debug.Log($"[ItemEffectHandler] Swap Remote: no valid target found. myTeam={myTeamMember.CurrentTeam} playersFound={allPlayers.Length}");
            return;
        }

        if (bestEnemyTarget == null)
        {
            Debug.LogWarning($"[ItemEffectHandler] Swap Remote: no enemy found (teams may be unset). Swapping with nearest player instead.");
        }

        var targetNetObj = bestTarget.GetComponent<NetworkObject>() ?? bestTarget.GetComponentInParent<NetworkObject>();
        if (targetNetObj == null)
        {
            Debug.LogWarning("[ItemEffectHandler] Swap Remote: target has no NetworkObject.");
            return;
        }

        var myNewPos = bestTarget.transform.position;
        var targetNewPos = myPos;

        Debug.Log($"[ItemEffectHandler] Swap Remote: swapping {myNetObj.NetworkObjectId} <-> {targetNetObj.NetworkObjectId}");
        SwapRemoteClientRpc(myNetObj.NetworkObjectId, targetNetObj.NetworkObjectId, myNewPos, targetNewPos);
    }

    [ClientRpc]
    private void SwapRemoteClientRpc(ulong aPlayerNetworkObjectId, ulong bPlayerNetworkObjectId, Vector3 aNewPosition, Vector3 bNewPosition)
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(aPlayerNetworkObjectId, out var aObj))
        {
            var aPc = aObj.GetComponent<PlayerController>();
            if (aPc != null)
            {
                aPc.TeleportToPosition(aNewPosition);
            }
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bPlayerNetworkObjectId, out var bObj))
        {
            var bPc = bObj.GetComponent<PlayerController>();
            if (bPc != null)
            {
                bPc.TeleportToPosition(bNewPosition);
            }
        }
    }
}



