using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Rust Gear item: slows OTHER players and plays a slow VFX on the affected player.
/// Implemented here so you don't need to put VFX code into ItemEffectHandler.
///
/// IMPORTANT: This item's ItemType is intentionally NOT "rust gear" so PlayerController's
/// ApplyItemEffect("rust gear") path won't run and double-apply the slow.
/// </summary>
public class RustGearItem : Item
{
    // Must NOT equal "rust gear" to avoid ItemEffectHandler also applying the effect.
    public override string ItemType => "rust gear vfx";

    [Header("Slow (Other Players)")]
    [SerializeField, Range(0f, 1f)]
    private float slowMultiplier = 0.2f;

    [SerializeField, Min(0f)]
    private float slowDuration = 10f;

    [Header("Slow VFX (Shows On Slowed Player)")]
    [SerializeField]
    private GameObject slowVfxPrefab;

    [Header("Slow SFX (Shows On Slowed Player)")]
    [SerializeField]
    private AudioClip slowSfxClip;

    [SerializeField]
    private AudioClip[] slowSfxClips;

    [SerializeField, Range(0f, 1f)]
    private float slowSfxVolume = 1f;

    [SerializeField]
    private bool debugLogs = false;

    [SerializeField]
    private bool avoidInheritingPlayerScale = true;

    [SerializeField]
    private Vector3 slowVfxLocalOffset = Vector3.zero;

    [SerializeField]
    private Vector3 slowVfxLocalEulerOffset = Vector3.zero;

    [SerializeField, Min(0f)]
    private float slowVfxDestroyAfterSeconds = 2f;

    [Header("Despawn")]
    [SerializeField, Min(0f)]
    private float despawnDelaySeconds = 0.1f;

    private bool consumeStarted;

    public override void Consume()
    {
        if (consumeStarted)
        {
            return;
        }

        consumeStarted = true;

        // Apply effect on server.
        ApplyRustGearServerRpc();

        // Notify chest immediately so it can reset state.
        NotifyOwnerConsumed();

        // Delay despawn slightly so RPCs have time to reach clients.
        if (IsServer)
        {
            StartCoroutine(DespawnAfterDelay(despawnDelaySeconds));
        }
        else
        {
            RequestDespawnAfterDelayServerRpc(despawnDelaySeconds);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ApplyRustGearServerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null)
        {
            return;
        }

        var senderClientId = rpcParams.Receive.SenderClientId;

        // Get the player who used the item.
        NetworkObject senderPlayerObj = null;
        if (NetworkManager.Singleton.ConnectedClients != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var senderClient) &&
            senderClient != null)
        {
            senderPlayerObj = senderClient.PlayerObject;
        }

        // Slow everyone except the sender.
        PlayerController[] allPlayers;
#if UNITY_2023_1_OR_NEWER
        allPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
#else
        allPlayers = Object.FindObjectsOfType<PlayerController>();
#endif

        foreach (var pc in allPlayers)
        {
            if (pc == null)
            {
                continue;
            }

            var targetNet = pc.GetComponent<NetworkObject>() ?? pc.GetComponentInParent<NetworkObject>();
            if (targetNet == null)
            {
                continue;
            }

            if (senderPlayerObj != null && targetNet.NetworkObjectId == senderPlayerObj.NetworkObjectId)
            {
                continue;
            }

            // Apply slow only to that player's owning client.
            var handler = pc.GetComponent<ItemEffectHandler>();
            if (handler != null)
            {
                var rpcParamsTarget = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { targetNet.OwnerClientId }
                    }
                };

                handler.ApplyForcedSlowClientRpc(slowMultiplier, slowDuration, rpcParamsTarget);
            }

            // Show VFX on the slowed player for everyone.
            if (slowVfxPrefab != null)
            {
                PlaySlowVfxClientRpc(targetNet.NetworkObjectId, targetNet.OwnerClientId);
            }
            else if (debugLogs)
            {
                Debug.LogWarning("[RustGearItem] slowVfxPrefab is not assigned; skipping slow VFX.", this);
            }
        }
    }

    [ClientRpc]
    private void PlaySlowVfxClientRpc(ulong targetPlayerNetworkObjectId, ulong targetOwnerClientId)
    {
        if (slowVfxPrefab == null || NetworkManager.Singleton == null)
        {
            return;
        }

        var targetTransform = ResolveTargetTransform(targetPlayerNetworkObjectId, targetOwnerClientId);
        if (targetTransform == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[RustGearItem] Could not resolve target transform. netId={targetPlayerNetworkObjectId} ownerClientId={targetOwnerClientId}", this);
            }
            return;
        }
        var worldPos = targetTransform.TransformPoint(slowVfxLocalOffset);
        var worldRot = targetTransform.rotation * Quaternion.Euler(slowVfxLocalEulerOffset);

        VfxSfxUtils.PlaySequenceAtPoint(slowSfxClip, slowSfxClips, worldPos, slowSfxVolume);

        GameObject vfx;
        if (avoidInheritingPlayerScale)
        {
            vfx = Instantiate(slowVfxPrefab, worldPos, worldRot);
            var follower = vfx.GetComponent<RustGearVfxFollower>();
            if (follower == null)
            {
                follower = vfx.AddComponent<RustGearVfxFollower>();
            }
            follower.Init(targetTransform, slowVfxLocalOffset, Quaternion.Euler(slowVfxLocalEulerOffset));
        }
        else
        {
            vfx = Instantiate(slowVfxPrefab, worldPos, worldRot, targetTransform);
        }

        if (vfx == null)
        {
            return;
        }

        var ps = vfx.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            var lifetime = main.duration;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
            {
                lifetime += main.startLifetime.constant;
            }
            if (lifetime <= 0f)
            {
                lifetime = slowVfxDestroyAfterSeconds;
            }
            if (lifetime > 0f)
            {
                Destroy(vfx, lifetime);
            }
        }
        else if (slowVfxDestroyAfterSeconds > 0f)
        {
            Destroy(vfx, slowVfxDestroyAfterSeconds);
        }
    }

    private Transform ResolveTargetTransform(ulong networkObjectId, ulong ownerClientId)
    {
        // Primary: lookup by NetworkObjectId
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var obj) && obj != null)
        {
            return obj.transform;
        }

        // Fallback: if this is the local owner's player
        var local = NetworkManager.Singleton.LocalClient;
        if (local != null && local.ClientId == ownerClientId)
        {
            var po = local.PlayerObject;
            if (po != null)
            {
                return po.transform;
            }
        }

        // Fallback: search any spawned NetworkObject with matching OwnerClientId and a PlayerController
        foreach (var kv in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
        {
            var no = kv.Value;
            if (no == null) continue;
            if (no.OwnerClientId != ownerClientId) continue;
            if (no.GetComponent<PlayerController>() != null || no.GetComponentInParent<PlayerController>() != null)
            {
                return no.transform;
            }
        }

        return null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDespawnAfterDelayServerRpc(float seconds)
    {
        if (!IsServer)
        {
            return;
        }

        StartCoroutine(DespawnAfterDelay(seconds));
    }

    private IEnumerator DespawnAfterDelay(float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        // Despawn item.
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private sealed class RustGearVfxFollower : MonoBehaviour
    {
        private Transform target;
        private Vector3 localOffset;
        private Quaternion localRotOffset;

        public void Init(Transform targetTransform, Vector3 offset, Quaternion rotOffset)
        {
            target = targetTransform;
            localOffset = offset;
            localRotOffset = rotOffset;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            transform.position = target.TransformPoint(localOffset);
            transform.rotation = target.rotation * localRotOffset;
        }
    }
}
