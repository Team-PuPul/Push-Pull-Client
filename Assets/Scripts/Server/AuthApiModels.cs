using System;
using Newtonsoft.Json;

[Serializable]
public sealed class LoginRequest
{
    [JsonProperty("steamTicket")]
    public string SteamTicket;

    [JsonProperty("nickname")]
    public string Nickname;
}

[Serializable]
public sealed class LoginResponse
{
    [JsonProperty("sessionId")]
    public string SessionId;
}
