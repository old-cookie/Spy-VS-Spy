using System.Collections;
using UnityEngine;

public class ChestController : MonoBehaviour
{
    [SerializeField]
    private GameObject payloadPrefab;

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

        var payload = CreatePayload();
        if (payload == null)
        {
            return null;
        }

        var itemComponent = payload.GetComponent<Item>() ?? payload.AddComponent<Item>();
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

    private GameObject CreatePayload()
    {
        var payloadPosition = GetChestCenter();
        GameObject payload;

        var payloadRotation = Quaternion.Euler(-45f, 0f, 0f);

        if (payloadPrefab != null)
        {
            payload = Instantiate(payloadPrefab, payloadPosition, payloadRotation);
        }
        else
        {
            payload = GameObject.CreatePrimitive(PrimitiveType.Cube);
            payload.transform.rotation = payloadRotation;
        }

        payload.name = $"ChestPayload_{name}";
        payload.transform.position = payloadPosition;

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
