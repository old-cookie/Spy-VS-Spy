using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Trigger zone for team flags. When a player from the matching team enters and presses E, they pick up the flag.
/// </summary>
public class FlagTrigger : MonoBehaviour
{
    /// <summary>
    /// The team that can pick up this flag.
    /// </summary>
    [SerializeField]
    private Team flagTeam = Team.Blue;

    /// <summary>
    /// The team allowed to pick this flag.
    /// </summary>
    public Team Team => flagTeam;

    /// <summary>
    /// Reference to the TeamMember component of the player in the trigger zone.
    /// </summary>
    private TeamMember currentTeamMember;

    /// <summary>
    /// Reference to the PlayerController component of the player in the trigger zone.
    /// </summary>
    private PlayerController currentPlayerController;

    /// <summary>
    /// Performs the actual flag pickup. Called by PlayerController after animation starts.
    /// Only starts mini game for the local player.
    /// </summary>
    public void PerformPickup()
    {
        if (currentTeamMember == null)
        {
            return;
        }

        if (currentPlayerController == null)
        {
            return;
        }

        // Check if this is the local player
        var networkObject = currentPlayerController.GetComponent<NetworkObject>();
        var isLocalPlayer = networkObject != null && networkObject.IsLocalPlayer;

        if (isLocalPlayer)
        {
            // Start a random mini game for the local player
            if (MiniGameManager.Instance != null)
            {
                // Capture reference for the callback
                var teamMember = currentTeamMember;
                
                // Only pick up flag if mini game is completed successfully (result = 1)
                MiniGameManager.Instance.StartRandomMiniGame(currentPlayerController, () =>
                {
                    if (teamMember != null)
                    {
                        teamMember.PickUpFlagRpc();
                    }
                });
            }
            else
            {
                Debug.LogWarning("[FlagTrigger] MiniGameManager not found. Proceeding without mini game.");
                currentTeamMember.PickUpFlagRpc();
            }
        }
        else
        {
            // Non-local players just pick up the flag directly
            currentTeamMember.PickUpFlagRpc();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent<TeamMember>(out var teamMember))
        {
            teamMember = other.GetComponentInParent<TeamMember>();
        }

        if (teamMember == null)
        {
            return;
        }

        // Only allow flag pickup if the player is on the same team as the flag
        if (!teamMember.IsOnTeam(flagTeam))
        {
            return;
        }

        // Cannot pick up another flag if already carrying one
        if (teamMember.HasFlag)
        {
            return;
        }

        if (!other.TryGetComponent<PlayerController>(out var playerController))
        {
            playerController = other.GetComponentInParent<PlayerController>();
        }

        if (playerController == null)
        {
            return;
        }

        currentTeamMember = teamMember;
        currentPlayerController = playerController;
        playerController.SetCurrentFlag(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent<PlayerController>(out var playerController))
        {
            playerController = other.GetComponentInParent<PlayerController>();
        }

        if (playerController == null)
        {
            return;
        }

        if (playerController == currentPlayerController)
        {
            playerController.SetCurrentFlag(null);
            currentTeamMember = null;
            currentPlayerController = null;
        }
    }
}
