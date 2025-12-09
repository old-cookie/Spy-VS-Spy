using System.Collections; // Necessary for IEnumerator
using UnityEngine;

public class TrapFloor : MonoBehaviour
{
    public float disappearDuration = 2.0f; // Time the trap remains for the player
    public GameObject trapEffect; // Effect to show upon disappearing (optional)

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object that entered the trigger is the player
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player is on the trap floor!");
            StartCoroutine(DisapearTrap());
        }
    }

    private IEnumerator DisapearTrap()
    {
        // Optionally instantiate a trap effect here
        if (trapEffect != null)
        {
            Instantiate(trapEffect, transform.position, Quaternion.identity);
        }

        // Wait for the specified duration
        yield return new WaitForSeconds(disappearDuration);

        // Disable the trap (could also destroy the game object)
        gameObject.SetActive(false);
        Debug.Log("Trap floor has disappeared!");
    }
}