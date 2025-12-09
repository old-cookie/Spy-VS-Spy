using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Controls chest behavior. When a player interacts, requests ItemSpawnManager to spawn a networked item.
/// </summary>
public class ChestController : MonoBehaviour
{
    private Item activeItem;

    /// <summary>
    /// Handles the pick action initiated by a player. Requests networked item spawn.
    /// </summary>
    /// <param name="pickerTransform">The transform of the player picking the chest.</param>
    /// <returns>null - item will be registered via network callback.</returns>
    public Item HandlePickStarted(Transform pickerTransform)
    {
        if (pickerTransform == null)
        {
            return null;
        }

        if (!pickerTransform.TryGetComponent<NetworkObject>(out var playerNetworkObject))
        {
            return null;
        }

        // Only the local player should request item spawn
        if (!playerNetworkObject.IsLocalPlayer)
        {
            return null;
        }

        // Request ItemSpawnManager to spawn a networked item
        if (ItemSpawnManager.Instance != null)
        {
            var chestCenter = GetChestCenter();
            ItemSpawnManager.Instance.RequestSpawnItem(playerNetworkObject.NetworkObjectId, chestCenter);
        }
        else
        {
            Debug.LogWarning("[ChestController] ItemSpawnManager not found!");
        }

        // Item will be registered via network callback, return null here
        return null;
    }

    /// <summary>
    /// Gets the center position of the chest using its collider bounds.
    /// </summary>
    private Vector3 GetChestCenter()
    {
        if (TryGetComponent(out Collider chestCollider))
        {
            return chestCollider.bounds.center;
        }
        return transform.position;
    }

    /// <summary>
    /// Notifies the chest that an item has been consumed.
    /// </summary>
    /// <param name="consumedItem">The item that was consumed.</param>
    public void NotifyItemConsumed(Item consumedItem)
    {
        if (consumedItem != activeItem)
        {
            return;
        }
        activeItem = null;
    }
}
