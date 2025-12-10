using UnityEngine;
using System.Collections;

public class RisingPlatform : MonoBehaviour
{
    public float jumpForce = 10.0f; // Amount of upward force to apply
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
        playerRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse); // Apply an upward force

        // Optional: You can add a small delay before resetting isRising
        yield return new WaitForSeconds(riseDuration);
        isRising = false;
    }
}