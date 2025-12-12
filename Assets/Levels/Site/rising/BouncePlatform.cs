using UnityEngine;
using System.Collections;

public class BouncePlatform : MonoBehaviour
{
    public float sideForce = 5.0f;   // Force to apply to the right
    private bool isBouncing = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") && !isBouncing)
        {
            StartCoroutine(BouncePlayer(collision.rigidbody));
        }
    }

    private IEnumerator BouncePlayer(Rigidbody playerRigidbody)
    {
        isBouncing = true;

        // Apply a rightward force only
        playerRigidbody.AddForce(Vector3.right * sideForce, ForceMode.Impulse);

        // Optional: You can reset isBouncing immediately
        yield return new WaitForSeconds(0.1f); // Short wait to avoid multiple bounces
        isBouncing = false;
    }
}