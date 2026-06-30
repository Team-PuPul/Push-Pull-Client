using System;
using Newtonsoft.Json;

[Serializable]
public sealed class CommonApiResponse<T>
{
    [JsonProperty("status")]
    public string Status;

    [JsonProperty("code")]
    public int Code;

    [JsonProperty("message")]
    public string Message;

    [JsonProperty("data")]
    public T Data;
}

[Serializable]
public sealed class CreateRoomRequest
{
    [JsonProperty("lobbyId")]
    public long LobbyId;

    [JsonProperty("roomName")]
    public string RoomName;

    [JsonProperty("isPrivate")]
    public bool IsPrivate;

    [JsonProperty("password")]
    public string Password;
}

[Serializable]
public sealed class CreateRoomResponse
{
    [JsonProperty("roomCode")]
    public string RoomCode;
}

[Serializable]
public sealed class JoinRoomRequest
{
    [JsonProperty("roomCode")]
    public string RoomCode;

    [JsonProperty("password")]
    public string Password;
}

[Serializable]
public sealed class JoinRoomResponse
{
    [JsonProperty("steamLobbyId")]
    public long SteamLobbyId;
}

[Serializable]
public sealed class GetRoomResponse
{
    [JsonProperty("roomCode")]
    public string RoomCode;

    [JsonProperty("roomName")]
    public string RoomName;

    [JsonProperty("currentPlayers")]
    public int CurrentPlayers;

    [JsonProperty("maxPlayers")]
    public int MaxPlayers;

    [JsonProperty("isPrivate")]
    public bool IsPrivate;

    [JsonProperty("hasPassword")]
    public bool HasPassword;
}

[Serializable]
public sealed class ReconnectRoomResponse
{
    [JsonProperty("steamLobbyId")]
    public long SteamLobbyId;

    [JsonProperty("role")]
    public string Role;
}
