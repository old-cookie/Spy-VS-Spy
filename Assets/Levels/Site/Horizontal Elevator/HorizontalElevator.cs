using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves a floor segment horizontally and carries players using frame delta.
/// </summary>
public class HorizontalElevator : MonoBehaviour
{
    /// <summary>
    /// Speed of the horizontal movement in units per second.
    /// </summary>
    public float moveSpeed = 2.0f;

    /// <summary>
    /// Maximum distance to travel from the initial position on the X axis.
    /// </summary>
    public float limitDistance = 5.0f;

    private Vector3 initialPosition;
    private bool movingRight = true;
    private Vector3 lastPosition;
    private readonly HashSet<Transform> passengers = new();

    /// <summary>
    /// Captures the initial transform state for limit calculations.
    /// </summary>
    private void Start()
    {
        initialPosition = transform.position;
        lastPosition = transform.position;
    }

    /// <summary>
    /// Updates platform motion and transports passengers each frame.
    /// </summary>
    private void Update()
    {
        MoveFloor();

        /// <remarks>Apply platform delta so passengers move with it without parenting (avoids NetworkObject parenting issues).</remarks>
        Vector3 frameDelta = transform.position - lastPosition;
        MovePassengers(frameDelta);
        lastPosition = transform.position;
    }

    /// <summary>
    /// Moves the platform and flips direction when reaching the configured limits.
    /// </summary>
    private void MoveFloor()
    {
        float moveDirection = movingRight ? 1 : -1;
        transform.position += new Vector3(moveDirection * moveSpeed * Time.deltaTime, 0, 0);

        // Check if the floor has reached limits
        if (transform.position.x >= initialPosition.x + limitDistance)
        {
            movingRight = false;
        }
        else if (transform.position.x <= initialPosition.x - limitDistance)
        {
            movingRight = true;
        }
    }

    /// <summary>
    /// Adds passengers when they enter the trigger collider.
    /// </summary>
    /// <param name="other">Collider entering the trigger.</param>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            passengers.Add(other.transform);
        }
    }

    /// <summary>
    /// Removes passengers when they leave the trigger collider.
    /// </summary>
    /// <param name="other">Collider exiting the trigger.</param>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            passengers.Remove(other.transform);
        }
    }

    /// <summary>
    /// Adds passengers when colliding with solid colliders so they move with the platform.
    /// </summary>
    /// <param name="collision">Collision information for the entering object.</param>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            passengers.Add(collision.collider.transform);
        }
    }

    /// <summary>
    /// Removes passengers when they stop colliding with the platform.
    /// </summary>
    /// <param name="collision">Collision information for the exiting object.</param>
    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            passengers.Remove(collision.collider.transform);
        }
    }

    /// <summary>
    /// Applies the platform's frame delta to all passengers to keep them in sync.
    /// </summary>
    /// <param name="frameDelta">Displacement of the platform during this frame.</param>
    private void MovePassengers(Vector3 frameDelta)
    {
        if (frameDelta == Vector3.zero)
        {
            return;
        }

        foreach (Transform passenger in passengers)
        {
            if (passenger == null)
            {
                continue;
            }

            // Prefer Rigidbody movement if available to stay in sync with physics/networked characters
            Rigidbody rb = passenger.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.MovePosition(rb.position + frameDelta);
            }
            else
            {
                passenger.position += frameDelta;
            }
        }
    }
}