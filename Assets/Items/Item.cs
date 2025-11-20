using UnityEngine;

public class Item : MonoBehaviour
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
        Destroy(gameObject);
    }

    public virtual void Discard()
    {
        NotifyOwnerConsumed();
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
