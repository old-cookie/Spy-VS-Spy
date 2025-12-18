using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PoopProjectile : NetworkBehaviour
{
    [SerializeField]
    private float speed = 10f;

    [SerializeField]
    private float maxLifetime = 5f;

    [SerializeField]
    private float blindDuration = 2.5f;

    private ulong shooterClientId;
    private float lifeTimer;
    private Rigidbody rb;
    private Vector3 shootDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false; // Disable gravity for straight shot
            rb.linearDamping = 0f; // No air resistance
            rb.angularDamping = 0f;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        lifeTimer = maxLifetime;
        if (rb != null)
        {
            rb.useGravity = false; // Ensure gravity is off
        }
        TryInitVelocity();
    }

    public void Configure(Vector3 direction, float setSpeed, float lifetime, float blindTime, ulong shooterId)
    {
        shootDirection = direction.normalized;
        speed = setSpeed;
        maxLifetime = lifetime;
        blindDuration = blindTime;
        shooterClientId = shooterId;
        TryInitVelocity();
    }

    private void TryInitVelocity()
    {
        if (rb == null) return;
        
        // Use horizontal direction only (remove vertical component for straight shot)
        var dir = shootDirection != Vector3.zero ? shootDirection : transform.forward;
        dir.y = 0f; // Force horizontal only
        dir = dir.normalized;
        
        rb.linearVelocity = dir * speed;
        Debug.Log($"[PoopProjectile] Velocity set to {rb.linearVelocity} (horizontal only, no gravity)");
    }

    private void Update()
    {
        if (!IsServer) return;
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Despawn();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        var player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null)
        {
            player = collision.gameObject.GetComponentInParent<PlayerController>();
        }

        if (player != null)
        {
            var playerNet = player.GetComponent<NetworkObject>();
            if (playerNet != null)
            {
                Debug.Log($"[PoopProjectile] Hit player {player.name} (Owner: {playerNet.OwnerClientId})");
                
                // Blind the hit player
                var hitRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { playerNet.OwnerClientId }
                    }
                };
                ApplyBlindClientRpc(playerNet.NetworkObjectId, blindDuration, hitRpcParams);

                // Also blind the shooter
                var shooterRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { shooterClientId }
                    }
                };
                ApplyBlindClientRpc(0, blindDuration, shooterRpcParams); // 0 = unused for shooter
                
                Debug.Log($"[PoopProjectile] Blinded both shooter ({shooterClientId}) and hit player ({playerNet.OwnerClientId})");
            }
            Despawn();
            return;
        }

        // Hit anything else: just despawn
        Debug.Log($"[PoopProjectile] Hit {collision.gameObject.name}, despawning");
        Despawn();
    }

    [ClientRpc]
    private void ApplyBlindClientRpc(ulong playerNetworkObjectId, float duration, ClientRpcParams rpcParams = default)
    {
        // Only the owning client will execute this
        PoopBlindEffect.Show(duration);
    }

    private void Despawn()
    {
        var net = GetComponent<NetworkObject>();
        if (net != null && net.IsSpawned)
        {
            net.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
