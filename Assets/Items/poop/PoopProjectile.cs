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

    [Header("Hit VFX (Shows On Hit Player)")]
    [SerializeField]
    private GameObject hitVfxPrefab;

    [Header("Hit SFX (Shows On Hit Player)")]
    [SerializeField]
    private AudioClip hitSfxClip;

    [SerializeField]
    private AudioClip[] hitSfxClips;

    [SerializeField, Range(0f, 1f)]
    private float hitSfxVolume = 1f;

    [SerializeField]
    private bool debugLogs = false;

    [SerializeField]
    private bool avoidInheritingPlayerScale = true;

    [SerializeField]
    private Vector3 hitVfxLocalOffset = Vector3.zero;

    [SerializeField]
    private Vector3 hitVfxLocalEulerOffset = Vector3.zero;

    [SerializeField, Min(0f)]
    private float hitVfxDestroyAfterSeconds = 2f;

    [Header("Despawn")]
    [SerializeField, Min(0f)]
    private float despawnDelaySeconds = 0.1f;

    private ulong shooterClientId;
    private float lifeTimer;
    private Rigidbody rb;
    private Rigidbody2D rb2d;
    private Vector3 shootDirection;
    private bool hitProcessed;

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
        if (hitProcessed) return;

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
                hitProcessed = true;
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

                // Show hit VFX on the hit player for everyone.
                if (hitVfxPrefab != null)
                {
                    PlayHitVfxClientRpc(playerNet.NetworkObjectId, playerNet.OwnerClientId);
                }
                else if (debugLogs)
                {
                    Debug.LogWarning("[PoopProjectile] hitVfxPrefab is not assigned; skipping hit VFX.", this);
                }
            }
            DespawnWithDelay();
            return;
        }

        // Hit anything else: just despawn
        Debug.Log($"[PoopProjectile] Hit {hitObject.name}, despawning");
        hitProcessed = true;
        DespawnWithDelay();
    }

    [ClientRpc]
    private void ApplyBlindClientRpc(ulong playerNetworkObjectId, float duration, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[PoopProjectile] ApplyBlindClientRpc received. duration={duration}");
        PoopBlindEffect.Show(duration);
    }

    [ClientRpc]
    private void PlayHitVfxClientRpc(ulong targetPlayerNetworkObjectId, ulong targetOwnerClientId)
    {
        if (hitVfxPrefab == null || NetworkManager.Singleton == null)
        {
            return;
        }

        var targetTransform = ResolveTargetTransform(targetPlayerNetworkObjectId, targetOwnerClientId);
        if (targetTransform == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[PoopProjectile] Could not resolve target transform. netId={targetPlayerNetworkObjectId} ownerClientId={targetOwnerClientId}", this);
            }
            return;
        }

        var worldPos = targetTransform.TransformPoint(hitVfxLocalOffset);
        var worldRot = targetTransform.rotation * Quaternion.Euler(hitVfxLocalEulerOffset);

        VfxSfxUtils.PlaySequenceAtPoint(hitSfxClip, hitSfxClips, worldPos, hitSfxVolume);

        GameObject vfx;
        if (avoidInheritingPlayerScale)
        {
            vfx = Instantiate(hitVfxPrefab, worldPos, worldRot);
            var follower = vfx.GetComponent<PoopVfxFollower>();
            if (follower == null)
            {
                follower = vfx.AddComponent<PoopVfxFollower>();
            }
            follower.Init(targetTransform, hitVfxLocalOffset, Quaternion.Euler(hitVfxLocalEulerOffset));
        }
        else
        {
            vfx = Instantiate(hitVfxPrefab, worldPos, worldRot, targetTransform);
        }

        if (vfx == null)
        {
            return;
        }

        var ps = vfx.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            var lifetime = main.duration;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
            {
                lifetime += main.startLifetime.constant;
            }
            if (lifetime <= 0f)
            {
                lifetime = hitVfxDestroyAfterSeconds;
            }
            if (lifetime > 0f)
            {
                Destroy(vfx, lifetime);
            }
        }
        else if (hitVfxDestroyAfterSeconds > 0f)
        {
            Destroy(vfx, hitVfxDestroyAfterSeconds);
        }
    }

    private Transform ResolveTargetTransform(ulong networkObjectId, ulong ownerClientId)
    {
        // Primary: lookup by NetworkObjectId
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var obj) && obj != null)
        {
            return obj.transform;
        }

        // Fallback: if this is the local owner's player
        var local = NetworkManager.Singleton.LocalClient;
        if (local != null && local.ClientId == ownerClientId)
        {
            var po = local.PlayerObject;
            if (po != null)
            {
                return po.transform;
            }
        }

        // Fallback: search any spawned NetworkObject with matching OwnerClientId and a PlayerController
        foreach (var kv in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
        {
            var no = kv.Value;
            if (no == null) continue;
            if (no.OwnerClientId != ownerClientId) continue;
            if (no.GetComponent<PlayerController>() != null || no.GetComponentInParent<PlayerController>() != null)
            {
                return no.transform;
            }
        }

        return null;
    }

    private void DespawnWithDelay()
    {
        if (!IsServer)
        {
            return;
        }

        if (despawnDelaySeconds <= 0f)
        {
            Despawn();
            return;
        }

        Invoke(nameof(Despawn), despawnDelaySeconds);
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
