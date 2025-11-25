using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ChestController : NetworkBehaviour
{
    [SerializeField]
    private List<GameObject> payloadPrefabs;

    [SerializeField]
    private float payloadFollowHeight = 3f;

    [SerializeField, Min(0f)]
    private float payloadMoveDuration = 0.75f;

    [SerializeField, Min(0f)]
    private float payloadFollowSmoothSpeed = 8f;

    private Coroutine payloadRoutine;
    private Item activeItem;

    public Item HandlePickStarted(Transform pickerTransform)
    {
        if (pickerTransform == null)
        {
            return null;
        }

        if (!IsServer)
        {
            return null;
        }

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

    private int GetRandomPayloadIndex()
    {
        if (payloadPrefabs == null || payloadPrefabs.Count == 0)
        {
            return -1;
        }
        return Random.Range(0, payloadPrefabs.Count);
    }

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

    private Vector3 GetChestCenter()
    {
        if (TryGetComponent(out Collider chestCollider))
        {
            return chestCollider.bounds.center;
        }

        return transform.position;
    }

    private IEnumerator MoveAndFollowPayloadRoutine(Transform payloadTransform, Transform pickerTransform)
    {
        if (payloadTransform == null || pickerTransform == null)
        {
            payloadRoutine = null;
            yield break;
        }

        var startPosition = payloadTransform.position;
        var targetPosition = GetPlayerAnchor(pickerTransform);

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

    private Vector3 GetPlayerAnchor(Transform pickerTransform)
    {
        if (pickerTransform.TryGetComponent(out Collider pickerCollider))
        {
            return pickerCollider.bounds.center + Vector3.up * payloadFollowHeight;
        }

        return pickerTransform.position + Vector3.up * payloadFollowHeight;
    }
}
