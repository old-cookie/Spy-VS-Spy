using UnityEngine;
using System.Collections;

public class BouncePlatform : MonoBehaviour
{
    private static readonly WaitForSeconds _waitForSeconds0_1 = new(0.1f);
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
        yield return _waitForSeconds0_1; // Short wait to avoid multiple bounces
        isBouncing = false;
    }
}