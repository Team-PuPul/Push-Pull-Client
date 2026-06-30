using System.Collections;
using System.Text;
using Steamworks;
using UnityEngine;

public sealed class SteamAuthService : MonoBehaviour
{
    private const string SessionIdKey = "sessionId";
    private const float SteamInitializeTimeout = 10f;

    [SerializeField]
    private APIConnector apiConnector;

    private Callback<GetTicketForWebApiResponse_t> ticketCallback;
    private HAuthTicket authTicket = HAuthTicket.Invalid;
    private bool isLoggingIn;

    public bool IsLoggedIn =>
        PlayerPrefs.HasKey(SessionIdKey)
        && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(SessionIdKey));

    public bool IsLoggingIn => isLoggingIn;

    private IEnumerator Start()
    {
        float elapsed = 0f;

        while (!SteamManager.Initialized && elapsed < SteamInitializeTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamAuth] Steam 초기화에 실패했습니다.");
            yield break;
        }

        if (apiConnector == null)
            apiConnector = GetComponent<APIConnector>();

        if (apiConnector == null)
        {
            Debug.LogError("[SteamAuth] APIConnector를 찾을 수 없습니다.");
            yield break;
        }

        Login();
    }

    public void Login()
    {
        if (isLoggingIn)
            return;

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamAuth] Steam이 초기화되지 않았습니다.");
            return;
        }

        if (apiConnector == null)
        {
            apiConnector = GetComponent<APIConnector>();

            if (apiConnector == null)
            {
                Debug.LogError("[SteamAuth] APIConnector를 찾을 수 없습니다.");
                return;
            }
        }

        EnsureTicketCallback();

        isLoggingIn = true;

        PlayerPrefs.DeleteKey(SessionIdKey);
        PlayerPrefs.Save();

        // HTTP 백엔드의 AuthenticateUserTicket 검증용 티켓
        authTicket = SteamUser.GetAuthTicketForWebApi(string.Empty);

        if (authTicket == HAuthTicket.Invalid)
        {
            Fail("Steam 인증 티켓 요청에 실패했습니다.");
            return;
        }

        Debug.Log("[SteamAuth] Steam 인증 티켓 요청 완료");
    }

    private void EnsureTicketCallback()
    {
        if (ticketCallback != null)
            return;

        ticketCallback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicketReceived);
    }

    private void OnTicketReceived(GetTicketForWebApiResponse_t callback)
    {
        if (!isLoggingIn)
            return;

        if (callback.m_hAuthTicket != authTicket)
            return;

        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Fail($"Steam 티켓 발급 실패: {callback.m_eResult}");
            return;
        }

        if (
            callback.m_rgubTicket == null
            || callback.m_cubTicket <= 0
            || callback.m_cubTicket > callback.m_rgubTicket.Length
        )
        {
            Fail("Steam에서 빈 인증 티켓을 반환했습니다.");
            return;
        }

        string steamTicket = ToHex(callback.m_rgubTicket, callback.m_cubTicket);

        string nickname = SteamFriends.GetPersonaName()?.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            nickname = SteamUser.GetSteamID().ToString();
        }

        Debug.Log(
            $"[SteamAuth] ticketBytes={callback.m_cubTicket}, "
                + $"hexLength={steamTicket.Length}, "
                + $"nickname={nickname}"
        );

        LoginRequest request = new LoginRequest { SteamTicket = steamTicket, Nickname = nickname };

        apiConnector.Post<CommonApiResponse<LoginResponse>>(
            apiConnector.Endpoints.Login,
            request,
            OnLoginSuccess,
            Fail
        );
    }

    private void OnLoginSuccess(CommonApiResponse<LoginResponse> response)
    {
        string sessionId = response?.Data?.SessionId;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Fail("서버가 sessionId를 반환하지 않았습니다.");
            return;
        }

        PlayerPrefs.SetString(SessionIdKey, sessionId);
        PlayerPrefs.Save();

        isLoggingIn = false;
        CancelTicket();

        Debug.Log("[SteamAuth] 로그인 성공");
    }

    private void Fail(string error)
    {
        isLoggingIn = false;

        PlayerPrefs.DeleteKey(SessionIdKey);
        PlayerPrefs.Save();

        CancelTicket();

        Debug.LogError($"[SteamAuth] {error}");
    }

    private void CancelTicket()
    {
        // 티켓이 없다면 SteamManager.Initialized에 접근하지 않는다.
        if (authTicket == HAuthTicket.Invalid)
            return;

        if (SteamManager.Initialized)
        {
            SteamUser.CancelAuthTicket(authTicket);
        }

        authTicket = HAuthTicket.Invalid;
    }

    private static string ToHex(byte[] bytes, int length)
    {
        StringBuilder builder = new StringBuilder(length * 2);

        for (int i = 0; i < length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }

        return builder.ToString();
    }

    private void OnDisable()
    {
        // SteamManager의 안내대로 OnDestroy에서는
        // Steamworks API를 호출하지 않는다.
        CancelTicket();

        ticketCallback?.Dispose();
        ticketCallback = null;
    }
}
