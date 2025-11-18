using UnityEngine;

public sealed class CameraController : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector2 offset;
    [SerializeField] private float followSpeed = 10f;

    private float initialZ;

    private void Awake()
    {
        initialZ = transform.position.z;

        if (followTarget != null)
        {
            var delta = transform.position - followTarget.position;
            offset = (Vector2)delta;
        }
    }

    private void LateUpdate()
    {
        if (followTarget == null)
        {
            return;
        }

        var targetPosition = new Vector3(
            followTarget.position.x + offset.x,
            followTarget.position.y + offset.y,
            initialZ);

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }
}
