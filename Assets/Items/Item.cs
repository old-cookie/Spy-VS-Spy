using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Base class for items that can be picked up from chests and consumed by players.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class Item : NetworkBehaviour
{
    /// <summary>
    /// The chest controller that spawned this item.
    /// </summary>
    private ChestController owner;

    /// <summary>
    /// The type identifier for this item, used to determine effects when consumed.
    /// </summary>
    [SerializeField]
    private string itemType = "item";

    /// <summary>
    /// The player-facing description of what the item does.
    /// </summary>
    [SerializeField]
    private string itemDescription = string.Empty;

    [Header("Use VFX")]
    [SerializeField]
    private GameObject useVfxPrefab;

    [Header("Use SFX")]
    [SerializeField]
    private AudioClip useSfxClip;

    [SerializeField]
    private AudioClip[] useSfxClips;

    [SerializeField, Range(0f, 1f)]
    private float useSfxVolume = 1f;

    [SerializeField]
    private bool attachUseVfxToPlayer = true;

    [SerializeField]
    private Vector3 useVfxLocalOffset = Vector3.zero;

    [SerializeField]
    private bool matchUseVfxRotationToPlayer = true;

    [SerializeField]
    private Vector3 useVfxLocalEulerOffset = Vector3.zero;

    [SerializeField, Min(0f)]
    private float useVfxDestroyAfterSeconds = 2f;

    /// <summary>
    /// Gets the item type identifier.
    /// </summary>
    public virtual string ItemType => itemType;

    /// <summary>
    /// Gets the player-facing description for UI display.
    /// </summary>
    public virtual string ItemDescription => itemDescription;

    /// <summary>
    /// Initializes the item with a reference to its owning chest controller.
    /// </summary>
    /// <param name="chestController">The chest that spawned this item.</param>
    public void Initialize(ChestController chestController)
    {
        owner = chestController;
    }

    /// <summary>
    /// Consumes the item, notifying the owner and destroying the game object.
    /// </summary>
    public virtual void Consume()
    {
        NotifyOwnerConsumed();
        DespawnItem();
    }

    /// <summary>
    /// Discards the item without applying its effect. Called when picking up a new item while holding one.
    /// </summary>
    public virtual void Discard()
    {
        NotifyOwnerConsumed();
        DespawnItem();
    }

    /// <summary>
    /// Plays the optional "use" VFX on a player (all clients). Safe to call even if no VFX is assigned.
    /// Call this BEFORE Consume/Discard so the RPC arrives before despawn.
    /// </summary>
    public void PlayUseVfxForPlayer(ulong playerNetworkObjectId)
    {
        if (useVfxPrefab == null)
        {
            return;
        }

        if (IsServer)
        {
            PlayUseVfxClientRpc(playerNetworkObjectId);
        }
        else
        {
            RequestPlayUseVfxServerRpc(playerNetworkObjectId);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlayUseVfxServerRpc(ulong playerNetworkObjectId)
    {
        PlayUseVfxClientRpc(playerNetworkObjectId);
    }

    [ClientRpc]
    private void PlayUseVfxClientRpc(ulong playerNetworkObjectId)
    {
        if (useVfxPrefab == null)
        {
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out var playerObj))
        {
            return;
        }

        var parent = attachUseVfxToPlayer ? playerObj.transform : null;
        var worldPos = playerObj.transform.TransformPoint(useVfxLocalOffset);

        var baseRot = matchUseVfxRotationToPlayer ? playerObj.transform.rotation : Quaternion.identity;
        var worldRot = baseRot * Quaternion.Euler(useVfxLocalEulerOffset);

        var go = Instantiate(useVfxPrefab, worldPos, worldRot, parent);
        if (go == null)
        {
            return;
        }

        VfxSfxUtils.PlaySequenceAtPoint(useSfxClip, useSfxClips, worldPos, useSfxVolume);

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
                lifetime = useVfxDestroyAfterSeconds;
            }

            if (lifetime > 0f)
            {
                Destroy(go, lifetime);
            }
        }
        else if (useVfxDestroyAfterSeconds > 0f)
        {
            Destroy(go, useVfxDestroyAfterSeconds);
        }
    }

    /// <summary>
    /// Handles despawning the item properly for both host and non-host clients.
    /// </summary>
    protected void DespawnItem()
    {
        if (IsHost)
        {
            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
        }
        else
        {
            RequestDespawnRpc();
        }
    }

    /// <summary>
    /// Server RPC to request the host to despawn this item.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDespawnRpc()
    {
        var networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn();
        }
    }

    /// <summary>
    /// Notifies the owning chest that this item has been consumed or discarded.
    /// </summary>
    protected void NotifyOwnerConsumed()
    {
        if (owner == null)
        {
            return;
        }

        owner.NotifyItemConsumed(this);
        owner = null;
    }
}
