using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Bomb item that places an explosive on the ground when consumed.
/// </summary>
public class Bomb : Item
{
    /// <summary>
    /// Prefab for the placed bomb that explodes on contact.
    /// </summary>
    [SerializeField]
    private GameObject placedBombPrefab;

    /// <summary>
    /// Height offset above the ground to place the bomb.
    /// </summary>
    [SerializeField]
    private float placementHeightOffset = 1f;

    public override void Consume()
    {
        // Place the bomb at the player's position
        PlaceBombServerRpc();
        
        // Destroy the item after placing
        NotifyOwnerConsumed();
        DespawnItem();
    }

    /// <summary>
    /// Server RPC to place the bomb at the player's location.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaceBombServerRpc()
    {
        if (placedBombPrefab == null)
        {
            Debug.LogWarning("[Bomb] Placed bomb prefab is not assigned!");
            return;
        }

        // Get the player's position
        var playerTransform = transform.parent; // Parent should be the player
        if (playerTransform == null)
        {
            playerTransform = transform;
        }

        var bombPosition = playerTransform.position + Vector3.up * placementHeightOffset;
        
        // Instantiate the placed bomb on the server
        var placedBomb = Instantiate(placedBombPrefab, bombPosition, Quaternion.identity);
        
        // If the placed bomb has a NetworkObject, spawn it on the network
        var networkObject = placedBomb.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
            Debug.Log($"[Bomb] Placed bomb at {bombPosition}");
        }
        else
        {
            Debug.LogWarning("[Bomb] Placed bomb prefab has no NetworkObject component!");
        }
    }
}