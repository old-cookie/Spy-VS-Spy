using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Fake Chest item: when consumed, places a fake chest trap that looks like a real chest.
/// When a player picks it (press E), it applies a short stun/slow and despawns.
/// </summary>
public class FakeChestItem : Item
{
    public override string ItemType => "fake chest";

    [Header("Fake Chest Prefab")]
    [SerializeField]
    private GameObject fakeChestPrefab;

    [Header("Placement")]
    [SerializeField]
    private float forwardOffset = 0.5f;

    [SerializeField]
    private float heightOffset = 0f;

    [SerializeField]
    private float zOffset = 0.05f;

    [SerializeField, Min(0.1f)]
    private float floorSnapRayStartHeight = 3f;

    [SerializeField, Min(0.1f)]
    private float floorSnapMaxDistance = 10f;

    [SerializeField]
    private float floorSnapUpOffset = 0.02f;

    public override void Consume()
    {
        PlaceFakeChestServerRpc();
        NotifyOwnerConsumed();
        DespawnItem();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaceFakeChestServerRpc()
    {
        if (fakeChestPrefab == null)
        {
            Debug.LogWarning("[FakeChestItem] fakeChestPrefab is not assigned!");
            return;
        }

        var playerTransform = transform.parent;
        if (playerTransform == null)
        {
            playerTransform = transform;
        }

        var forward = playerTransform.right;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.right;
        }
        forward = forward.normalized;

        var desiredPos = playerTransform.position + forward * forwardOffset + Vector3.up * heightOffset;
        desiredPos.z += zOffset;
        var pos = SnapToNearestFloor(desiredPos);

        // Rotate 180 degrees horizontally (Y axis) to face the opposite direction.
        var rot = Quaternion.Euler(0f, playerTransform.eulerAngles.y + 0f, 0f);
        var go = Instantiate(fakeChestPrefab, pos, rot);

        var netObj = go.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
            Debug.Log($"[FakeChestItem] Placed fake chest at {pos}");
        }
        else
        {
            Debug.LogWarning("[FakeChestItem] fakeChestPrefab has no NetworkObject!");
        }
    }

    private Vector3 SnapToNearestFloor(Vector3 desiredPosition)
    {
        // Raycast down to find the nearest floor surface under the desired position.
        // Prefer colliders tagged "Floor" but fall back to any collider.
        var rayStart = desiredPosition + Vector3.up * floorSnapRayStartHeight;
        var ray = new Ray(rayStart, Vector3.down);

        var hits = Physics.RaycastAll(ray, floorSnapMaxDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return desiredPosition;
        }

        RaycastHit? bestFloorHit = null;
        RaycastHit? bestAnyHit = null;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            // Closest overall hit
            if (bestAnyHit == null || h.distance < bestAnyHit.Value.distance)
            {
                bestAnyHit = h;
            }

            // Closest hit tagged Floor
            if (h.collider.CompareTag("Floor"))
            {
                if (bestFloorHit == null || h.distance < bestFloorHit.Value.distance)
                {
                    bestFloorHit = h;
                }
            }
        }

        var chosen = bestFloorHit ?? bestAnyHit;
        if (chosen == null)
        {
            return desiredPosition;
        }

        var p = chosen.Value.point;
        p.y += floorSnapUpOffset;
        return p;
    }
}
