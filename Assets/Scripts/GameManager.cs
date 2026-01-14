using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq; // Required for Find/FirstOrDefault

public class GameManager : NetworkBehaviour
{
    [Header("Resources")]
    [SerializeField] private NetworkPrefabsList itemsToSpawn;

    [Header("Scene References")]
    [SerializeField] private List<Vector3> spawnPoints = new List<Vector3>() { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };

    private GameObject ballPrefab;

    private void Awake()
    {
        if (itemsToSpawn == null)
        {
            Debug.LogError("GameManager: itemsToSpawn (NetworkPrefabsList) is not assigned!");
            return;
        }

        // Use LINQ to safely find the prefab by name
        var networkPrefab = itemsToSpawn.PrefabList.FirstOrDefault(p => p.Prefab.name.Contains("Ball"));

        if (networkPrefab != null && networkPrefab.Prefab != null)
        {
            ballPrefab = networkPrefab.Prefab;
        }
        else
        {
            Debug.LogError("GameManager: Could not find a prefab with 'Ball' in the name within the NetworkPrefabsList!");
        }
    }

    public override void OnNetworkSpawn()
    {
        // Spawning must happen on the Server/Host to replicate to clients
        if (IsServer)
        {
            SpawnBalls();
        }
    }

    private void SpawnBalls()
    {
        if (ballPrefab == null) return;

        foreach (Vector3 point in spawnPoints)
        {
            if (point == null) continue;

            // 1. Instantiate the GameObject on the Server
            GameObject ballInstance = Instantiate(ballPrefab, point, Quaternion.identity);

            // 2. Spawn it on the Network
            // This replicates the object to all connected clients
            NetworkObject nO = ballInstance.GetComponent<NetworkObject>();
            if (nO != null)
            {
                nO.Spawn();
            }
        }
    }
}