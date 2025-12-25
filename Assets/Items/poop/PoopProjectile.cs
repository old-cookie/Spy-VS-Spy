using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
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
    private Rigidbody2D rb2d;
    private Vector3 shootDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb2d = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.useGravity = true; // fall to the ground
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        if (rb2d != null)
        {
            // fall to the ground in 2D
            if (rb2d.bodyType == RigidbodyType2D.Dynamic)
            {
                rb2d.gravityScale = Mathf.Max(0.01f, rb2d.gravityScale);
            }
            rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        lifeTimer = maxLifetime;
        if (rb != null) rb.useGravity = true;
        // Rigidbody2D gravity handled via gravityScale
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
        // We want it to move forward but also fall due to gravity.
        // So: initial velocity is forward only; gravity handles the fall.

        var dir3 = shootDirection != Vector3.zero ? shootDirection : transform.forward;
        dir3.y = 0f;
        if (dir3.sqrMagnitude < 0.0001f)
        {
            // If the prefab's forward is vertical, fall back to right.
            dir3 = transform.right;
            dir3.y = 0f;
        }
        dir3 = dir3.normalized;

        if (rb != null)
        {
            rb.linearVelocity = dir3 * speed;
            Debug.Log($"[PoopProjectile] Velocity set (3D) to {rb.linearVelocity} (gravity on)");
            return;
        }

        if (rb2d != null)
        {
            var dir2 = new Vector2(dir3.x, dir3.y);
            if (dir2.sqrMagnitude < 0.0001f) dir2 = Vector2.right;
            dir2 = dir2.normalized;
            rb2d.linearVelocity = dir2 * speed;
            Debug.Log($"[PoopProjectile] Velocity set (2D) to {rb2d.linearVelocity} (gravity on)");
        }
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

        HandleHit(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        HandleHit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer) return;

        HandleHit(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        HandleHit(other.gameObject);
    }

    private void HandleHit(GameObject hitObject)
    {
        if (hitObject == null) return;

        var player = hitObject.GetComponent<PlayerController>();
        if (player == null)
        {
            player = hitObject.GetComponentInParent<PlayerController>();
        }

        if (player != null)
        {
            var playerNet = player.GetComponent<NetworkObject>();
            if (playerNet != null)
            {
                Debug.Log($"[PoopProjectile] Hit player {player.name} (Owner: {playerNet.OwnerClientId})");
                
                // Blind only the hit player
                var hitRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { playerNet.OwnerClientId }
                    }
                };
                ApplyBlindClientRpc(playerNet.NetworkObjectId, blindDuration, hitRpcParams);
                
                Debug.Log($"[PoopProjectile] Blinded hit player ({playerNet.OwnerClientId})");
            }
            Despawn();
            return;
        }

        // Hit anything else: just despawn
        Debug.Log($"[PoopProjectile] Hit {hitObject.name}, despawning");
        Despawn();
    }

    [ClientRpc]
    private void ApplyBlindClientRpc(ulong playerNetworkObjectId, float duration, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[PoopProjectile] ApplyBlindClientRpc received. duration={duration}");
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
