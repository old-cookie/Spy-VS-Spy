using UnityEngine;

public class HammerMovement : MonoBehaviour
{
    public float amplitude = 0.2f; // Height of the bounce
    public float frequency = 2.0f;  // Speed of the oscillation

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position; // Store the initial position
    }

    private void Update()
    {
        // Calculate the new position based on a sine wave for smooth bouncing
        float newY = startPosition.y + Mathf.Sin(Time.time * frequency) * amplitude;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
}