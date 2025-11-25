using UnityEngine;
using Unity.Netcode;

public class Item : NetworkBehaviour
{
    private ChestController owner;

    [SerializeField]
    private string itemType = "item";

    public virtual string ItemType => itemType;

    public void Initialize(ChestController chestController)
    {
        owner = chestController;
    }

    public void Consume()
    {
        NotifyOwnerConsumed();
        if (IsServer)
        {
            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
        }
        Destroy(gameObject);
    }

    public virtual void Discard()
    {
        NotifyOwnerConsumed();
        if (IsServer)
        {
            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
        }
        Destroy(gameObject);
    }

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
