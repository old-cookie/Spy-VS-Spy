using UnityEngine;

public sealed class PoopVfxFollower : MonoBehaviour
{
    private Transform target;
    private Vector3 localOffset;
    private Quaternion localRotOffset;

    public void Init(Transform targetTransform, Vector3 offset, Quaternion rotOffset)
    {
        target = targetTransform;
        localOffset = offset;
        localRotOffset = rotOffset;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.SetPositionAndRotation(target.TransformPoint(localOffset), target.rotation * localRotOffset);
    }
}
