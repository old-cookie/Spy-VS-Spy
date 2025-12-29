using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Plays a VFX on the player when they become affected by an item effect.
/// This is implemented as a separate component so you don't need to modify ItemEffectHandler.
///
/// How it works:
/// - On the local owning client, it watches ItemEffectHandler's public multipliers.
/// - When an effect starts (multiplier goes from 1 to != 1), it sends a Server RPC.
/// - The server broadcasts a Client RPC so all clients spawn the VFX on this player.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerEffectVfxBroadcaster : NetworkBehaviour
{
    [Header("Effect VFX")]
    [SerializeField]
    private GameObject receivedEffectVfxPrefab;

    [SerializeField]
    private GameObject slowEffectVfxPrefab;

    [SerializeField]
    private GameObject speedEffectVfxPrefab;

    [SerializeField]
    private GameObject jumpEffectVfxPrefab;

    [SerializeField]
    private bool attachToPlayer = true;

    [SerializeField]
    private bool avoidInheritingPlayerScale = true;

    [SerializeField]
    private Vector3 localOffset = Vector3.zero;

    [SerializeField]
    private bool matchVfxRotationToPlayer = true;

    [SerializeField]
    private Vector3 localEulerOffset = Vector3.zero;

    [SerializeField]
    private Vector3 slowLocalOffset = Vector3.zero;

    [SerializeField]
    private Vector3 slowLocalEulerOffset = Vector3.zero;

    [SerializeField]
    private Vector3 speedLocalOffset = Vector3.zero;

    [SerializeField]
    private Vector3 speedLocalEulerOffset = Vector3.zero;

    [SerializeField]
    private Vector3 jumpLocalOffset = Vector3.zero;

    [SerializeField]
    private Vector3 jumpLocalEulerOffset = Vector3.zero;

    [SerializeField, Min(0f)]
    private float destroyAfterSeconds = 2f;

    [Header("Effect SFX")]
    [SerializeField]
    private AudioClip effectSfxClip;

    [SerializeField]
    private AudioClip[] effectSfxClips;

    [SerializeField, Range(0f, 1f)]
    private float effectSfxVolume = 1f;

    [Header("Debug")]
    [SerializeField]
    private bool debugLogs = false;

    [Header("Anti-spam")]
    [SerializeField, Min(0f)]
    private float minSecondsBetweenTriggers = 0.15f;

    private ItemEffectHandler cachedHandler;
    private float lastSpeedMultiplier = 1f;
    private float lastJumpMultiplier = 1f;

    private float lastTriggerTime;

    private void Awake()
    {
        CacheHandler();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheHandler();

        lastSpeedMultiplier = 1f;
        lastJumpMultiplier = 1f;
        lastTriggerTime = -999f;
    }

    private void CacheHandler()
    {
        if (cachedHandler != null)
        {
            return;
        }

        cachedHandler = GetComponent<ItemEffectHandler>();
        if (cachedHandler == null)
        {
            cachedHandler = GetComponentInChildren<ItemEffectHandler>();
        }
        if (cachedHandler == null)
        {
            cachedHandler = GetComponentInParent<ItemEffectHandler>();
        }
    }

    private void Update()
    {
        // Only the local player's instance should detect + report effect starts.
        var netObj = GetComponent<NetworkObject>();
        var isLocalControlled = netObj != null && (netObj.IsOwner || netObj.IsLocalPlayer);
        if (!isLocalControlled)
        {
            return;
        }

        CacheHandler();
        if (cachedHandler == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[PlayerEffectVfxBroadcaster] No ItemEffectHandler found on {name} (or children/parent).", this);
            }
            return;
        }

        // Detect "effect started" when multiplier goes from 1 -> != 1.
        var currentSpeedMultiplier = cachedHandler.CurrentSpeedMultiplier;
        var currentJumpMultiplier = cachedHandler.CurrentJumpMultiplier;

        var speedStarted = !Mathf.Approximately(lastSpeedMultiplier, 1f) ? false : !Mathf.Approximately(currentSpeedMultiplier, 1f);
        var jumpStarted = !Mathf.Approximately(lastJumpMultiplier, 1f) ? false : !Mathf.Approximately(currentJumpMultiplier, 1f);

        if (speedStarted)
        {
            if (debugLogs)
            {
                Debug.Log($"[PlayerEffectVfxBroadcaster] Effect started on {name}. speed={currentSpeedMultiplier} jump={currentJumpMultiplier}", this);
            }

            var type = currentSpeedMultiplier < 1f ? EffectVfxType.Slow : EffectVfxType.Speed;
            TryTriggerVfx(type);
        }

        if (jumpStarted)
        {
            if (debugLogs)
            {
                Debug.Log($"[PlayerEffectVfxBroadcaster] Jump effect started on {name}. jump={currentJumpMultiplier}", this);
            }
            TryTriggerVfx(EffectVfxType.Jump);
        }

        lastSpeedMultiplier = currentSpeedMultiplier;
        lastJumpMultiplier = currentJumpMultiplier;
    }

    private enum EffectVfxType : byte
    {
        Generic = 0,
        Slow = 1,
        Speed = 2,
        Jump = 3,
    }

    private GameObject GetVfxPrefab(EffectVfxType type)
    {
        return type switch
        {
            EffectVfxType.Slow => slowEffectVfxPrefab != null ? slowEffectVfxPrefab : receivedEffectVfxPrefab,
            EffectVfxType.Speed => speedEffectVfxPrefab != null ? speedEffectVfxPrefab : receivedEffectVfxPrefab,
            EffectVfxType.Jump => jumpEffectVfxPrefab != null ? jumpEffectVfxPrefab : receivedEffectVfxPrefab,
            _ => receivedEffectVfxPrefab,
        };
    }

    private Vector3 GetVfxLocalOffset(EffectVfxType type)
    {
        return type switch
        {
            EffectVfxType.Slow => slowLocalOffset,
            EffectVfxType.Speed => speedLocalOffset,
            EffectVfxType.Jump => jumpLocalOffset,
            _ => localOffset,
        };
    }

    private Vector3 GetVfxLocalEulerOffset(EffectVfxType type)
    {
        return type switch
        {
            EffectVfxType.Slow => slowLocalEulerOffset,
            EffectVfxType.Speed => speedLocalEulerOffset,
            EffectVfxType.Jump => jumpLocalEulerOffset,
            _ => localEulerOffset,
        };
    }

    private void TryTriggerVfx(EffectVfxType type)
    {
        var prefab = GetVfxPrefab(type);
        if (prefab == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[PlayerEffectVfxBroadcaster] No VFX prefab assigned for {type} on {name}.", this);
            }
            return;
        }

        if (Time.time - lastTriggerTime < minSecondsBetweenTriggers)
        {
            return;
        }

        lastTriggerTime = Time.time;

        if (debugLogs)
        {
            Debug.Log($"[PlayerEffectVfxBroadcaster] Trigger VFX request. IsServer={IsServer}", this);
        }

        if (IsServer)
        {
            PlayEffectVfxClientRpc((byte)type);
        }
        else
        {
            TriggerEffectVfxServerRpc((byte)type);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TriggerEffectVfxServerRpc(byte effectType)
    {
        PlayEffectVfxClientRpc(effectType);
    }

    [ClientRpc]
    private void PlayEffectVfxClientRpc(byte effectType)
    {
        var prefab = GetVfxPrefab((EffectVfxType)effectType);
        if (prefab == null)
        {
            return;
        }

        if (debugLogs)
        {
            Debug.Log($"[PlayerEffectVfxBroadcaster] PlayEffectVfxClientRpc on {name}", this);
        }

        var parent = attachToPlayer ? transform : null;
        var type = (EffectVfxType)effectType;
        var offset = GetVfxLocalOffset(type);
        var euler = GetVfxLocalEulerOffset(type);

        var worldPos = transform.TransformPoint(offset);
        var baseRot = matchVfxRotationToPlayer ? transform.rotation : Quaternion.identity;
        var worldRot = baseRot * Quaternion.Euler(euler);

        VfxSfxUtils.PlaySequenceAtPoint(effectSfxClip, effectSfxClips, worldPos, effectSfxVolume);

        GameObject go;
        if (attachToPlayer && avoidInheritingPlayerScale)
        {
            // Some character rigs flip by using negative scale, which will mirror/flip child VFX.
            // To avoid that, spawn unparented and follow the player by script (position/rotation only).
            go = Instantiate(prefab, worldPos, worldRot);
            var follower = go.GetComponent<TransformFollower>();
            if (follower == null)
            {
                follower = go.AddComponent<TransformFollower>();
            }
            follower.SetTarget(transform, offset, Quaternion.Euler(euler), matchVfxRotationToPlayer);
        }
        else
        {
            go = Instantiate(prefab, worldPos, worldRot, parent);
        }
        if (go == null)
        {
            return;
        }

        var ps = go.GetComponentInChildren<ParticleSystem>();
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
                lifetime = destroyAfterSeconds;
            }

            if (lifetime > 0f)
            {
                Destroy(go, lifetime);
            }
        }
        else if (destroyAfterSeconds > 0f)
        {
            Destroy(go, destroyAfterSeconds);
        }
    }
}

internal sealed class TransformFollower : MonoBehaviour
{
    private Transform target;
    private Vector3 localOffset;
    private Quaternion localRotationOffset;
    private bool matchTargetRotation;

    public void SetTarget(Transform targetTransform, Vector3 offset, Quaternion rotationOffset, bool matchRotation)
    {
        target = targetTransform;
        localOffset = offset;
        localRotationOffset = rotationOffset;
        matchTargetRotation = matchRotation;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.TransformPoint(localOffset);

        if (matchTargetRotation)
        {
            transform.rotation = target.rotation * localRotationOffset;
        }
    }
}
