using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EndPoint",
    menuName = "PushPull/Server/End Point",
    order = 0
)]
public sealed class EndPointSO : ScriptableObject
{
    [Header("Server")]
    [SerializeField]
    private string baseUrl = "https://pupul.https.gsmsv.site/api/v1";

    public string BaseUrl => baseUrl.TrimEnd('/');

    public string Login => "/auth/login";
    public string Logout => "/auth/logout";
    public string CreateRoom => "/room";
    public string GetAllRooms => "/room/all";

    public string GetRoom(string roomCode)
    {
        return $"/room/{Uri.EscapeDataString(NormalizeRoomCode(roomCode))}";
    }

    public string JoinRoom(string roomCode)
    {
        return $"{GetRoom(roomCode)}/join";
    }

    public string LeaveRoom(string roomCode)
    {
        return $"{GetRoom(roomCode)}/leave";
    }

    public string Heartbeat(string roomCode)
    {
        return $"{GetRoom(roomCode)}/heartbeat";
    }

    public string ReconnectRoom(string roomCode)
    {
        return $"{GetRoom(roomCode)}/reconnect";
    }

    public string CloseRoom(string roomCode)
    {
        return GetRoom(roomCode);
    }

    public static string NormalizeRoomCode(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            throw new ArgumentException("방 코드를 입력해주세요.", nameof(roomCode));

        return roomCode.Trim().ToUpperInvariant();
    }
}
