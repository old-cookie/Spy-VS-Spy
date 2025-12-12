using UnityEngine;
using Unity.Netcode;

/// <summary>
/// The actual bomb placed on the ground that explodes on contact.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PlacedBomb : NetworkBehaviour
{
    /// <summary>
    /// Force applied to players hit by the explosion.
    /// </summary>
    [SerializeField]
    private float explosionForce = 50f;

    /// <summary>
    /// Radius within which players are affected by the explosion.
    /// </summary>
    [SerializeField]
    private float explosionRadius = 3f;

    /// <summary>
    /// Height of the explosion for upward force.
    /// </summary>
    [SerializeField]
    private float explosionUpforce = 20f;

    /// <summary>
    /// Time in seconds before the bomb becomes semi-transparent.
    /// </summary>
    [SerializeField]
    private float transparencyStartTime = 2f;

    /// <summary>
    /// Transparency level when semi-transparent (0-1, where 1 is opaque).
    /// </summary>
    [SerializeField, Range(0.1f, 1f)]
    private float semiTransparentAlpha = 0.3f;

    /// <summary>
    /// Time in seconds to fade out after becoming semi-transparent.
    /// </summary>
    [SerializeField]
    private float fadeOutDuration = 1f;

    private float deploymentTime;
    private bool hasExploded;
    private bool startedFadeOut;
    private Renderer bombRenderer;
    private Color originalColor;
    private Collider cachedCollider;

    private void Awake()
    {
        CacheRenderer();
        CacheCollider();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        deploymentTime = Time.time;
        hasExploded = false;
        startedFadeOut = false;
        CacheRenderer();
        CacheCollider();
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        float timeSincePlacement = Time.time - deploymentTime;

        // Check if it's time to make semi-transparent
        if (!startedFadeOut && timeSincePlacement >= transparencyStartTime)
        {
            MakeSemiTransparentClientRpc();
            startedFadeOut = true;
        }

        // Remove bomb after fade out is complete
        if (startedFadeOut && timeSincePlacement >= (transparencyStartTime + fadeOutDuration))
        {
            DespawnBomb();
        }
    }

    /// <summary>
    /// Caches the renderer component for alpha manipulation.
    /// </summary>
    private void CacheRenderer()
    {
        if (bombRenderer == null)
        {
            bombRenderer = GetComponent<Renderer>();
            if (bombRenderer != null)
            {
                originalColor = bombRenderer.material.color;
            }
        }
    }

    /// <summary>
    /// Caches the collider component for trigger toggling.
    /// </summary>
    private void CacheCollider()
    {
        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<Collider>();
        }
    }

    /// <summary>
    /// Called when another collider enters the trigger.
    /// </summary>
    private void OnTriggerEnter(Collider collision)
    {
        Debug.Log($"[PlacedBomb] OnTriggerEnter called with: {collision.name}, IsServer: {IsServer}, hasExploded: {hasExploded}");

        if (!IsServer || hasExploded)
        {
            Debug.Log($"[PlacedBomb] Blocked - IsServer: {IsServer}, hasExploded: {hasExploded}");
            return;
        }

        var playerController = collision.GetComponent<PlayerController>();
        if (playerController == null)
        {
            playerController = collision.GetComponentInParent<PlayerController>();
        }

        if (playerController == null)
        {
            // Landed on floor: disable trigger to rest on ground
            if (collision.CompareTag("Floor"))
            {
                CacheCollider();
                if (cachedCollider != null && cachedCollider.isTrigger)
                {
                    cachedCollider.isTrigger = false;
                    Debug.Log("[PlacedBomb] Landed on floor, collider trigger disabled.");
                }
            }
            Debug.Log($"[PlacedBomb] No PlayerController found in {collision.name}");
            return;
        }

        Debug.Log($"[PlacedBomb] PlayerController found: {playerController.name}");
        // Explode and blow away nearby players
        ExplodeBomb();
    }

    /// <summary>
    /// Handles collision when trigger is disabled (e.g., after landing on floor).
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || hasExploded)
        {
            return;
        }

        if (collision.gameObject.CompareTag("Floor"))
        {
            // Already non-trigger; nothing else needed
            return;
        }

        var playerController = collision.gameObject.GetComponent<PlayerController>();
        if (playerController == null)
        {
            playerController = collision.gameObject.GetComponentInParent<PlayerController>();
        }

        if (playerController == null)
        {
            return;
        }

        ExplodeBomb();
    }

    /// <summary>
    /// Handles the explosion logic when a player touches the bomb.
    /// </summary>
    private void ExplodeBomb()
    {
        if (hasExploded)
        {
            return;
        }

        hasExploded = true;
        Debug.Log($"[PlacedBomb] Bomb exploded! Applying force in radius {explosionRadius}");

        var hitCount = 0;
        var colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var col in colliders)
        {
            var playerController = col.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = col.GetComponentInParent<PlayerController>();
            }

            if (playerController == null)
            {
                continue;
            }

            var rigidbody = playerController.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                Debug.LogWarning($"[PlacedBomb] No Rigidbody on {playerController.name}");
                continue;
            }

            hitCount++;
            Debug.Log($"[PlacedBomb] Applying explosion to {playerController.name}. IsKinematic: {rigidbody.isKinematic}, Constraints: {rigidbody.constraints}");

            // Use built-in explosion force so the player is pushed outward naturally
            rigidbody.linearVelocity = Vector3.zero;  // Clear existing velocity first
            rigidbody.AddExplosionForce(explosionForce, transform.position, explosionRadius, explosionUpforce, ForceMode.Impulse);
            Debug.Log($"[PlacedBomb] ExplosionForce {explosionForce} (radius {explosionRadius}, up {explosionUpforce}) applied to {playerController.name}. New velocity: {rigidbody.linearVelocity}");
        }

        if (hitCount == 0)
        {
            Debug.Log("[PlacedBomb] Explosion hit no players.");
        }

        // Play explosion effect on all clients
        PlayExplosionEffectClientRpc();

        // Despawn the bomb
        DespawnBomb();
    }

    /// <summary>
    /// Client RPC to make the bomb semi-transparent on all clients.
    /// </summary>
    [ClientRpc]
    private void MakeSemiTransparentClientRpc()
    {
        CacheRenderer();
        if (bombRenderer != null)
        {
            var newColor = originalColor;
            newColor.a = semiTransparentAlpha;
            bombRenderer.material.color = newColor;
            Debug.Log("[PlacedBomb] Bomb became semi-transparent");
        }
    }

    /// <summary>
    /// Client RPC to play explosion effects on all clients.
    /// </summary>
    [ClientRpc]
    private void PlayExplosionEffectClientRpc()
    {
        // You can add visual/audio effects here (particle system, sound, etc.)
        Debug.Log("[PlacedBomb] Explosion effect played");
    }

    /// <summary>
    /// Despawns the bomb from the network.
    /// </summary>
    private void DespawnBomb()
    {
        if (IsServer)
        {
            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
        }
    }
}