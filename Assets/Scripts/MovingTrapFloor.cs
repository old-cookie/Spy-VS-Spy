using System.Collections; 
using UnityEngine;

public class MovingTrapFloor : MonoBehaviour
{
    public float moveSpeed = 2.0f; // Speed of the floor movement
    public float limitDistance = 5.0f; // Distance for horizontal movement
    public float disappearDuration = 2.0f; // Duration before disappearing
    public GameObject trapEffect; // Optional effect on disappear

    private Vector3 initialPosition;
    private bool movingRight = true;
    private bool isTrapped = false;

    private void Start()
    {
        initialPosition = transform.position; // Initial position of the floor
    }

    private void Update()
    {
        if (!isTrapped)
        {
            MoveFloor();
        }
    }

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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player is on the trap floor!");
            StartCoroutine(DisappearTrap());
        }
    }

    private IEnumerator DisappearTrap()
    {
        if (trapEffect != null)
        {
            Instantiate(trapEffect, transform.position, Quaternion.identity);
        }

        isTrapped = true; // Stop movement

        // Wait
        yield return new WaitForSeconds(disappearDuration);

        gameObject.SetActive(false);
        Debug.Log("Trap floor has disappeared!");
    }
}