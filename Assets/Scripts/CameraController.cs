using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Controls camera movement to follow the local player in a networked game.
/// </summary>
public sealed class CameraController : MonoBehaviour
{
    /// <summary>
    /// Offset from the player position (X = horizontal, Y = vertical).
    /// </summary>
    [SerializeField] private Vector3 offset = new(0f, 3f, -10f);

    /// <summary>
    /// How smoothly the camera follows the player.
    /// </summary>
    [SerializeField] private float smoothSpeed = 5f;

    private Transform target;

    private void Start()
    {
        // Start searching for player
        InvokeRepeating(nameof(TryFindLocalPlayer), 0.5f, 0.5f);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // Calculate desired position
        Vector3 desiredPosition = target.position + offset;

        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Searches for the local player and sets it as the follow target.
    /// </summary>
    private void TryFindLocalPlayer()
    {
        if (target != null)
        {
            CancelInvoke(nameof(TryFindLocalPlayer));
            return;
        }

        // Find all players
        var players = GameObject.FindGameObjectsWithTag("Player");

        foreach (var player in players)
        {
            var netObj = player.GetComponent<NetworkObject>();
            
            if (netObj != null && netObj.IsSpawned && netObj.IsLocalPlayer)
            {
                target = player.transform;
                Debug.Log($"[CameraController] Now following: {player.name}");
                
                // Stop searching
                CancelInvoke(nameof(TryFindLocalPlayer));
                
                // Snap to player immediately
                transform.position = target.position + offset;
                return;
            }
        }
    }

    /// <summary>
    /// Manually set the camera target.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        CancelInvoke(nameof(TryFindLocalPlayer));
        
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }
}
