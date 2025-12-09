using System.Collections; // Necessary for IEnumerator
using UnityEngine;

public class DestroyAndRecreateFloor : MonoBehaviour
{
    public GameObject floorPrefab; // Prefab of the floor to be recreated
    public float disappearDuration = 2.0f; // Duration the floor is destroyed
    public float visibleDuration = 2.0f;   // Duration the floor is visible after recreating

    private void Start()
    {
        StartCoroutine(ManageFloorCycle());
    }

    private IEnumerator ManageFloorCycle()
    {
        while (true) // Loop forever
        {
            // Destroy the current floor
            Destroy(gameObject);
            yield return new WaitForSeconds(disappearDuration); // Wait for the disappear duration

            // Instantiate a new floor at the same position and rotation
            GameObject newFloor = Instantiate(floorPrefab, transform.position, transform.rotation);
            newFloor.transform.parent = transform.parent; // If you want to keep hierarchy

            yield return new WaitForSeconds(visibleDuration); // Wait for the visible duration
        }
    }
}