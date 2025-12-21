using UnityEngine;
using System.Collections; // Required for IEnumerator

public class MovingPlatform : MonoBehaviour
{
    public float riseHeight = 5.0f;      // How high the platform will rise
    public float riseSpeed = 2.0f;       // Speed of the platform
    public float holdTime = 3.0f;        // Time to hold at the top

    private bool isMoving = false;        // To prevent multiple rises
    private Vector3 startPosition;        // Starting position of the platform

    private void Start()
    {
        // Store the starting position
        startPosition = transform.position; 
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the player touches the platform
        if (other.CompareTag("Player") && !isMoving) 
        {
            StartCoroutine(RiseAndReturn());
        }
    }

    private IEnumerator RiseAndReturn()
    {
        isMoving = true; // Prevent further rises during movement
        Vector3 targetPosition = startPosition + new Vector3(0, riseHeight, 0);

        // Move the platform upward
        while (transform.position.y < targetPosition.y)
        {
            transform.position += Vector3.up * riseSpeed * Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // Hold at the top
        yield return new WaitForSeconds(holdTime);
        
        // Move the platform back down
        while (transform.position.y > startPosition.y)
        {
            transform.position -= Vector3.up * riseSpeed * Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // Ensure the platform is back to the original position
        transform.position = startPosition;
        isMoving = false; // Allow the platform to move again if needed
    }
}