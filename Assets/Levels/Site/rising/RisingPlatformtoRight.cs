using UnityEngine;
using System.Collections;

public class RisingPlatformtoRight : MonoBehaviour
{
    public float jumpForce = 10.0f; // Upward force
    public float sideForce = 5.0f;   // Force to apply to the right
    public float riseDuration = 1.0f; // Duration of the jump effect
    private bool isRising = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") && !isRising)
        {
            StartCoroutine(RisePlayer(collision.rigidbody));
        }
    }

    private IEnumerator RisePlayer(Rigidbody playerRigidbody)
    {
        isRising = true;

        // Apply upward force
        playerRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        // Apply rightward force
        playerRigidbody.AddForce(Vector3.right * sideForce, ForceMode.Impulse);

        // Optional: You can add a small delay before resetting isRising
        yield return new WaitForSeconds(riseDuration);
        isRising = false;
    }
}