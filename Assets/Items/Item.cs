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
