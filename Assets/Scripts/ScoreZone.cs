using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Trigger zone where players deliver flags to score points.
/// Players must first pick up a flag, then enter this zone to score.
/// </summary>
public class ScoreZone : MonoBehaviour
{
    /// <summary>
    /// The team that can score at this zone.
    /// </summary>
    [SerializeField]
    private Team scoreTeam = Team.Blue;

    /// <summary>
    /// Points awarded when a flag is delivered.
    /// </summary>
    [SerializeField]
    private int pointsPerScore = 1;

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

        // Only allow scoring if the player is on the correct team
        if (!teamMember.IsOnTeam(scoreTeam))
        {
            return;
        }

        // Only score if the player is carrying a flag
        if (!teamMember.TryScoreFlag())
        {
            return;
        }

        // Add score for the team
        if (GameController.Instance != null)
        {
            GameController.Instance.AddScoreRpc(scoreTeam, pointsPerScore);
        }
    }
}
