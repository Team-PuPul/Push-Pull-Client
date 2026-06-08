using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PushPullNetworkManager : NetworkManager
{
    [SerializeField]
    private string whiteSpawnName = "WhiteStartPoint";

    [SerializeField]
    private string blackSpawnName = "BlackStartPoint";

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        Debug.Log(
            $"[PushPullNetworkManager] OnServerSceneChanged scene={sceneName}, isServer={NetworkServer.active}"
        );

        if (!sceneName.StartsWith("Stage"))
            return;

        StartCoroutine(MovePlayersToStageSpawnPointsAfterSceneLoad());
    }

    private IEnumerator MovePlayersToStageSpawnPointsAfterSceneLoad()
    {
        const float timeout = 3f;
        float startTime = Time.time;

        while (!AllPlayersHaveIdentity())
        {
            if (Time.time - startTime > timeout)
            {
                Debug.LogError(
                    $"[PushPullNetworkManager] Player identity wait timeout. "
                        + $"connectionCount={NetworkServer.connections.Count}, isServer={NetworkServer.active}"
                );
                yield break;
            }

            yield return null;
        }

        MovePlayersToStageSpawnPoints();
    }

    private bool AllPlayersHaveIdentity()
    {
        if (NetworkServer.connections.Count == 0)
            return false;

        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null)
                continue;

            if (connection.identity == null)
                return false;
        }

        return true;
    }

    private void MovePlayersToStageSpawnPoints()
    {
        Transform whiteSpawn = FindSpawnPoint(whiteSpawnName);
        Transform blackSpawn = FindSpawnPoint(blackSpawnName);

        if (whiteSpawn == null || blackSpawn == null)
        {
            Debug.LogError(
                $"[PushPullNetworkManager] Stage spawn points not found. "
                    + $"white={whiteSpawn != null}, black={blackSpawn != null}, "
                    + $"isServer={NetworkServer.active}, isClient={NetworkClient.active}"
            );
            return;
        }

        List<NetworkConnectionToClient> connections = new List<NetworkConnectionToClient>(
            NetworkServer.connections.Values
        );

        connections.Sort((a, b) => a.connectionId.CompareTo(b.connectionId));

        for (int i = 0; i < connections.Count; i++)
        {
            NetworkConnectionToClient connection = connections[i];

            if (connection == null)
                continue;

            NetworkIdentity player = connection.identity;

            if (player == null)
            {
                Debug.LogWarning(
                    $"[PushPullNetworkManager] Player identity is null. "
                        + $"connectionId={connection.connectionId}, isServer={NetworkServer.active}"
                );
                continue;
            }

            Transform spawn = i == 0 ? whiteSpawn : blackSpawn;

            NetworkTransformBase networkTransform = player.GetComponent<NetworkTransformBase>();

            if (networkTransform != null)
            {
                networkTransform.ServerTeleport(spawn.position, spawn.rotation);
            }
            else
            {
                player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            }

            Debug.Log(
                $"[PushPullNetworkManager] Move player to spawn. "
                    + $"connectionId={connection.connectionId}, player={player.name}, spawn={spawn.name}, position={spawn.position}"
            );
        }
    }

    private Transform FindSpawnPoint(string spawnName)
    {
        GameObject spawnObject = GameObject.Find(spawnName);
        return spawnObject != null ? spawnObject.transform : null;
    }
}
