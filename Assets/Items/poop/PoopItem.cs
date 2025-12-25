using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Poop item: when consumed, fires a projectile forward that blinds hit players.
/// </summary>
public class PoopItem : Item
{
    [Header("Projectile Settings")]
    [SerializeField]
    private GameObject poopProjectilePrefab;

    [Header("Spawn Offset")]
    [SerializeField]
    private float forwardSpawnOffset = 0.3f;

    [SerializeField, Range(0f, 1f)]
    private float spawnHeight01 = 0.55f; // 0=feet, 1=head (relative to collider bounds)

    [SerializeField]
    private float verticalSpawnOffset = -0.5f; // lower from the collider center

    [SerializeField, Min(0.1f)]
    private float shootSpeed = 12f;

    [SerializeField, Min(0.1f)]
    private float projectileLifetime = 5f;

    [SerializeField, Range(0.5f, 5f)]
    private float blindDuration = 2.5f;

    public override void Consume()
    {
        Debug.Log("[PoopItem] Consume called - firing poop projectile");
        FirePoopServerRpc(shootSpeed, projectileLifetime, blindDuration);
        NotifyOwnerConsumed();
        DespawnItem();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void FirePoopServerRpc(float speed, float lifetime, float blindTime)
    {
        Debug.Log($"[PoopItem] FirePoopServerRpc called on server. Prefab assigned: {poopProjectilePrefab != null}");
        
        if (poopProjectilePrefab == null)
        {
            Debug.LogError("[PoopItem] poopProjectilePrefab NOT ASSIGNED in Inspector!");
            return;
        }

        // Get the player controller to determine shoot direction
        var playerTransform = transform.parent;
        if (playerTransform == null)
        {
            Debug.LogWarning("[PoopItem] No parent, using self transform");
            playerTransform = transform;
        }

        Debug.Log($"[PoopItem] Player transform: {playerTransform.name}, position: {playerTransform.position}");

        var playerController = playerTransform.GetComponent<PlayerController>();
        Vector3 shootDirection = playerTransform.forward;
        
        // Use player's facing direction (right/left in 2D platformer)
        if (playerController != null)
        {
            // Assuming players face left (-1, 0, 0) or right (1, 0, 0) in your 2D setup
            shootDirection = playerTransform.right; // Use right vector for side-scrolling
            Debug.Log($"[PoopItem] Using player right vector: {shootDirection}");
        }
        else
        {
            Debug.LogWarning("[PoopItem] PlayerController not found, using transform.right");
            shootDirection = playerTransform.right;
        }

        // Spawn using collider bounds (independent of pivot), at a normalized body height.
        var basePos = playerTransform.position;
        var usedBounds = false;
        var col3d = playerTransform.GetComponentInChildren<Collider>();
        if (col3d != null)
        {
            var b = col3d.bounds;
            basePos = new Vector3(b.center.x, b.min.y + b.size.y * spawnHeight01, b.center.z);
            usedBounds = true;
        }
        else
        {
            var col2d = playerTransform.GetComponentInChildren<Collider2D>();
            if (col2d != null)
            {
                var b = col2d.bounds;
                basePos = new Vector3(b.center.x, b.min.y + b.size.y * spawnHeight01, b.center.z);
                usedBounds = true;
            }
        }

        if (!usedBounds)
        {
            // Fallback if no collider found
            basePos = playerTransform.position;
        }

        var origin = basePos + shootDirection * forwardSpawnOffset + Vector3.up * verticalSpawnOffset;
        var rotation = Quaternion.LookRotation(shootDirection, Vector3.up);
        
        Debug.Log($"[PoopItem] Instantiating at position: {origin}, rotation: {rotation.eulerAngles}");
        var go = Instantiate(poopProjectilePrefab, origin, rotation);
        Debug.Log($"[PoopItem] GameObject created: {go.name}");

        // Configure before spawning so OnNetworkSpawn can initialize with correct direction.
        var proj = go.GetComponent<PoopProjectile>();
        if (proj != null)
        {
            Debug.Log($"[PoopItem] Configuring projectile with speed={speed}, direction={shootDirection}");
            proj.Configure(shootDirection, speed, lifetime, blindTime, OwnerClientId);
        }
        else
        {
            Debug.LogError("[PoopItem] PoopProjectile component NOT FOUND on projectile prefab!");
        }

        var netObj = go.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log("[PoopItem] NetworkObject found, spawning on network...");
            netObj.Spawn();
            Debug.Log("[PoopItem] NetworkObject spawned successfully");
        }
        else
        {
            Debug.LogError("[PoopItem] NetworkObject component NOT FOUND on projectile prefab!");
        }

        Debug.Log($"[PoopItem] Poop projectile setup complete from {playerTransform.name}");
    }
}
