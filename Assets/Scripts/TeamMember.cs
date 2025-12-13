using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Component that identifies which team a player belongs to.
/// </summary>
public class TeamMember : NetworkBehaviour
{
    /// <summary>
    /// Network variable to sync the team assignment across all clients.
    /// </summary>
    private readonly NetworkVariable<Team> team = new(Team.None);

    /// <summary>
    /// Whether this player is currently carrying a flag and can score.
    /// </summary>
    private readonly NetworkVariable<bool> hasFlag = new(false);

    /// <summary>
    /// Gets the current team this player belongs to.
    /// </summary>
    public Team CurrentTeam => team.Value;

    /// <summary>
    /// Gets whether this player is carrying a flag.
    /// </summary>
    public bool HasFlag => hasFlag.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        team.OnValueChanged += OnTeamChanged;

        // If team is already set, notify the local player
        if (IsLocalPlayer && team.Value != Team.None)
        {
            NotifyLocalPlayerTeam(team.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        team.OnValueChanged -= OnTeamChanged;
    }

    /// <summary>
    /// Called when the team value changes. Notifies the GameController if this is the local player.
    /// </summary>
    private void OnTeamChanged(Team oldValue, Team newValue)
    {
        if (IsLocalPlayer)
        {
            NotifyLocalPlayerTeam(newValue);
        }
    }

    /// <summary>
    /// Notifies the GameController of the local player's team assignment.
    /// </summary>
    private void NotifyLocalPlayerTeam(Team assignedTeam)
    {
        if (GameController.Instance != null)
        {
            GameController.Instance.SetLocalPlayerTeam(assignedTeam);
        }
        else
        {
            // Defer if GameController not ready yet
            StartCoroutine(WaitForGameControllerAndNotify(assignedTeam));
        }
    }

    private System.Collections.IEnumerator WaitForGameControllerAndNotify(Team assignedTeam)
    {
        float timeout = 5f;
        float elapsed = 0f;
        while (GameController.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (GameController.Instance != null)
        {
            GameController.Instance.SetLocalPlayerTeam(assignedTeam);
        }
    }

    /// <summary>
    /// Sets the team for this player. Can only be called on the server.
    /// </summary>
    /// <param name="newTeam">The team to assign to this player.</param>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetTeamRpc(Team newTeam)
    {
        team.Value = newTeam;
    }

    /// <summary>
    /// Checks if this player is on the specified team.
    /// </summary>
    /// <param name="checkTeam">The team to check against.</param>
    /// <returns>True if the player is on the specified team.</returns>
    public bool IsOnTeam(Team checkTeam)
    {
        return team.Value == checkTeam;
    }

    /// <summary>
    /// Sets the flag carrying state. Called when the player picks up a flag.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PickUpFlagRpc()
    {
        hasFlag.Value = true;
    }

    /// <summary>
    /// Attempts to score by delivering the flag. Returns true if successful.
    /// </summary>
    /// <returns>True if the player was carrying a flag and scored.</returns>
    public bool TryScoreFlag()
    {
        if (!hasFlag.Value)
        {
            return false;
        }

        if (IsServer)
        {
            hasFlag.Value = false;
        }
        else
        {
            ClearFlagRpc();
        }

        return true;
    }

    /// <summary>
    /// Clears the flag carrying state on the server.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ClearFlagRpc()
    {
        hasFlag.Value = false;
    }
}
