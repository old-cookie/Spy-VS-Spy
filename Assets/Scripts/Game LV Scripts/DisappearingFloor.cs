using System.Collections; // Necessary for IEnumerator
using UnityEngine;

public class DisappearingFloor : MonoBehaviour
{
    public float disappearDuration = 2.0f; // Duration the floor disappears
    private Renderer floorRenderer; // Reference to the floor's Renderer

    private void Start()
    {
        floorRenderer = GetComponent<Renderer>(); // Get the Renderer component
        StartCoroutine(ManageFloorVisibility());
    }

    private IEnumerator ManageFloorVisibility()
    {
        while (true) // Loop forever
        {
            // Show the floor
            floorRenderer.enabled = true; 
            yield return new WaitForSeconds(disappearDuration); // Wait for the disappear duration
            
            // Hide the floor
            floorRenderer.enabled = false; 
            yield return new WaitForSeconds(disappearDuration); // Wait before reappearing
        }
    }
}