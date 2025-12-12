using UnityEngine;
using System.Collections;

public class FloatItem : MonoBehaviour
{
    public float floatDuration = 2.0f; // Duration of the floating effect
    public float floatHeight = 1.0f;    // Height the player will float
    public float floatSpeed = 2.0f;      // Speed of the floating motion

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // Check if the object is tagged as Player
        {
            StartCoroutine(ApplyFloatingEffect(other.transform));
            // Destroy the item after collection
            Destroy(gameObject);
        }
    }

    private IEnumerator ApplyFloatingEffect(Transform playerTransform)
    {
        float originalY = playerTransform.position.y; // Store the original Y position
        float elapsed = 0f;

        // Allow floating without making Rigidbody kinematic
        while (elapsed < floatDuration)
        {
            float newY = originalY + Mathf.Sin(elapsed * floatSpeed) * floatHeight;
            playerTransform.position = new Vector3(playerTransform.position.x, newY, playerTransform.position.z);

            elapsed += Time.deltaTime; // Increment elapsed time
            yield return null; // Wait for the next frame
        }

        // Optionally set it back to original height
        playerTransform.position = new Vector3(playerTransform.position.x, originalY, playerTransform.position.z);
    }
}