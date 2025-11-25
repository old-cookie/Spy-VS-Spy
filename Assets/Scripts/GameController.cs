using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameController : NetworkBehaviour
{
    public GameObject playerPrefabs;
    public List<Transform> spawnPos;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsHost)
        {
            Quaternion spawnRotation = Quaternion.Euler(0f, 90f, 0f);
            for (int i = 0; i < NetworkManager.Singleton.ConnectedClientsIds.Count; i++)
            {
                ulong clientID = NetworkManager.Singleton.ConnectedClientsIds[i];
                GameObject player = Instantiate(playerPrefabs, spawnPos[i].position, spawnRotation);
                player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientID);
            }
        }
    }
}
