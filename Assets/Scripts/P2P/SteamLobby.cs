using System;
using Mirror;
using Steamworks;
using UnityEngine;

[DisallowMultipleComponent]
public class SteamLobby : MonoBehaviour
{
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    [Header("Room Server")]
    [SerializeField]
    private RoomServerService roomServerService;

    [SerializeField]
    [Min(2)]
    private int maxPlayers = 2;

    private CSteamID currentLobbyID;
    private bool joiningFromSteamInvite;
    private bool operationInProgress;
    private int pendingMaxPlayers = 2;

    public event Action<string> RoomCodeCreated;
    public event Action<string> StatusChanged;
    public event Action<string> ErrorOccurred;

    public string CurrentRoomCode => roomServerService?.CurrentRoomCode;
    public string CurrentStatus { get; private set; }

    private void Awake()
    {
        if (roomServerService == null)
            roomServerService = GetComponent<RoomServerService>();

        if (roomServerService == null)
            roomServerService = gameObject.AddComponent<RoomServerService>();
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            ReportError("Steam이 초기화되지 않았습니다.");
            return;
        }

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public void HostSteamLobby()
    {
        HostSteamLobby(maxPlayers);
    }

    public void HostPvpSteamLobby()
    {
        HostSteamLobby(4);
    }

    public void HostSteamLobby(int requestedMaxPlayers)
    {
        if (!CanUseRoomServer())
            return;

        if (operationInProgress)
        {
            ReportStatus("이미 방 생성 또는 참가를 처리하고 있습니다.");
            return;
        }

        if (NetworkManager.singleton == null)
        {
            ReportError("NetworkManager를 찾을 수 없습니다.");
            return;
        }

        joiningFromSteamInvite = false;
        operationInProgress = true;
        pendingMaxPlayers = Mathf.Clamp(
            requestedMaxPlayers,
            2,
            NetworkManager.singleton.maxConnections
        );

        ReportStatus("Steam 로비를 생성하는 중입니다...");
        SteamMatchmaking.CreateLobby(
            ELobbyType.k_ELobbyTypeInvisible,
            pendingMaxPlayers
        );
    }

    public void JoinRoomByCode(string roomCode)
    {
        if (!CanUseRoomServer())
            return;

        if (operationInProgress)
        {
            ReportStatus("이미 방 생성 또는 참가를 처리하고 있습니다.");
            return;
        }

        if (NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive)
        {
            ReportError("이미 멀티플레이 세션에 참가 중입니다.");
            return;
        }

        ReportStatus("방 코드를 확인하는 중입니다...");
        joiningFromSteamInvite = false;
        operationInProgress = true;

        roomServerService.JoinRoom(
            roomCode,
            steamLobbyId =>
            {
                ReportStatus("Steam 로비에 입장하는 중입니다...");
                SteamMatchmaking.JoinLobby(new CSteamID((ulong)steamLobbyId));
            },
            error =>
            {
                if (IsRoomNotFoundError(error))
                {
                    // TODO: 방 코드 오류 전용 안내 UI가 개발되면 임시 경고 처리를 교체한다.
                    ReportWarning(error);
                    return;
                }

                ReportError(error);
            }
        );
    }

    public void OpenInviteDialog()
    {
        if (currentLobbyID.IsValid())
        {
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyID);
            Debug.Log("친구 초대 창 열기!");
        }
        else
        {
            Debug.Log("아직 방을 파지 않았습니다!");
        }
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            ReportError($"Steam 로비 생성에 실패했습니다. ({callback.m_eResult})");
            return;
        }

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            "HostAddress",
            SteamUser.GetSteamID().ToString()
        );
        SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            "MaxPlayers",
            pendingMaxPlayers.ToString()
        );
        SteamMatchmaking.SetLobbyJoinable(currentLobbyID, true);
        NetworkManager.singleton.StartHost();

        roomServerService.CreateRoom(
            checked((long)callback.m_ulSteamIDLobby),
            roomCode =>
            {
                SteamMatchmaking.SetLobbyData(currentLobbyID, "RoomCode", roomCode);
                operationInProgress = false;
                ReportStatus($"방이 생성되었습니다. 코드: {roomCode}");
                RoomCodeCreated?.Invoke(roomCode);
            },
            error =>
            {
                ReportError(error);
                CloseSteamLobbyAndNetwork();
            }
        );
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        if (
            operationInProgress
            || (NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive)
        )
            return;

        joiningFromSteamInvite = true;
        operationInProgress = true;
        ReportStatus("Steam 초대로 받은 로비에 입장하는 중입니다...");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        EChatRoomEnterResponse enterResponse =
            (EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse;

        if (enterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            ReportError($"Steam 로비 입장에 실패했습니다. ({enterResponse})");
            roomServerService?.RollbackJoin();
            return;
        }

        if (NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive)
            return;

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        if (joiningFromSteamInvite && !roomServerService.IsInRoom)
        {
            string roomCode = SteamMatchmaking.GetLobbyData(currentLobbyID, "RoomCode");

            if (string.IsNullOrWhiteSpace(roomCode))
            {
                ReportError("초대받은 로비에서 방 코드를 찾을 수 없습니다.");
                SteamMatchmaking.LeaveLobby(currentLobbyID);
                return;
            }

            roomServerService.JoinRoom(
                roomCode,
                _ => ConnectMirrorClient(),
                error =>
                {
                    ReportError(error);
                    SteamMatchmaking.LeaveLobby(currentLobbyID);
                }
            );
            return;
        }

        ConnectMirrorClient();
    }

    public void LeaveCurrentRoom()
    {
        if (roomServerService == null)
        {
            CloseSteamLobbyAndNetwork();
            return;
        }

        roomServerService.ExitCurrentRoom(
            CloseSteamLobbyAndNetwork,
            error =>
            {
                Debug.LogWarning($"[SteamLobby] 서버 방 정리 실패: {error}");
                CloseSteamLobbyAndNetwork();
            }
        );
    }

    private void ConnectMirrorClient()
    {
        if (NetworkManager.singleton == null)
        {
            ReportError("NetworkManager를 찾을 수 없습니다.");
            roomServerService.RollbackJoin();
            return;
        }

        string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyID, "HostAddress");

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            ReportError("로비의 호스트 주소를 찾을 수 없습니다.");
            roomServerService.RollbackJoin(
                () => SteamMatchmaking.LeaveLobby(currentLobbyID)
            );
            return;
        }

        NetworkManager.singleton.networkAddress = hostAddress;
        NetworkManager.singleton.StartClient();
        operationInProgress = false;
        ReportStatus("멀티플레이 방에 입장했습니다.");
    }

    private bool CanUseRoomServer()
    {
        if (!SteamManager.Initialized)
        {
            ReportError("Steam이 초기화되지 않았습니다.");
            return false;
        }

        if (roomServerService != null)
            return true;

        ReportError("SteamLobby에 RoomServerService가 연결되지 않았습니다.");
        return false;
    }

    private void CloseSteamLobbyAndNetwork()
    {
        if (NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
                NetworkManager.singleton.StopHost();
            else if (NetworkClient.active)
                NetworkManager.singleton.StopClient();
            else if (NetworkServer.active)
                NetworkManager.singleton.StopServer();
        }

        if (currentLobbyID.IsValid())
            SteamMatchmaking.LeaveLobby(currentLobbyID);

        currentLobbyID = CSteamID.Nil;
        joiningFromSteamInvite = false;
        operationInProgress = false;
    }

    private void ReportStatus(string message)
    {
        CurrentStatus = message;
        Debug.Log($"[SteamLobby] {message}");
        StatusChanged?.Invoke(message);
    }

    private void ReportWarning(string message)
    {
        operationInProgress = false;
        CurrentStatus = message;
        Debug.LogWarning($"[SteamLobby] {message}");
        StatusChanged?.Invoke(message);
    }

    private void ReportError(string message)
    {
        operationInProgress = false;
        CurrentStatus = message;
        Debug.LogError($"[SteamLobby] {message}");
        ErrorOccurred?.Invoke(message);
    }

    private static bool IsRoomNotFoundError(string message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && message.StartsWith("ROOM_NOT_FOUND", StringComparison.OrdinalIgnoreCase);
    }
}
