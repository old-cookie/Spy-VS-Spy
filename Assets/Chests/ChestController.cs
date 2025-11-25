using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Controls chest behavior including spawning and managing payload items when a player interacts with the chest.
/// </summary>
public class ChestController : NetworkBehaviour
{
    /// <summary>
    /// List of possible payload prefabs that can be spawned from this chest.
    /// </summary>
    [SerializeField]
    private List<GameObject> payloadPrefabs;

    /// <summary>
    /// Height offset above the player where the payload will follow.
    /// </summary>
    [SerializeField]
    private float payloadFollowHeight = 3f;

    /// <summary>
    /// Duration in seconds for the payload to move from chest to player.
    /// </summary>
    [SerializeField, Min(0f)]
    private float payloadMoveDuration = 0.75f;

    /// <summary>
    /// Smoothing speed for payload following the player.
    /// </summary>
    [SerializeField, Min(0f)]
    private float payloadFollowSmoothSpeed = 8f;

    private Coroutine payloadRoutine;
    private Item activeItem;

    /// <summary>
    /// Handles the pick action initiated by a player. Spawns a random payload and starts following the player.
    /// </summary>
    /// <param name="pickerTransform">The transform of the player picking the chest.</param>
    /// <returns>The spawned Item component, or null if spawning failed.</returns>
    public Item HandlePickStarted(Transform pickerTransform)
    {
        if (pickerTransform == null)
        {
            return null;
        }

        var networkObject = pickerTransform.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            return null;
        }

        if (IsHost)
        {
            return SpawnAndSetupItem(pickerTransform, networkObject.OwnerClientId);
        }
        else
        {
            RequestPickItemServerRpc(networkObject.OwnerClientId);
            return null;
        }
    }

    /// <summary>
    /// Server RPC to request item spawning from a non-host client.
    /// </summary>
    /// <param name="requestingClientId">The client ID of the player requesting the item.</param>
    [ServerRpc(RequireOwnership = false)]
    private void RequestPickItemServerRpc(ulong requestingClientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(requestingClientId))
        {
            return;
        }

        var playerObject = NetworkManager.Singleton.ConnectedClients[requestingClientId].PlayerObject;
        if (playerObject == null)
        {
            return;
        }

        var pickerTransform = playerObject.transform;
        var item = SpawnAndSetupItem(pickerTransform, requestingClientId);

        if (item != null)
        {
            var itemNetworkObject = item.GetComponent<NetworkObject>();
            if (itemNetworkObject != null)
            {
                NotifyItemSpawnedClientRpc(itemNetworkObject.NetworkObjectId, requestingClientId);
            }
        }
    }

    /// <summary>
    /// Client RPC to notify a specific client that their item has been spawned.
    /// </summary>
    /// <param name="itemNetworkObjectId">The network object ID of the spawned item.</param>
    /// <param name="targetClientId">The client ID that should register this item.</param>
    [ClientRpc]
    private void NotifyItemSpawnedClientRpc(ulong itemNetworkObjectId, ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
        {
            return;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkObjectId, out var networkObject))
        {
            var item = networkObject.GetComponent<Item>();
            if (item != null)
            {
                var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localPlayer != null)
                {
                    var playerController = localPlayer.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        playerController.RegisterHeldItemFromNetwork(item);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Spawns and sets up an item for the specified player.
    /// </summary>
    /// <param name="pickerTransform">The transform of the player picking the chest.</param>
    /// <param name="clientId">The client ID of the player.</param>
    /// <returns>The spawned Item component, or null if spawning failed.</returns>
    private Item SpawnAndSetupItem(Transform pickerTransform, ulong clientId)
    {
        var randomIndex = GetRandomPayloadIndex();
        if (randomIndex < 0)
        {
            return null;
        }

        var payload = CreateAndSpawnPayload(randomIndex);
        if (payload == null)
        {
            return null;
        }

        var itemComponent = payload.GetComponent<Item>();
        if (itemComponent == null)
        {
            itemComponent = payload.AddComponent<Item>();
        }
        itemComponent.Initialize(this);

        if (payloadRoutine != null)
        {
            StopCoroutine(payloadRoutine);
            payloadRoutine = null;
        }

        activeItem = itemComponent;
        payloadRoutine = StartCoroutine(MoveAndFollowPayloadRoutine(payload.transform, pickerTransform));
        return itemComponent;
    }

    /// <summary>
    /// Gets a random index from the payload prefabs list.
    /// </summary>
    /// <returns>A random valid index, or -1 if no prefabs are available.</returns>
    private int GetRandomPayloadIndex()
    {
        if (payloadPrefabs == null || payloadPrefabs.Count == 0)
        {
            return -1;
        }
        return Random.Range(0, payloadPrefabs.Count);
    }

    /// <summary>
    /// Creates and spawns a payload from the specified prefab index.
    /// </summary>
    /// <param name="prefabIndex">Index of the prefab to instantiate.</param>
    /// <returns>The instantiated payload GameObject, or null if creation failed.</returns>
    private GameObject CreateAndSpawnPayload(int prefabIndex)
    {
        var payloadPosition = GetChestCenter();
        var payloadRotation = Quaternion.Euler(-45f, 0f, 0f);

        if (prefabIndex < 0 || prefabIndex >= payloadPrefabs.Count)
        {
            return null;
        }

        var selectedPrefab = payloadPrefabs[prefabIndex];
        if (selectedPrefab == null)
        {
            return null;
        }

        var payload = Instantiate(selectedPrefab, payloadPosition, payloadRotation);
        payload.name = $"ChestPayload_{name}";

        var networkObject = payload.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
        }

        return payload;
    }

    /// <summary>
    /// Gets the center position of the chest using its collider bounds.
    /// </summary>
    /// <returns>The center position of the chest.</returns>
    private Vector3 GetChestCenter()
    {
        if (TryGetComponent(out Collider chestCollider))
        {
            return chestCollider.bounds.center;
        }

        return transform.position;
    }

    /// <summary>
    /// Coroutine that moves the payload from the chest to the player, then continuously follows the player.
    /// </summary>
    /// <param name="payloadTransform">Transform of the spawned payload.</param>
    /// <param name="pickerTransform">Transform of the player to follow.</param>
    private IEnumerator MoveAndFollowPayloadRoutine(Transform payloadTransform, Transform pickerTransform)
    {
        if (payloadTransform == null || pickerTransform == null)
        {
            payloadRoutine = null;
            yield break;
        }

        var startPosition = payloadTransform.position;
        var targetPosition = GetPlayerAnchor(pickerTransform);

        // Move payload from chest to player over duration
        if (payloadMoveDuration > 0f)
        {
            var elapsed = 0f;

            while (elapsed < payloadMoveDuration && payloadTransform != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / payloadMoveDuration);
                payloadTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
                targetPosition = GetPlayerAnchor(pickerTransform);
            }
        }

        if (payloadTransform != null)
        {
            payloadTransform.position = targetPosition;
        }

        // Continuously follow the player
        while (payloadTransform != null && pickerTransform != null)
        {
            var anchor = GetPlayerAnchor(pickerTransform);
            if (payloadFollowSmoothSpeed <= 0f)
            {
                payloadTransform.position = anchor;
            }
            else
            {
                var followT = Mathf.Clamp01(payloadFollowSmoothSpeed * Time.deltaTime);
                payloadTransform.position = Vector3.Lerp(payloadTransform.position, anchor, followT);
            }

            yield return null;
        }

        payloadRoutine = null;
        activeItem = null;
    }

    /// <summary>
    /// Notifies the chest that an item has been consumed. Stops the follow routine if it matches the active item.
    /// </summary>
    /// <param name="consumedItem">The item that was consumed.</param>
    public void NotifyItemConsumed(Item consumedItem)
    {
        if (consumedItem != activeItem)
        {
            return;
        }

        activeItem = null;

        if (payloadRoutine != null)
        {
            StopCoroutine(payloadRoutine);
            payloadRoutine = null;
        }
    }

    /// <summary>
    /// Calculates the anchor position above the player where the payload should follow.
    /// </summary>
    /// <param name="pickerTransform">Transform of the player.</param>
    /// <returns>The anchor position above the player.</returns>
    private Vector3 GetPlayerAnchor(Transform pickerTransform)
    {
        if (pickerTransform.TryGetComponent(out Collider pickerCollider))
        {
            return pickerCollider.bounds.center + Vector3.up * payloadFollowHeight;
        }

        return pickerTransform.position + Vector3.up * payloadFollowHeight;
    }
}
