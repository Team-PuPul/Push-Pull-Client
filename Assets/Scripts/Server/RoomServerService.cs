using System;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine;

public sealed class RoomServerService : MonoBehaviour
{
    private const string DefaultRoomName = "PushPull Room";

    [SerializeField]
    private APIConnector apiConnector;

    [SerializeField]
    [Min(5f)]
    private float heartbeatInterval = 15f;

    private Coroutine heartbeatCoroutine;

    public string CurrentRoomCode { get; private set; }
    public long CurrentSteamLobbyId { get; private set; }
    public bool IsHost { get; private set; }
    public bool IsInRoom => !string.IsNullOrEmpty(CurrentRoomCode);

    private void Awake()
    {
        if (apiConnector == null)
            apiConnector = GetComponent<APIConnector>();

        if (apiConnector == null)
            apiConnector = APIConnector.instance;

        if (apiConnector == null)
            apiConnector = gameObject.AddComponent<APIConnector>();
    }

    public void CreateRoom(long lobbyId, Action<string> onSuccess, Action<string> onError)
    {
        if (!TryGetConnector(onError))
            return;

        CreateRoomRequest request = new CreateRoomRequest
        {
            LobbyId = lobbyId,
            RoomName = DefaultRoomName,
            IsPrivate = true,
            Password = null,
        };

        apiConnector.Post<CommonApiResponse<CreateRoomResponse>>(
            apiConnector.Endpoints.CreateRoom,
            request,
            response =>
            {
                if (response?.Data == null || string.IsNullOrWhiteSpace(response.Data.RoomCode))
                {
                    onError?.Invoke("서버가 방 코드를 반환하지 않았습니다.");
                    return;
                }

                CurrentRoomCode = response.Data.RoomCode.Trim().ToUpperInvariant();
                CurrentSteamLobbyId = lobbyId;
                IsHost = true;
                StartHeartbeat();
                onSuccess?.Invoke(CurrentRoomCode);
            },
            error => onError?.Invoke(ParseError(error)),
            true
        );
    }

    public void JoinRoom(string roomCode, Action<long> onSuccess, Action<string> onError)
    {
        if (!TryGetConnector(onError))
            return;

        string normalizedCode;

        try
        {
            normalizedCode = EndPointSO.NormalizeRoomCode(roomCode);
        }
        catch (ArgumentException exception)
        {
            onError?.Invoke(exception.Message);
            return;
        }

        JoinRoomRequest request = new JoinRoomRequest
        {
            RoomCode = normalizedCode,
            Password = null,
        };

        apiConnector.Post<CommonApiResponse<JoinRoomResponse>>(
            apiConnector.Endpoints.JoinRoom(normalizedCode),
            request,
            response =>
            {
                if (response?.Data == null || response.Data.SteamLobbyId <= 0)
                {
                    onError?.Invoke("서버가 Steam 로비 ID를 반환하지 않았습니다.");
                    return;
                }

                CurrentRoomCode = normalizedCode;
                CurrentSteamLobbyId = response.Data.SteamLobbyId;
                IsHost = false;
                StartHeartbeat();
                onSuccess?.Invoke(CurrentSteamLobbyId);
            },
            error => onError?.Invoke(ParseError(error)),
            true
        );
    }

    public void GetRoom(string roomCode, Action<GetRoomResponse> onSuccess, Action<string> onError)
    {
        if (!TryGetConnector(onError))
            return;

        apiConnector.Get<CommonApiResponse<GetRoomResponse>>(
            apiConnector.Endpoints.GetRoom(roomCode),
            response => onSuccess?.Invoke(response?.Data),
            error => onError?.Invoke(ParseError(error))
        );
    }

    public void Reconnect(
        string roomCode,
        Action<ReconnectRoomResponse> onSuccess,
        Action<string> onError
    )
    {
        if (!TryGetConnector(onError))
            return;

        apiConnector.Post<CommonApiResponse<ReconnectRoomResponse>>(
            apiConnector.Endpoints.ReconnectRoom(roomCode),
            null,
            response =>
            {
                if (response?.Data == null)
                {
                    onError?.Invoke("재접속 정보를 받지 못했습니다.");
                    return;
                }

                CurrentRoomCode = roomCode.Trim().ToUpperInvariant();
                CurrentSteamLobbyId = response.Data.SteamLobbyId;
                IsHost = string.Equals(
                    response.Data.Role,
                    "host",
                    StringComparison.OrdinalIgnoreCase
                );
                StartHeartbeat();
                onSuccess?.Invoke(response.Data);
            },
            error => onError?.Invoke(ParseError(error)),
            true
        );
    }

    public void ExitCurrentRoom(Action onComplete = null, Action<string> onError = null)
    {
        if (!IsInRoom)
        {
            onComplete?.Invoke();
            return;
        }

        if (!TryGetConnector(onError))
            return;

        string endpoint = IsHost
            ? apiConnector.Endpoints.CloseRoom(CurrentRoomCode)
            : apiConnector.Endpoints.LeaveRoom(CurrentRoomCode);

        Action<CommonApiResponse<object>> success = _ =>
        {
            ClearRoomState();
            onComplete?.Invoke();
        };

        Action<string> failure = error =>
        {
            string parsedError = ParseError(error);
            ClearRoomState();
            onError?.Invoke(parsedError);
        };

        if (IsHost)
            apiConnector.Delete(endpoint, success, failure, true);
        else
            apiConnector.Post(endpoint, null, success, failure, true);
    }

    public void RollbackJoin(Action onComplete = null)
    {
        if (!IsInRoom || IsHost)
        {
            ClearRoomState();
            onComplete?.Invoke();
            return;
        }

        ExitCurrentRoom(onComplete, _ => onComplete?.Invoke());
    }

    public void ResetLocalState()
    {
        ClearRoomState();
    }

    private bool TryGetConnector(Action<string> onError)
    {
        if (apiConnector == null)
            apiConnector = APIConnector.instance;

        if (apiConnector != null && apiConnector.Endpoints != null)
            return true;

        onError?.Invoke("RoomServerService에 APIConnector 또는 EndPointSO가 연결되지 않았습니다.");
        return false;
    }

    private void StartHeartbeat()
    {
        if (heartbeatCoroutine != null)
            StopCoroutine(heartbeatCoroutine);

        heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
    }

    private IEnumerator HeartbeatLoop()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(heartbeatInterval);

        while (IsInRoom)
        {
            yield return wait;

            if (!IsInRoom || apiConnector == null)
                yield break;

            apiConnector.Post<CommonApiResponse<object>>(
                apiConnector.Endpoints.Heartbeat(CurrentRoomCode),
                null,
                _ => { },
                error => Debug.LogWarning($"[RoomServer] 하트비트 실패: {ParseError(error)}"),
                true
            );
        }
    }

    private void ClearRoomState()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }

        CurrentRoomCode = null;
        CurrentSteamLobbyId = 0;
        IsHost = false;
    }

    private static string ParseError(string rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
            return "알 수 없는 서버 오류가 발생했습니다.";

        int lineBreak = rawError.IndexOf('\n');
        string body = lineBreak >= 0 ? rawError.Substring(lineBreak + 1) : rawError;

        try
        {
            CommonApiResponse<object> response = JsonConvert.DeserializeObject<
                CommonApiResponse<object>
            >(body);

            if (!string.IsNullOrWhiteSpace(response?.Message))
                return response.Message;
        }
        catch (JsonException)
        {
            // JSON이 아닌 네트워크 오류는 원문을 사용한다.
        }

        return body;
    }
}
