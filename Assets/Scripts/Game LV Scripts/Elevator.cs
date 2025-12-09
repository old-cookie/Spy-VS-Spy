using System.Collections;
using UnityEngine;

public class AutomatedElevator : MonoBehaviour
{
    public float targetHeight = 5.0f;      // Height the elevator will move to
    public float moveSpeed = 2.0f;          // Speed of the elevator
    public float waitTime = 1.0f;           // Time to wait at the top and bottom

    private Vector3 startingPosition;
    private Vector3 targetPosition;

    private void Start()
    {
        startingPosition = transform.position; // Store the initial position
        targetPosition = new Vector3(transform.position.x, startingPosition.y + targetHeight, transform.position.z);
        StartCoroutine(MoveElevator());
    }

    private IEnumerator MoveElevator()
    {
        while (true)
        {
            // Move to the target position
            yield return StartCoroutine(MoveTo(targetPosition));

            // Wait at the top
            yield return new WaitForSeconds(waitTime);

            // Move back to the starting position
            yield return StartCoroutine(MoveTo(startingPosition));

            // Wait at the bottom
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator MoveTo(Vector3 destination)
    {
        // Calculate the distance to be covered
        float distance = Vector3.Distance(transform.position, destination);
        float totalTime = distance / moveSpeed; // Calculate time needed based on speed

        float elapsedTime = 0f;

        while (elapsedTime < totalTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
            elapsedTime += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // Ensure we reach the destination
        transform.position = destination;
    }
}