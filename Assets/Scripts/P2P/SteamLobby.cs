using System;
using Mirror;
using Steamworks;
using UnityEngine;

[DisallowMultipleComponent]
public class SteamLobby : MonoBehaviour
{
    private const string RoomCodeKey = "RoomCode";
    private const string HostAddressKey = "HostAddress";
    private const string MaxPlayersKey = "MaxPlayers";
    private const string BuildVersionKey = "BuildVersion";
    private const string RoomCodeCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected CallResult<LobbyMatchList_t> lobbyMatchListResult;

    [Header("Room Code")]
    [SerializeField]
    [Range(6, 10)]
    private int roomCodeLength = 8;

    [SerializeField]
    [Min(1)]
    private int maxRoomCodeCreateAttempts = 10;

    [Header("Network Capacity")]
    [SerializeField]
    [Min(2)]
    private int maxPlayers = 2;

    private CSteamID currentLobbyID;
    private LobbySearchPurpose lobbySearchPurpose;
    private string currentRoomCode;
    private string pendingRoomCode;
    private string pendingJoinRoomCode;
    private bool creatingSteamLobbyAsHost;
    private bool joiningFromSteamInvite;
    private bool operationInProgress;
    private int pendingMaxPlayers = 2;
    private int roomCodeCreateAttemptCount;

    public event Action<string> RoomCodeCreated;
    public event Action<string> StatusChanged;
    public event Action<string> ErrorOccurred;

    private enum LobbySearchPurpose
    {
        None,
        DuplicateCheckBeforeCreate,
        DuplicateCheckAfterCreate,
        JoinByRoomCode,
    }

    public string CurrentRoomCode => currentRoomCode;

    public string CurrentStatus { get; private set; }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            ReportError("Steam이 초기화되지 않았습니다.");
            return;
        }

        EnsureSteamCallbacks();
    }

    private void OnDestroy()
    {
        lobbyCreated?.Dispose();
        gameLobbyJoinRequested?.Dispose();
        lobbyEntered?.Dispose();
        lobbyMatchListResult?.Dispose();

        lobbyCreated = null;
        gameLobbyJoinRequested = null;
        lobbyEntered = null;
        lobbyMatchListResult = null;
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
        if (!CanUseSteamLobby())
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

        if (NetworkManager.singleton.isNetworkActive)
        {
            ReportError("이미 멀티플레이 세션에 참가 중입니다.");
            return;
        }

        joiningFromSteamInvite = false;
        operationInProgress = true;
        roomCodeCreateAttemptCount = 0;

        pendingMaxPlayers = Mathf.Clamp(
            requestedMaxPlayers,
            2,
            NetworkManager.singleton.maxConnections
        );

        TryGenerateUniqueRoomCode();
    }

    public void JoinRoomByCode(string roomCode)
    {
        if (!CanUseSteamLobby())
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

        if (!TryNormalizeRoomCode(roomCode, out string normalizedRoomCode))
        {
            ReportError("방 코드를 입력해주세요.");
            return;
        }

        pendingJoinRoomCode = normalizedRoomCode;
        joiningFromSteamInvite = false;
        operationInProgress = true;

        ReportStatus("Steam 로비에서 방 코드를 검색하는 중입니다...");

        RequestLobbySearch(
            LobbySearchPurpose.JoinByRoomCode,
            pendingJoinRoomCode,
            requireOpenSlots: true
        );
    }

    public void OpenInviteDialog()
    {
        if (currentLobbyID.IsValid())
        {
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyID);
            Debug.Log("[SteamLobby] 친구 초대 창을 열었습니다.");
        }
        else
        {
            Debug.Log("[SteamLobby] 아직 초대할 Steam 로비가 없습니다.");
        }
    }

    public void LeaveCurrentRoom()
    {
        CloseSteamLobbyAndNetwork();
    }

    public void HandleUnexpectedClientDisconnect()
    {
        if (currentLobbyID.IsValid() && SteamManager.Initialized)
            SteamMatchmaking.LeaveLobby(currentLobbyID);

        ClearLocalLobbyState();

        Debug.LogWarning("[SteamLobby] 호스트 연결 종료로 게스트의 로비 상태를 초기화했습니다.");
    }

    private void TryGenerateUniqueRoomCode()
    {
        roomCodeCreateAttemptCount++;

        if (roomCodeCreateAttemptCount > maxRoomCodeCreateAttempts)
        {
            ReportError("사용 가능한 방 코드를 찾지 못했습니다. 잠시 후 다시 시도해주세요.");
            return;
        }

        pendingRoomCode = GenerateRoomCode();

        ReportStatus("방 코드 중복을 확인하는 중입니다...");

        RequestLobbySearch(
            LobbySearchPurpose.DuplicateCheckBeforeCreate,
            pendingRoomCode,
            requireOpenSlots: false
        );
    }

    private string GenerateRoomCode()
    {
        int length = Mathf.Max(1, roomCodeLength);
        char[] code = new char[length];

        for (int i = 0; i < code.Length; i++)
        {
            int characterIndex = UnityEngine.Random.Range(0, RoomCodeCharacters.Length);
            code[i] = RoomCodeCharacters[characterIndex];
        }

        return new string(code);
    }

    private void RequestLobbySearch(
        LobbySearchPurpose purpose,
        string roomCode,
        bool requireOpenSlots
    )
    {
        lobbySearchPurpose = purpose;

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(
            ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide
        );

        SteamMatchmaking.AddRequestLobbyListStringFilter(
            RoomCodeKey,
            roomCode,
            ELobbyComparison.k_ELobbyComparisonEqual
        );

        SteamMatchmaking.AddRequestLobbyListStringFilter(
            BuildVersionKey,
            GetBuildVersion(),
            ELobbyComparison.k_ELobbyComparisonEqual
        );

        if (requireOpenSlots)
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);

        SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);

        SteamAPICall_t apiCall = SteamMatchmaking.RequestLobbyList();

        if (apiCall == SteamAPICall_t.Invalid)
        {
            lobbySearchPurpose = LobbySearchPurpose.None;
            ReportError("Steam 로비 검색 요청에 실패했습니다.");
            return;
        }

        lobbyMatchListResult.Set(apiCall);
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback, bool ioFailure)
    {
        if (ioFailure)
        {
            LobbySearchPurpose failedPurpose = lobbySearchPurpose;
            lobbySearchPurpose = LobbySearchPurpose.None;
            HandleLobbySearchFailure(failedPurpose);
            return;
        }

        LobbySearchPurpose completedPurpose = lobbySearchPurpose;
        lobbySearchPurpose = LobbySearchPurpose.None;

        switch (completedPurpose)
        {
            case LobbySearchPurpose.DuplicateCheckBeforeCreate:
                HandleDuplicateCheckBeforeCreate(callback);
                break;

            case LobbySearchPurpose.DuplicateCheckAfterCreate:
                HandleDuplicateCheckAfterCreate(callback);
                break;

            case LobbySearchPurpose.JoinByRoomCode:
                HandleJoinLobbySearch(callback);
                break;
        }
    }

    private void HandleDuplicateCheckBeforeCreate(LobbyMatchList_t callback)
    {
        if (callback.m_nLobbiesMatching > 0)
        {
            TryGenerateUniqueRoomCode();
            return;
        }

        ReportStatus("Steam 로비를 생성하는 중입니다...");

        creatingSteamLobbyAsHost = true;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeInvisible, pendingMaxPlayers);
    }

    private void HandleDuplicateCheckAfterCreate(LobbyMatchList_t callback)
    {
        if (HasDuplicateLobby(callback))
        {
            Debug.LogWarning(
                $"[SteamLobby] 방 코드 충돌 감지. code={currentRoomCode}, lobby={currentLobbyID}"
            );

            if (currentLobbyID.IsValid())
                SteamMatchmaking.LeaveLobby(currentLobbyID);

            currentLobbyID = CSteamID.Nil;
            currentRoomCode = null;
            creatingSteamLobbyAsHost = false;
            TryGenerateUniqueRoomCode();
            return;
        }

        StartMirrorHost();
    }

    private bool HasDuplicateLobby(LobbyMatchList_t callback)
    {
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);

            if (lobbyID.IsValid() && lobbyID != currentLobbyID)
                return true;
        }

        return false;
    }

    private void HandleJoinLobbySearch(LobbyMatchList_t callback)
    {
        CSteamID selectedLobby = CSteamID.Nil;
        int validLobbyCount = 0;

        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);

            if (!IsJoinableRoomCodeLobby(lobbyID, pendingJoinRoomCode))
                continue;

            selectedLobby = lobbyID;
            validLobbyCount++;
        }

        if (validLobbyCount == 0)
        {
            ReportWarning("해당 방 코드를 찾을 수 없습니다.");
            return;
        }

        if (validLobbyCount > 1)
        {
            ReportError("같은 방 코드의 Steam 로비가 여러 개 발견되었습니다. 다시 시도해주세요.");
            return;
        }

        ReportStatus("Steam 로비에 입장하는 중입니다...");

        SteamMatchmaking.JoinLobby(selectedLobby);
    }

    private bool IsJoinableRoomCodeLobby(CSteamID lobbyID, string roomCode)
    {
        if (!lobbyID.IsValid())
            return false;

        string lobbyRoomCode = SteamMatchmaking.GetLobbyData(lobbyID, RoomCodeKey);
        string hostAddress = SteamMatchmaking.GetLobbyData(lobbyID, HostAddressKey);
        string buildVersion = SteamMatchmaking.GetLobbyData(lobbyID, BuildVersionKey);

        return string.Equals(lobbyRoomCode, roomCode, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(hostAddress)
            && string.Equals(buildVersion, GetBuildVersion(), StringComparison.Ordinal);
    }

    private void HandleLobbySearchFailure(LobbySearchPurpose failedPurpose)
    {
        if (failedPurpose == LobbySearchPurpose.DuplicateCheckAfterCreate && currentLobbyID.IsValid())
            LeaveCurrentSteamLobby();

        ReportError("Steam 로비 검색에 실패했습니다.");
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            creatingSteamLobbyAsHost = false;
            pendingRoomCode = null;
            ReportError($"Steam 로비 생성에 실패했습니다. ({callback.m_eResult})");
            return;
        }

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        currentRoomCode = pendingRoomCode;
        SteamMatchmaking.SetLobbyJoinable(currentLobbyID, false);

        if (!TrySetLobbyData())
        {
            ReportError("Steam 로비 데이터 설정에 실패했습니다.");
            CloseSteamLobbyAndNetwork();
            return;
        }

        RequestLobbySearch(
            LobbySearchPurpose.DuplicateCheckAfterCreate,
            currentRoomCode,
            requireOpenSlots: false
        );
    }

    private bool TrySetLobbyData()
    {
        bool success = true;

        success &= SteamMatchmaking.SetLobbyData(currentLobbyID, RoomCodeKey, currentRoomCode);

        success &= SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            HostAddressKey,
            SteamUser.GetSteamID().ToString()
        );

        success &= SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            MaxPlayersKey,
            pendingMaxPlayers.ToString()
        );

        success &= SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            BuildVersionKey,
            GetBuildVersion()
        );

        return success;
    }

    private void StartMirrorHost()
    {
        if (NetworkManager.singleton == null)
        {
            ReportError("NetworkManager를 찾을 수 없습니다.");
            CloseSteamLobbyAndNetwork();
            return;
        }

        NetworkManager.singleton.StartHost();
        SteamMatchmaking.SetLobbyJoinable(currentLobbyID, true);

        creatingSteamLobbyAsHost = false;
        operationInProgress = false;
        pendingRoomCode = null;

        ReportStatus($"방이 생성되었습니다. 코드: {currentRoomCode}");
        RoomCodeCreated?.Invoke(currentRoomCode);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        if (
            operationInProgress
            || (NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive)
        )
        {
            return;
        }

        joiningFromSteamInvite = true;
        operationInProgress = true;

        ReportStatus("Steam 초대로 받은 로비에 입장하는 중입니다...");

        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        EChatRoomEnterResponse enterResponse = (EChatRoomEnterResponse)
            callback.m_EChatRoomEnterResponse;

        if (enterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            ReportError($"Steam 로비 입장에 실패했습니다. ({enterResponse})");
            return;
        }

        if (NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive)
            return;

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        if (creatingSteamLobbyAsHost)
            return;

        currentRoomCode = SteamMatchmaking.GetLobbyData(currentLobbyID, RoomCodeKey);

        if (string.IsNullOrWhiteSpace(currentRoomCode))
        {
            ReportError("Steam 로비에서 방 코드를 찾을 수 없습니다.");
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            ClearLocalLobbyState();
            return;
        }

        if (joiningFromSteamInvite)
            RoomCodeCreated?.Invoke(currentRoomCode);

        ConnectMirrorClient();
    }

    private void ConnectMirrorClient()
    {
        if (NetworkManager.singleton == null)
        {
            ReportError("NetworkManager를 찾을 수 없습니다.");
            LeaveCurrentSteamLobby();
            return;
        }

        string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyID, HostAddressKey);

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            ReportError("Steam 로비에서 호스트 주소를 찾을 수 없습니다.");
            LeaveCurrentSteamLobby();
            return;
        }

        NetworkManager.singleton.networkAddress = hostAddress;
        NetworkManager.singleton.StartClient();

        operationInProgress = false;
        pendingJoinRoomCode = null;
        joiningFromSteamInvite = false;

        ReportStatus("멀티플레이 방에 입장했습니다.");
    }

    private bool CanUseSteamLobby()
    {
        if (!SteamManager.Initialized)
        {
            ReportError("Steam이 초기화되지 않았습니다.");
            return false;
        }

        EnsureSteamCallbacks();

        return true;
    }

    private void EnsureSteamCallbacks()
    {
        if (lobbyCreated == null)
            lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);

        if (gameLobbyJoinRequested == null)
        {
            gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(
                OnGameLobbyJoinRequested
            );
        }

        if (lobbyEntered == null)
            lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);

        if (lobbyMatchListResult == null)
            lobbyMatchListResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }

    private void CloseSteamLobbyAndNetwork()
    {
        if (NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopHost();
            }
            else if (NetworkClient.active)
            {
                NetworkManager.singleton.StopClient();
            }
            else if (NetworkServer.active)
            {
                NetworkManager.singleton.StopServer();
            }
        }

        LeaveCurrentSteamLobby();
    }

    private void LeaveCurrentSteamLobby()
    {
        if (currentLobbyID.IsValid() && SteamManager.Initialized)
            SteamMatchmaking.LeaveLobby(currentLobbyID);

        ClearLocalLobbyState();
    }

    private void ClearLocalLobbyState()
    {
        currentLobbyID = CSteamID.Nil;
        currentRoomCode = null;
        pendingRoomCode = null;
        pendingJoinRoomCode = null;
        creatingSteamLobbyAsHost = false;
        joiningFromSteamInvite = false;
        operationInProgress = false;
        lobbySearchPurpose = LobbySearchPurpose.None;
        roomCodeCreateAttemptCount = 0;
    }

    private static bool TryNormalizeRoomCode(string roomCode, out string normalizedRoomCode)
    {
        normalizedRoomCode = null;

        if (string.IsNullOrWhiteSpace(roomCode))
            return false;

        normalizedRoomCode = roomCode.Trim().ToUpperInvariant();
        return true;
    }

    private static string GetBuildVersion()
    {
        return string.IsNullOrWhiteSpace(Application.version) ? "dev" : Application.version;
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
}
