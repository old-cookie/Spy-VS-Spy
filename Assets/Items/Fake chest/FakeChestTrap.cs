using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Networked fake chest trap. Must be placed in the scene with a NetworkObject.
/// Players can interact with it via the same chest system (PlayerController looks for ChestController).
/// This script provides a ChestController-compatible HandlePickStarted method via composition:
/// add BOTH ChestController (for trigger detection) and this component, then PlayerController will be updated to detect this.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class FakeChestTrap : NetworkBehaviour
{
    [Header("Debuff")]
    [SerializeField, Min(0.1f)]
    private float slowDuration = 1.5f;

    [SerializeField, Range(0f, 1f)]
    private float slowMultiplier = 0.1f; // 0 = fully stunned, 1 = no slow

    [Header("Anti-abuse")]
    [SerializeField, Min(0f)]
    private float maxPickDistance = 2.5f;

    [Header("VFX")]
    [SerializeField]
    private GameObject triggerVfxPrefab;

    [SerializeField, Min(0f)]
    private float triggerVfxDestroyAfterSeconds = 3f;

    [Header("Lifetime")]
    [SerializeField, Min(0f)]
    private float despawnAfterSeconds = 0f; // 0 = never despawn unless picked

    private bool triggered;
    private float spawnTime;

    private void Awake()
    {
        spawnTime = Time.time;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        spawnTime = Time.time;
        triggered = false;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (despawnAfterSeconds > 0f && Time.time - spawnTime > despawnAfterSeconds)
        {
            DespawnSelf();
        }
    }

    /// <summary>
    /// Called by PlayerController when the local player presses E while in chest trigger.
    /// </summary>
    public void HandlePickStarted(Transform pickerTransform)
    {
        if (pickerTransform == null) return;

        if (!pickerTransform.TryGetComponent<NetworkObject>(out var pickerNet))
        {
            return;
        }

        if (!pickerNet.IsLocalPlayer)
        {
            return;
        }

        PickFakeChestServerRpc(pickerNet.NetworkObjectId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickFakeChestServerRpc(ulong pickerNetworkObjectId)
    {
        if (triggered) return;
        triggered = true;

        if (NetworkManager.Singleton == null)
        {
            triggered = false;
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(pickerNetworkObjectId, out var pickerObj))
        {
            triggered = false;
            return;
        }

        var pickerPc = pickerObj.GetComponent<PlayerController>();
        if (pickerPc == null)
        {
            triggered = false;
            return;
        }

        var pickerNet = pickerObj.GetComponent<NetworkObject>();
        if (pickerNet == null)
        {
            triggered = false;
            return;
        }

        if (maxPickDistance > 0f)
        {
            var d = pickerPc.transform.position - transform.position;
            if (d.sqrMagnitude > maxPickDistance * maxPickDistance)
            {
                triggered = false;
                return;
            }
        }

        var handler = pickerPc.GetComponent<ItemEffectHandler>();
        if (handler != null)
        {
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { pickerNet.OwnerClientId }
                }
            };

            handler.ApplyForcedSlowClientRpc(slowMultiplier, slowDuration, rpcParams);
            Debug.Log($"[FakeChestTrap] Applied slow to picker {pickerPc.name} (Owner {pickerNet.OwnerClientId})");
        }

        // Play VFX on all clients at the trap position
        if (triggerVfxPrefab != null)
        {
            PlayTriggerVfxClientRpc(transform.position);
        }

        DespawnSelf();
    }

    [ClientRpc]
    private void PlayTriggerVfxClientRpc(Vector3 worldPosition)
    {
        if (triggerVfxPrefab == null)
        {
            return;
        }

        var go = Instantiate(triggerVfxPrefab, worldPosition, Quaternion.identity);
        if (go == null)
        {
            return;
        }

        // If it has a ParticleSystem, destroy after it finishes; otherwise use a fixed timeout.
        var ps = go.GetComponentInChildren<ParticleSystem>();
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
                lifetime = triggerVfxDestroyAfterSeconds;
            }
            Destroy(go, lifetime);
        }
        else if (triggerVfxDestroyAfterSeconds > 0f)
        {
            Destroy(go, triggerVfxDestroyAfterSeconds);
        }
    }

    private void DespawnSelf()
    {
        if (!IsServer) return;

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
