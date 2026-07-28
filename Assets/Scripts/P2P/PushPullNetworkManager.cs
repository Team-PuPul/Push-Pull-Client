using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PushPullNetworkManager : NetworkManager
{
    private const string MainMenuScenePath = "Assets/Scenes/InGameScenes/UI/MainUI.unity";

    private const string WaitingRoomScenePath = "Assets/Scenes/InGameScenes/WaitingRoom.unity";

    [Header("Network Capacity")]
    [SerializeField]
    [Range(2, 4)]
    private int supportedMaxConnections = 4;

    [Header("Player Prefabs")]
    [SerializeField]
    private GameObject blackPlayerPrefab;

    [Header("Stage Spawn Points")]
    [SerializeField]
    private string whiteSpawnName = "WhiteStartPoint";

    [SerializeField]
    private string blackSpawnName = "BlackStartPoint";

    private Coroutine stageSpawnPlacementCoroutine;
    private bool hasPendingStageSpawnPlacement;
    private int stageSpawnPlacementVersion;
    private string pendingStageSceneName;

    public override void Awake()
    {
        // MainUI에서 네트워크를 시작하고,
        // 연결 성공 후 Mirror가 WaitingRoom을 로드한다.
        if (string.IsNullOrWhiteSpace(offlineScene))
            offlineScene = MainMenuScenePath;

        if (string.IsNullOrWhiteSpace(onlineScene))
            onlineScene = WaitingRoomScenePath;

        dontDestroyOnLoad = true;

        // 현재 협동 방은 2명이지만,
        // NetworkManager 자체는 향후 PVP를 위해 4명까지 허용한다.
        maxConnections = supportedMaxConnections;
        autoCreatePlayer = true;

        base.Awake();
    }

    public override void OnClientDisconnect()
    {
        // 호스트의 로컬 클라이언트 종료가 아니라
        // 원격 호스트와 연결이 끊긴 게스트만 처리한다.
        if (!NetworkServer.active)
        {
            SteamLobby steamLobby = GetComponent<SteamLobby>();
            steamLobby?.HandleUnexpectedClientDisconnect();
        }

        base.OnClientDisconnect();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Transform startPosition = GetStartPosition();
        GameObject selectedPrefab = GetPlayerPrefabForConnection(conn);

        Vector3 spawnPosition = startPosition != null ? startPosition.position : Vector3.zero;

        Quaternion spawnRotation =
            startPosition != null ? startPosition.rotation : Quaternion.identity;

        GameObject player = Instantiate(selectedPrefab, spawnPosition, spawnRotation);

        NetworkServer.AddPlayerForConnection(conn, player);

        Debug.Log(
            $"[PushPullNetworkManager] Add player. "
                + $"connectionId={conn.connectionId}, "
                + $"prefab={selectedPrefab.name}"
        );
    }

    private GameObject GetPlayerPrefabForConnection(NetworkConnectionToClient conn)
    {
        int existingPlayerCount = 0;

        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null)
                continue;

            if (connection.identity != null)
                existingPlayerCount++;
        }

        if (existingPlayerCount == 0)
            return playerPrefab;

        if (blackPlayerPrefab != null)
            return blackPlayerPrefab;

        Debug.LogWarning(
            "[PushPullNetworkManager] blackPlayerPrefab이 비어있어서 "
                + "기본 playerPrefab을 사용합니다."
        );

        return playerPrefab;
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        Debug.Log(
            $"[PushPullNetworkManager] OnServerSceneChanged "
                + $"scene={sceneName}, isServer={NetworkServer.active}"
        );

        if (!IsStageScene(sceneName))
        {
            ClearPendingStageSpawnPlacement();
            return;
        }

        BeginStageSpawnPlacement(sceneName);
    }

    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);

        if (!NetworkServer.active || !hasPendingStageSpawnPlacement)
            return;

        if (!IsStageScene(SceneManager.GetActiveScene().name))
            return;

        TryStartStageSpawnPlacementCoroutine();
    }

    private void BeginStageSpawnPlacement(string sceneName)
    {
        if (stageSpawnPlacementCoroutine != null)
        {
            StopCoroutine(stageSpawnPlacementCoroutine);
            stageSpawnPlacementCoroutine = null;
        }

        hasPendingStageSpawnPlacement = true;
        pendingStageSceneName = sceneName;
        stageSpawnPlacementVersion++;

        TryStartStageSpawnPlacementCoroutine();
    }

    private void TryStartStageSpawnPlacementCoroutine()
    {
        if (!hasPendingStageSpawnPlacement || stageSpawnPlacementCoroutine != null)
            return;

        stageSpawnPlacementCoroutine = StartCoroutine(
            MovePlayersToStageSpawnPointsWhenReady(
                stageSpawnPlacementVersion,
                pendingStageSceneName
            )
        );
    }

    private IEnumerator MovePlayersToStageSpawnPointsWhenReady(
        int placementVersion,
        string sceneName
    )
    {
        const float timeout = 5f;
        float startTime = Time.time;

        yield return null;

        while (!AllPlayersReadyWithIdentity() || !StageSpawnPointsExist())
        {
            if (!IsCurrentStageSpawnPlacement(placementVersion))
            {
                ClearStageSpawnPlacementCoroutine(placementVersion);
                yield break;
            }

            if (Time.time - startTime > timeout)
            {
                Debug.LogError(
                    $"[PushPullNetworkManager] Stage spawn placement timeout. "
                        + $"scene={sceneName}, "
                        + $"connectionCount={NetworkServer.connections.Count}, "
                        + $"playersReady={AllPlayersReadyWithIdentity()}, "
                        + $"spawnPointsReady={StageSpawnPointsExist()}, "
                        + $"isServer={NetworkServer.active}"
                );

                hasPendingStageSpawnPlacement = false;
                ClearStageSpawnPlacementCoroutine(placementVersion);
                yield break;
            }

            yield return null;
        }

        yield return null;

        if (!IsCurrentStageSpawnPlacement(placementVersion))
        {
            ClearStageSpawnPlacementCoroutine(placementVersion);
            yield break;
        }

        MovePlayersToStageSpawnPoints();
        hasPendingStageSpawnPlacement = false;

        ClearStageSpawnPlacementCoroutine(placementVersion);
    }

    private bool AllPlayersReadyWithIdentity()
    {
        if (NetworkServer.connections.Count == 0)
            return false;

        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection == null)
                return false;

            if (!connection.isReady || connection.identity == null)
                return false;
        }

        return true;
    }

    private bool StageSpawnPointsExist()
    {
        return FindSpawnPoint(whiteSpawnName) != null && FindSpawnPoint(blackSpawnName) != null;
    }

    private bool IsCurrentStageSpawnPlacement(int placementVersion)
    {
        return hasPendingStageSpawnPlacement && placementVersion == stageSpawnPlacementVersion;
    }

    private void ClearStageSpawnPlacementCoroutine(int placementVersion)
    {
        if (placementVersion == stageSpawnPlacementVersion)
            stageSpawnPlacementCoroutine = null;
    }

    private void ClearPendingStageSpawnPlacement()
    {
        hasPendingStageSpawnPlacement = false;
        pendingStageSceneName = null;

        if (stageSpawnPlacementCoroutine == null)
            return;

        StopCoroutine(stageSpawnPlacementCoroutine);
        stageSpawnPlacementCoroutine = null;
    }

    private bool IsStageScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        string normalizedSceneName = sceneName.Replace('\\', '/');
        int slashIndex = normalizedSceneName.LastIndexOf('/');

        if (slashIndex >= 0)
            normalizedSceneName = normalizedSceneName.Substring(slashIndex + 1);

        const string sceneExtension = ".unity";

        if (normalizedSceneName.EndsWith(sceneExtension))
            normalizedSceneName = normalizedSceneName.Substring(
                0,
                normalizedSceneName.Length - sceneExtension.Length
            );

        return normalizedSceneName.StartsWith("Stage");
    }

    private bool MovePlayersToStageSpawnPoints()
    {
        Transform whiteSpawn = FindSpawnPoint(whiteSpawnName);
        Transform blackSpawn = FindSpawnPoint(blackSpawnName);

        if (whiteSpawn == null || blackSpawn == null)
        {
            Debug.LogError(
                $"[PushPullNetworkManager] Stage spawn points not found. "
                    + $"white={whiteSpawn != null}, "
                    + $"black={blackSpawn != null}, "
                    + $"isServer={NetworkServer.active}, "
                    + $"isClient={NetworkClient.active}"
            );

            return false;
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
                        + $"connectionId={connection.connectionId}, "
                        + $"isServer={NetworkServer.active}"
                );

                continue;
            }

            Transform spawn = i == 0 ? whiteSpawn : blackSpawn;

            ApplyPlayerStageSpawn(player, spawn.position, spawn.rotation);

            Debug.Log(
                $"[PushPullNetworkManager] Move player to spawn. "
                    + $"connectionId={connection.connectionId}, "
                    + $"player={player.name}, "
                    + $"spawn={spawn.name}, "
                    + $"position={spawn.position}"
            );
        }

        return true;
    }

    private void ApplyPlayerStageSpawn(
        NetworkIdentity player,
        Vector3 position,
        Quaternion rotation
    )
    {
        InputPlayer inputPlayer = player.GetComponent<InputPlayer>();

        if (inputPlayer != null)
        {
            inputPlayer.ServerApplyStageSpawnState(position, rotation);
            return;
        }

        NetworkTransformBase networkTransform = player.GetComponent<NetworkTransformBase>();

        if (networkTransform != null)
            networkTransform.ServerTeleport(position, rotation);
        else
            player.transform.SetPositionAndRotation(position, rotation);

        ResetRigidbody2D(player.GetComponent<Rigidbody2D>(), position, rotation);
    }

    private void ResetRigidbody2D(
        Rigidbody2D body,
        Vector3 position,
        Quaternion rotation
    )
    {
        if (body == null)
            return;

        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.position = position;
        body.SetRotation(rotation.eulerAngles.z);
    }

    private Transform FindSpawnPoint(string spawnName)
    {
        GameObject spawnObject = GameObject.Find(spawnName);

        return spawnObject != null ? spawnObject.transform : null;
    }
}
