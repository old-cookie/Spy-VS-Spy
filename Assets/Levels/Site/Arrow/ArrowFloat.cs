using UnityEngine;

/// <summary>
/// Adds a gentle vertical sine-wave bob to the arrow prefab.
/// </summary>
public class ArrowFloat : MonoBehaviour
{
    /// <summary>Peak displacement in world units from the starting height.</summary>
    [SerializeField] private float amplitude = 0.25f;

    /// <summary>Oscillation speed multiplier.</summary>
    [SerializeField] private float speed = 1.2f;

    private Vector3 _startPosition;
    private float _phaseOffset;

    private void Start()
    {
        _startPosition = transform.position;
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f); // desync if multiple instances
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * speed + _phaseOffset) * amplitude;
        transform.position = _startPosition + Vector3.up * offset;
    }
}
