using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Swap Remote item: swaps positions with the nearest enemy.
/// Implemented here (not in ItemEffectHandler) and also plays a VFX on BOTH swapped players.
/// </summary>
public class SwapRemoteItem : Item
{
    // Intentionally NOT "swap remote" so PlayerController's ApplyItemEffect("swap remote") won't double-run via ItemEffectHandler.
    public override string ItemType => "swap remote vfx";

    [Header("Swap Settings")]
    [SerializeField, Min(0f)]
    private float range = 0f; // 0 = unlimited

    [Header("Swap VFX (Shows On Swapped Players)")]
    [SerializeField]
    private GameObject swapVfxPrefab;

    [SerializeField]
    private bool debugLogs = false;

    [SerializeField]
    private bool avoidInheritingPlayerScale = true;

    [SerializeField]
    private Vector3 swapVfxLocalOffset = new Vector3(0f, 1.5f, 0f);

    [SerializeField]
    private Vector3 swapVfxLocalEulerOffset = Vector3.zero;

    [SerializeField, Min(0f)]
    private float swapVfxDestroyAfterSeconds = 2f;

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

        ApplySwapServerRpc(range);

        NotifyOwnerConsumed();

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
    private void ApplySwapServerRpc(float rangeParam, RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null)
        {
            return;
        }

        var senderClientId = rpcParams.Receive.SenderClientId;
        NetworkObject senderPlayerObj = null;
        if (NetworkManager.Singleton.ConnectedClients != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var senderClient) &&
            senderClient != null)
        {
            senderPlayerObj = senderClient.PlayerObject;
        }

        if (senderPlayerObj == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[SwapRemoteItem] Sender PlayerObject not found; cannot swap.", this);
            }
            return;
        }

        var senderPc = senderPlayerObj.GetComponent<PlayerController>() ?? senderPlayerObj.GetComponentInChildren<PlayerController>() ?? senderPlayerObj.GetComponentInParent<PlayerController>();
        if (senderPc == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[SwapRemoteItem] Sender PlayerController not found; cannot swap.", this);
            }
            return;
        }

        var myTeamMember = senderPc.GetComponent<TeamMember>() ?? senderPc.GetComponentInChildren<TeamMember>() ?? senderPc.GetComponentInParent<TeamMember>();
        if (myTeamMember == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[SwapRemoteItem] Sender TeamMember not found; will swap with nearest player.", this);
            }
        }

        var myPos = senderPc.transform.position;
        var rangeSqr = rangeParam <= 0f ? float.MaxValue : rangeParam * rangeParam;

        PlayerController bestEnemyTarget = null;
        var bestEnemyDistSqr = float.MaxValue;

        PlayerController bestAnyTarget = null;
        var bestAnyDistSqr = float.MaxValue;

        PlayerController[] allPlayers;
#if UNITY_2023_1_OR_NEWER
        allPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
#else
        allPlayers = Object.FindObjectsOfType<PlayerController>();
#endif

        foreach (var otherPc in allPlayers)
        {
            if (otherPc == null || otherPc == senderPc)
            {
                continue;
            }

            var otherNet = otherPc.GetComponent<NetworkObject>() ?? otherPc.GetComponentInParent<NetworkObject>();
            if (otherNet == null)
            {
                continue;
            }

            var d = otherPc.transform.position - myPos;
            var distSqr = d.sqrMagnitude;
            if (distSqr > rangeSqr)
            {
                continue;
            }

            if (distSqr < bestAnyDistSqr)
            {
                bestAnyDistSqr = distSqr;
                bestAnyTarget = otherPc;
            }

            if (myTeamMember != null && myTeamMember.CurrentTeam != Team.None)
            {
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
        }

        var bestTarget = bestEnemyTarget != null ? bestEnemyTarget : bestAnyTarget;
        if (bestTarget == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[SwapRemoteItem] No valid target found. playersFound={allPlayers.Length}", this);
            }
            return;
        }

        var targetNetObj = bestTarget.GetComponent<NetworkObject>() ?? bestTarget.GetComponentInParent<NetworkObject>();
        if (targetNetObj == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[SwapRemoteItem] Target has no NetworkObject.", this);
            }
            return;
        }

        var myNewPos = bestTarget.transform.position;
        var targetNewPos = myPos;

        if (debugLogs)
        {
            Debug.Log($"[SwapRemoteItem] Swapping {senderPlayerObj.NetworkObjectId} <-> {targetNetObj.NetworkObjectId}", this);
        }

        SwapAndVfxClientRpc(
            senderPlayerObj.NetworkObjectId,
            senderPlayerObj.OwnerClientId,
            targetNetObj.NetworkObjectId,
            targetNetObj.OwnerClientId,
            myNewPos,
            targetNewPos);
    }

    [ClientRpc]
    private void SwapAndVfxClientRpc(
        ulong aPlayerNetworkObjectId,
        ulong aOwnerClientId,
        ulong bPlayerNetworkObjectId,
        ulong bOwnerClientId,
        Vector3 aNewPosition,
        Vector3 bNewPosition)
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        var aTransform = ResolveTargetTransform(aPlayerNetworkObjectId, aOwnerClientId);
        var bTransform = ResolveTargetTransform(bPlayerNetworkObjectId, bOwnerClientId);

        if (aTransform != null)
        {
            var aPc = aTransform.GetComponent<PlayerController>() ?? aTransform.GetComponentInChildren<PlayerController>() ?? aTransform.GetComponentInParent<PlayerController>();
            if (aPc != null)
            {
                aPc.TeleportToPosition(aNewPosition);
            }
            TrySpawnSwapVfx(aTransform);
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"[SwapRemoteItem] Could not resolve player A transform. netId={aPlayerNetworkObjectId} ownerClientId={aOwnerClientId}", this);
        }

        if (bTransform != null)
        {
            var bPc = bTransform.GetComponent<PlayerController>() ?? bTransform.GetComponentInChildren<PlayerController>() ?? bTransform.GetComponentInParent<PlayerController>();
            if (bPc != null)
            {
                bPc.TeleportToPosition(bNewPosition);
            }
            TrySpawnSwapVfx(bTransform);
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"[SwapRemoteItem] Could not resolve player B transform. netId={bPlayerNetworkObjectId} ownerClientId={bOwnerClientId}", this);
        }
    }

    private Transform ResolveTargetTransform(ulong networkObjectId, ulong ownerClientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var obj) && obj != null)
        {
            return obj.transform;
        }

        var local = NetworkManager.Singleton.LocalClient;
        if (local != null && local.ClientId == ownerClientId)
        {
            var po = local.PlayerObject;
            if (po != null)
            {
                return po.transform;
            }
        }

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

    private void TrySpawnSwapVfx(Transform targetTransform)
    {
        if (swapVfxPrefab == null || targetTransform == null)
        {
            return;
        }

        var worldPos = targetTransform.TransformPoint(swapVfxLocalOffset);
        var worldRot = targetTransform.rotation * Quaternion.Euler(swapVfxLocalEulerOffset);

        GameObject vfx;
        if (avoidInheritingPlayerScale)
        {
            vfx = Instantiate(swapVfxPrefab, worldPos, worldRot);
            var follower = vfx.GetComponent<SwapRemoteVfxFollower>();
            if (follower == null)
            {
                follower = vfx.AddComponent<SwapRemoteVfxFollower>();
            }
            follower.Init(targetTransform, swapVfxLocalOffset, Quaternion.Euler(swapVfxLocalEulerOffset));
        }
        else
        {
            vfx = Instantiate(swapVfxPrefab, worldPos, worldRot, targetTransform);
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
                lifetime = swapVfxDestroyAfterSeconds;
            }
            if (lifetime > 0f)
            {
                Destroy(vfx, lifetime);
            }
        }
        else if (swapVfxDestroyAfterSeconds > 0f)
        {
            Destroy(vfx, swapVfxDestroyAfterSeconds);
        }
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

    private sealed class SwapRemoteVfxFollower : MonoBehaviour
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
