using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Manages game initialization, player spawning, team assignment, and scoring for networked multiplayer.
/// </summary>
public class GameController : NetworkBehaviour
{
    /// <summary>
    /// The player prefab to instantiate for each connected client.
    /// </summary>
    public GameObject playerPrefabs;

    /// <summary>
    /// List of spawn positions for players. Each player spawns at the position corresponding to their index.
    /// </summary>
    public List<Transform> spawnPos;

    /// <summary>
    /// UI Text element to display the player's own team score.
    /// </summary>
    public Text scoreText;

    /// <summary>
    /// Network variable to sync blue team score across all clients.
    /// </summary>
    private NetworkVariable<int> blueTeamScore = new NetworkVariable<int>(0);

    /// <summary>
    /// Network variable to sync red team score across all clients.
    /// </summary>
    private NetworkVariable<int> redTeamScore = new NetworkVariable<int>(0);

    /// <summary>
    /// The local player's team, used to determine which score to display.
    /// </summary>
    private Team localPlayerTeam = Team.None;

    /// <summary>
    /// Singleton instance for easy access from other scripts.
    /// </summary>
    public static GameController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called when the network object is spawned. The host spawns player objects for all connected clients.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        blueTeamScore.OnValueChanged += OnBlueScoreChanged;
        redTeamScore.OnValueChanged += OnRedScoreChanged;

        UpdateScoreUI();

        if (IsHost)
        {
            SpawnAllPlayers();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        blueTeamScore.OnValueChanged -= OnBlueScoreChanged;
        redTeamScore.OnValueChanged -= OnRedScoreChanged;
    }

    /// <summary>
    /// Spawns player objects for all connected clients at their designated spawn positions.
    /// Host (index 0) is assigned to Blue team, other players to Red team.
    /// </summary>
    private void SpawnAllPlayers()
    {
        Quaternion spawnRotation = Quaternion.Euler(0f, 90f, 0f);
        for (int i = 0; i < NetworkManager.Singleton.ConnectedClientsIds.Count; i++)
        {
            ulong clientID = NetworkManager.Singleton.ConnectedClientsIds[i];
            GameObject player = Instantiate(playerPrefabs, spawnPos[i].position, spawnRotation);

            var networkObject = player.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(clientID);

            // Assign team: Host (index 0) = Blue, others = Red
            var teamMember = player.GetComponent<TeamMember>();
            if (teamMember != null)
            {
                Team assignedTeam = (i == 0) ? Team.Blue : Team.Red;
                teamMember.SetTeamRpc(assignedTeam);
            }
        }
    }

    /// <summary>
    /// Called when blue team score changes. Updates the UI.
    /// </summary>
    private void OnBlueScoreChanged(int oldValue, int newValue)
    {
        if (localPlayerTeam == Team.Blue)
        {
            UpdateScoreUI();
        }
    }

    /// <summary>
    /// Called when red team score changes. Updates the UI.
    /// </summary>
    private void OnRedScoreChanged(int oldValue, int newValue)
    {
        if (localPlayerTeam == Team.Red)
        {
            UpdateScoreUI();
        }
    }

    /// <summary>
    /// Updates the score UI to show only the local player's team score.
    /// </summary>
    private void UpdateScoreUI()
    {
        if (scoreText == null)
        {
            return;
        }

        int score = GetScore(localPlayerTeam);
        scoreText.text = "Score: " + score;
    }

    /// <summary>
    /// Sets the local player's team and updates the UI accordingly.
    /// </summary>
    /// <param name="team">The team assigned to the local player.</param>
    public void SetLocalPlayerTeam(Team team)
    {
        localPlayerTeam = team;
        UpdateScoreUI();
    }

    /// <summary>
    /// Adds score to the specified team. Called from flag triggers.
    /// </summary>
    /// <param name="team">The team to add score to.</param>
    /// <param name="points">The number of points to add.</param>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AddScoreRpc(Team team, int points = 1)
    {
        if (team == Team.Blue)
        {
            blueTeamScore.Value += points;
        }
        else if (team == Team.Red)
        {
            redTeamScore.Value += points;
        }
    }

    /// <summary>
    /// Gets the current score for the specified team.
    /// </summary>
    /// <param name="team">The team to get the score for.</param>
    /// <returns>The team's current score.</returns>
    public int GetScore(Team team)
    {
        if (team == Team.Blue)
        {
            return blueTeamScore.Value;
        }
        else if (team == Team.Red)
        {
            return redTeamScore.Value;
        }
        return 0;
    }
}
