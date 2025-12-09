using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

/// <summary>
/// Holds the level selection chosen in the lobby and syncs it to clients before loading the game scene.
/// Attach this to a NetworkObject that persists across the lobby and game scenes.
/// </summary>
public class LevelSelectionState : NetworkBehaviour
{
    /// <summary>
    /// Singleton-style access so other scripts can query the chosen level.
    /// </summary>
    public static LevelSelectionState Instance { get; private set; }

    /// <summary>
    /// Networked chosen level prefab name (host writes, everyone reads).
    /// </summary>
    private readonly NetworkVariable<FixedString64Bytes> selectedLevelName =
        new(new FixedString64Bytes(""),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    /// <summary>
    /// Networked winning team so the end scene knows who won.
    /// </summary>
    private readonly NetworkVariable<Team> winningTeam =
        new(Team.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    /// <summary>
    /// Currently selected level prefab name.
    /// </summary>
    public string SelectedLevelName => selectedLevelName.Value.ToString();

    /// <summary>
    /// Team that won the last match.
    /// </summary>
    public Team WinningTeam => winningTeam.Value;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Sets the chosen level name (host only).
    /// </summary>
    /// <param name="levelName">Prefab name to load in the game scene.</param>
    public void SetSelectedLevelName(string levelName)
    {
        if (!IsServer)
        {
            return;
        }

        selectedLevelName.Value = new FixedString64Bytes(levelName ?? string.Empty);
    }

    /// <summary>
    /// Sets the winning team (server only).
    /// </summary>
    /// <param name="team">Team that reached the win condition.</param>
    public void SetWinningTeam(Team team)
    {
        if (!IsServer)
        {
            return;
        }

        winningTeam.Value = team;
    }

    /// <summary>
    /// Clears the winning team state (server only).
    /// </summary>
    public void ClearWinningTeam()
    {
        if (!IsServer)
        {
            return;
        }

        winningTeam.Value = Team.None;
    }
}
