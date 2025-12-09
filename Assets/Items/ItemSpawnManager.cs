using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages networked item spawning from chests. Handles server-side spawning and syncs items to all clients.
/// </summary>
public class ItemSpawnManager : NetworkBehaviour
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static ItemSpawnManager Instance { get; private set; }

    /// <summary>
    /// List of possible item prefabs that can be spawned.
    /// </summary>
    [SerializeField]
    private List<GameObject> itemPrefabs;

    /// <summary>
    /// Height offset above the player where items follow.
    /// </summary>
    [SerializeField]
    private float itemFollowHeight = 3f;

    /// <summary>
    /// Duration for item to move from spawn position to player.
    /// </summary>
    [SerializeField]
    private float itemMoveDuration = 0.75f;

    /// <summary>
    /// Smoothing speed for item following player.
    /// </summary>
    [SerializeField]
    private float itemFollowSmoothSpeed = 8f;

    /// <summary>
    /// Tracks active items and their follow coroutines.
    /// </summary>
    private readonly Dictionary<ulong, Coroutine> activeItemCoroutines = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Stop all coroutines when despawning
        foreach (var coroutine in activeItemCoroutines.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        activeItemCoroutines.Clear();
    }

    /// <summary>
    /// Requests the server to spawn an item for a player from a chest position.
    /// </summary>
    /// <param name="playerNetworkObjectId">The network object ID of the requesting player.</param>
    /// <param name="chestPosition">The position of the chest.</param>
    /// <param name="itemPrefabIndex">Optional specific item index, -1 for random.</param>
    public void RequestSpawnItem(ulong playerNetworkObjectId, Vector3 chestPosition, int itemPrefabIndex = -1)
    {
        if (IsServer)
        {
            SpawnItemForPlayer(playerNetworkObjectId, chestPosition, itemPrefabIndex);
        }
        else
        {
            RequestSpawnItemServerRpc(playerNetworkObjectId, chestPosition, itemPrefabIndex);
        }
    }

    /// <summary>
    /// Server RPC to request item spawn from a client.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnItemServerRpc(ulong playerNetworkObjectId, Vector3 chestPosition, int itemPrefabIndex)
    {
        SpawnItemForPlayer(playerNetworkObjectId, chestPosition, itemPrefabIndex);
    }

    /// <summary>
    /// Spawns an item for a specific player (server only).
    /// </summary>
    private void SpawnItemForPlayer(ulong playerNetworkObjectId, Vector3 chestPosition, int itemPrefabIndex)
    {
        if (!IsServer)
        {
            return;
        }

        if (itemPrefabs == null || itemPrefabs.Count == 0)
        {
            Debug.LogWarning("[ItemSpawnManager] No item prefabs assigned.");
            return;
        }

        // Get player transform
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out var playerNetworkObject))
        {
            Debug.LogWarning($"[ItemSpawnManager] Player with NetworkObjectId {playerNetworkObjectId} not found.");
            return;
        }

        var playerTransform = playerNetworkObject.transform;

        // Select random prefab if not specified
        if (itemPrefabIndex < 0 || itemPrefabIndex >= itemPrefabs.Count)
        {
            itemPrefabIndex = Random.Range(0, itemPrefabs.Count);
        }

        var selectedPrefab = itemPrefabs[itemPrefabIndex];
        if (selectedPrefab == null)
        {
            Debug.LogWarning("[ItemSpawnManager] Selected prefab is null.");
            return;
        }

        // Spawn item at chest position
        var spawnRotation = Quaternion.Euler(-45f, 0f, 0f);
        var itemObject = Instantiate(selectedPrefab, chestPosition, spawnRotation);
        itemObject.name = $"Item_{playerNetworkObjectId}";

        var itemNetworkObject = itemObject.GetComponent<NetworkObject>();
        if (itemNetworkObject == null)
        {
            Debug.LogWarning("[ItemSpawnManager] Item prefab missing NetworkObject component.");
            Destroy(itemObject);
            return;
        }

        // Spawn on network
        itemNetworkObject.Spawn(true);

        // Notify the owning client about their item
        NotifyItemSpawnedClientRpc(itemNetworkObject.NetworkObjectId, playerNetworkObjectId);

        // Start following coroutine on server
        var coroutine = StartCoroutine(ItemFollowRoutine(itemObject.transform, playerTransform, playerNetworkObjectId));
        activeItemCoroutines[itemNetworkObject.NetworkObjectId] = coroutine;
    }

    /// <summary>
    /// Notifies clients that an item was spawned for a specific player.
    /// </summary>
    [ClientRpc]
    private void NotifyItemSpawnedClientRpc(ulong itemNetworkObjectId, ulong ownerPlayerNetworkObjectId)
    {
        // Only the owning player registers the item
        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer == null || localPlayer.NetworkObjectId != ownerPlayerNetworkObjectId)
        {
            return;
        }

        StartCoroutine(RegisterItemToPlayer(itemNetworkObjectId));
    }

    /// <summary>
    /// Waits for the item to be available and registers it to the local player.
    /// </summary>
    private IEnumerator RegisterItemToPlayer(ulong itemNetworkObjectId)
    {
        // Wait a frame for the item to be spawned on client
        yield return null;

        // Try to find the spawned item
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkObjectId, out var itemNetworkObject))
        {
            Debug.LogWarning($"[ItemSpawnManager] Item {itemNetworkObjectId} not found on client.");
            yield break;
        }

        var item = itemNetworkObject.GetComponent<Item>();
        if (item == null)
        {
            Debug.LogWarning("[ItemSpawnManager] Spawned object has no Item component.");
            yield break;
        }

        // Register to local player
        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer != null)
        {
            var playerController = localPlayer.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.RegisterHeldItemFromNetwork(item);
            }
        }
    }

    /// <summary>
    /// Coroutine that makes the item follow a player.
    /// </summary>
    private IEnumerator ItemFollowRoutine(Transform itemTransform, Transform playerTransform, ulong playerNetworkObjectId)
    {
        if (itemTransform == null || playerTransform == null)
        {
            yield break;
        }

        var startPosition = itemTransform.position;
        var targetPosition = GetPlayerAnchor(playerTransform);

        // Move item from chest to player
        if (itemMoveDuration > 0f)
        {
            var elapsed = 0f;
            while (elapsed < itemMoveDuration && itemTransform != null && playerTransform != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / itemMoveDuration);
                itemTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
                if (playerTransform != null)
                {
                    targetPosition = GetPlayerAnchor(playerTransform);
                }
            }
        }

        if (itemTransform != null)
        {
            itemTransform.position = targetPosition;
        }

        // Continuously follow player
        while (itemTransform != null && playerTransform != null)
        {
            var anchor = GetPlayerAnchor(playerTransform);
            if (itemFollowSmoothSpeed <= 0f)
            {
                itemTransform.position = anchor;
            }
            else
            {
                var followT = Mathf.Clamp01(itemFollowSmoothSpeed * Time.deltaTime);
                itemTransform.position = Vector3.Lerp(itemTransform.position, anchor, followT);
            }
            yield return null;
        }
    }

    /// <summary>
    /// Calculates the anchor position above a player.
    /// </summary>
    private Vector3 GetPlayerAnchor(Transform playerTransform)
    {
        if (playerTransform.TryGetComponent(out Collider playerCollider))
        {
            return playerCollider.bounds.center + Vector3.up * itemFollowHeight;
        }
        return playerTransform.position + Vector3.up * itemFollowHeight;
    }

    /// <summary>
    /// Stops the follow coroutine for an item (called when item is consumed/discarded).
    /// </summary>
    public void StopItemFollow(ulong itemNetworkObjectId)
    {
        if (activeItemCoroutines.TryGetValue(itemNetworkObjectId, out var coroutine))
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
            activeItemCoroutines.Remove(itemNetworkObjectId);
        }
    }
}
