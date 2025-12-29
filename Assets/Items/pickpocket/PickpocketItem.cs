using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Pickpocket item: steals the nearest enemy player's held item.
/// Server-authoritative: server finds a valid target, transfers the Item, and targets both clients
/// to update their local held-item references.
/// </summary>
public class PickpocketItem : Item
{
    [Header("Pickpocket Settings")]
    [SerializeField, Min(0f)]
    private float range = 5f;

    [Header("Despawn")]
    [SerializeField, Min(0f)]
    private float despawnDelaySeconds = 0.1f;

    [Header("Debug")]
    [SerializeField]
    private bool debugLogs = false;

    private bool consumeStarted;

    public override string ItemType => "pickpocket";

    public override void Consume()
    {
        if (consumeStarted)
        {
            return;
        }

        consumeStarted = true;

        PickpocketServerRpc(range);
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
    private void PickpocketServerRpc(float rangeParam, RpcParams rpcParams = default)
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
                Debug.LogWarning("[PickpocketItem] Sender PlayerObject not found; aborted.", this);
            }
            return;
        }

        var senderPc = senderPlayerObj.GetComponent<PlayerController>() ?? senderPlayerObj.GetComponentInChildren<PlayerController>() ?? senderPlayerObj.GetComponentInParent<PlayerController>();
        if (senderPc == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[PickpocketItem] Sender PlayerController not found; aborted.", this);
            }
            return;
        }

        var senderNet = senderPc.GetComponent<NetworkObject>() ?? senderPc.GetComponentInParent<NetworkObject>();
        if (senderNet == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[PickpocketItem] Sender NetworkObject not found; aborted.", this);
            }
            return;
        }

        var senderTeam = senderPc.GetComponent<TeamMember>() ?? senderPc.GetComponentInChildren<TeamMember>() ?? senderPc.GetComponentInParent<TeamMember>();
        if (senderTeam == null || senderTeam.CurrentTeam == Team.None)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[PickpocketItem] Sender TeamMember missing/Team.None; aborted.", this);
            }
            return;
        }

        var myPos = senderPc.transform.position;
        var rangeSqr = rangeParam <= 0f ? float.MaxValue : rangeParam * rangeParam;

        PlayerController bestTarget = null;
        float bestDistSqr = float.MaxValue;

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

            var otherTeam = otherPc.GetComponent<TeamMember>() ?? otherPc.GetComponentInChildren<TeamMember>() ?? otherPc.GetComponentInParent<TeamMember>();
            if (otherTeam == null || otherTeam.CurrentTeam == Team.None)
            {
                continue;
            }

            if (otherTeam.CurrentTeam == senderTeam.CurrentTeam)
            {
                continue;
            }

            if (!otherPc.HasHeldItem())
            {
                continue;
            }

            var d = otherPc.transform.position - myPos;
            var distSqr = d.sqrMagnitude;
            if (distSqr > rangeSqr)
            {
                continue;
            }

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestTarget = otherPc;
            }
        }

        if (bestTarget == null)
        {
            if (debugLogs)
            {
                Debug.Log("[PickpocketItem] No enemy with a held item in range.", this);
            }
            return;
        }

        var targetNet = bestTarget.GetComponent<NetworkObject>() ?? bestTarget.GetComponentInParent<NetworkObject>();
        if (targetNet == null)
        {
            return;
        }

        var stolenItem = bestTarget.GetHeldItem();
        if (stolenItem == null)
        {
            return;
        }

        var stolenItemNet = stolenItem.GetComponent<NetworkObject>();
        if (stolenItemNet == null)
        {
            return;
        }

        var stolenItemNetworkObjectId = stolenItemNet.NetworkObjectId;

        // Update server-side references
        bestTarget.ClearHeldItemServer();
        senderPc.SetHeldItemServer(stolenItem);

        // Re-target item follow to the new owner
        if (ItemSpawnManager.Instance != null)
        {
            ItemSpawnManager.Instance.ChangeItemOwner(stolenItemNetworkObjectId, senderNet.NetworkObjectId);
        }

        // Tell each local player client to update their held-item reference
        var toSender = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { senderNet.OwnerClientId }
            }
        };

        var toVictim = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { targetNet.OwnerClientId }
            }
        };

        senderPc.SetHeldItemClientRpc(true, stolenItemNetworkObjectId, toSender);
        bestTarget.SetHeldItemClientRpc(false, 0, toVictim);

        if (debugLogs)
        {
            Debug.Log($"[PickpocketItem] Stole '{stolenItem.ItemType}' from {targetNet.NetworkObjectId} -> {senderNet.NetworkObjectId}", this);
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
}
