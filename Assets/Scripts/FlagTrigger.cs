using UnityEngine;

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
    /// Reference to the TeamMember component of the player in the trigger zone.
    /// </summary>
    private TeamMember currentTeamMember;

    /// <summary>
    /// Reference to the PlayerController component of the player in the trigger zone.
    /// </summary>
    private PlayerController currentPlayerController;

    /// <summary>
    /// Performs the actual flag pickup. Called by PlayerController after animation starts.
    /// </summary>
    public void PerformPickup()
    {
        if (currentTeamMember == null)
        {
            return;
        }

        currentTeamMember.PickUpFlagRpc();
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
