using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// EMP item: disables enemy players from using their held item for a short duration.
/// Server-authoritative: server finds nearby enemies and sends them a targeted ClientRpc lock.
/// </summary>
public class EmpItem : Item
{
    [Header("EMP Settings")]
    [SerializeField, Min(0f)]
    private float range = 5f;

    [SerializeField, Min(0f)]
    private float disableUseDuration = 3f;

    [Header("Despawn")]
    [SerializeField, Min(0f)]
    private float despawnDelaySeconds = 0.1f;

    [Header("Debug")]
    [SerializeField]
    private bool debugLogs = false;

    private bool consumeStarted;

    public override string ItemType => "emp";

    public override void Consume()
    {
        if (consumeStarted)
        {
            return;
        }

        consumeStarted = true;

        ApplyEmpServerRpc(range, disableUseDuration);
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
    private void ApplyEmpServerRpc(float rangeParam, float durationParam, RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null)
        {
            return;
        }

        durationParam = Mathf.Max(0f, durationParam);
        if (durationParam <= 0f)
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
                Debug.LogWarning("[EmpItem] Sender PlayerObject not found; EMP aborted.", this);
            }
            return;
        }

        var senderPc = senderPlayerObj.GetComponent<PlayerController>() ?? senderPlayerObj.GetComponentInChildren<PlayerController>() ?? senderPlayerObj.GetComponentInParent<PlayerController>();
        if (senderPc == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[EmpItem] Sender PlayerController not found; EMP aborted.", this);
            }
            return;
        }

        var senderTeamMember = senderPc.GetComponent<TeamMember>() ?? senderPc.GetComponentInChildren<TeamMember>() ?? senderPc.GetComponentInParent<TeamMember>();
        if (senderTeamMember == null || senderTeamMember.CurrentTeam == Team.None)
        {
            if (debugLogs)
            {
                Debug.LogWarning("[EmpItem] Sender TeamMember missing/Team.None; EMP aborted (enemy team unknown).", this);
            }
            return;
        }

        var myPos = senderPc.transform.position;
        var rangeSqr = rangeParam <= 0f ? float.MaxValue : rangeParam * rangeParam;

        PlayerController[] allPlayers;
#if UNITY_2023_1_OR_NEWER
        allPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
#else
        allPlayers = Object.FindObjectsOfType<PlayerController>();
#endif

        var lockedCount = 0;
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

            var otherTeam = otherPc.GetComponent<TeamMember>() ?? otherPc.GetComponentInChildren<TeamMember>() ?? otherPc.GetComponentInParent<TeamMember>();
            if (otherTeam == null || otherTeam.CurrentTeam == Team.None)
            {
                continue;
            }

            if (otherTeam.CurrentTeam == senderTeamMember.CurrentTeam)
            {
                continue;
            }

            var d = otherPc.transform.position - myPos;
            if (d.sqrMagnitude > rangeSqr)
            {
                continue;
            }

            var rpcTarget = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { otherNet.OwnerClientId }
                }
            };

                otherPc.ApplyItemUseLockClientRpc(durationParam, rpcTarget);
            lockedCount++;
        }

        if (debugLogs)
        {
            Debug.Log($"[EmpItem] Applied EMP lock to {lockedCount} enemy player(s). range={rangeParam} duration={durationParam}", this);
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
