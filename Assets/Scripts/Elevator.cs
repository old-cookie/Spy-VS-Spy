using System.Collections;
using UnityEngine;

public class AutomatedElevator : MonoBehaviour
{
    public int currentFloor = 0;
    public int maxFloor = 5;
    public int minFloor = 0;
    public float moveSpeed = 2.0f; // Speed in units per second
    public float waitTime = 2.0f;  // Time to wait at each floor

    private bool isMoving = false;
    private int targetFloor;

    private void Start()
    {
        targetFloor = minFloor; // Start at the minimum floor
        StartCoroutine(MoveElevator());
    }

    private IEnumerator MoveElevator()
    {
        while (true)
        {
            // Move to the target floor
            yield return StartCoroutine(MoveToFloor(targetFloor));

            // Wait at the target floor
            yield return new WaitForSeconds(waitTime);

            // Update target floor
            if (targetFloor >= maxFloor)
            {
                targetFloor = minFloor; // Go back to the minimum floor
            }
            else
            {
                targetFloor++; // Move to the next floor
            }
        }
    }

    private IEnumerator MoveToFloor(int floor)
    {
        isMoving = true;
        Debug.Log("Moving to floor: " + floor);

        // Calculate the target position (adjust as needed)
        Vector3 targetPosition = new Vector3(transform.position.x, floor, transform.position.z);

        while (transform.position.y != targetPosition.y)
        {
            // Move toward the target position
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null; // Wait for the next frame
        }

        currentFloor = floor;
        Debug.Log("Arrived at floor: " + currentFloor);
        isMoving = false;
    }
}