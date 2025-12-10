using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple elevator that oscillates vertically and carries player passengers along with its motion.
/// </summary>
public class Elevator : MonoBehaviour
{
    /// <summary>
    /// Vertical movement speed in units per second.
    /// </summary>
    public float moveSpeed = 2.0f; // Vertical speed

    /// <summary>
    /// Travel distance up and down from the initial position.
    /// </summary>
    public float limitDistance = 5.0f; // Travel distance up/down from initial position

    private Vector3 initialPosition;
    private bool movingUp = true;
    private Vector3 lastPosition;
    private readonly HashSet<Transform> passengers = new();

    /// <summary>
    /// Cache the starting position and initialize tracking.
    /// </summary>
    private void Start()
    {
        initialPosition = transform.position;
        lastPosition = transform.position;
    }

    /// <summary>
    /// Move the elevator each frame and shift passengers accordingly.
    /// </summary>
    private void Update()
    {
        MoveElevator();

        Vector3 frameDelta = transform.position - lastPosition;
        MovePassengers(frameDelta);
        lastPosition = transform.position;
    }

    /// <summary>
    /// Oscillate the elevator between the configured vertical limits.
    /// </summary>
    private void MoveElevator()
    {
        float moveDirection = movingUp ? 1f : -1f;
        transform.position += new Vector3(0f, moveDirection * moveSpeed * Time.deltaTime, 0f);

        if (transform.position.y >= initialPosition.y + limitDistance)
        {
            movingUp = false;
        }
        else if (transform.position.y <= initialPosition.y - limitDistance)
        {
            movingUp = true;
        }
    }

    /// <summary>
    /// Track players entering the trigger so they ride with the platform.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            passengers.Add(other.transform);
        }
    }

    /// <summary>
    /// Stop tracking players that leave the trigger area.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            passengers.Remove(other.transform);
        }
    }

    /// <summary>
    /// Track players that physically collide with the elevator.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            passengers.Add(collision.collider.transform);
        }
    }

    /// <summary>
    /// Stop tracking players once they separate from the elevator surface.
    /// </summary>
    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            passengers.Remove(collision.collider.transform);
        }
    }

    /// <summary>
    /// Move tracked passengers by the elevator's frame delta to keep them aligned.
    /// </summary>
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
