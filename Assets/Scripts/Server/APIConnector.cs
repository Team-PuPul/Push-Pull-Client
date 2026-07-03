using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public sealed class APIConnector : MonoBehaviour
{
    public static APIConnector instance;

    [SerializeField]
    private EndPointSO endpointSO;

    public EndPointSO Endpoints => endpointSO;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            if (endpointSO == null)
            {
                endpointSO = ScriptableObject.CreateInstance<EndPointSO>();
                endpointSO.hideFlags = HideFlags.DontSave;
                Debug.LogWarning(
                    "[APIConnector] EndPointSO가 연결되지 않아 기본 개발 서버 URL을 사용합니다."
                );
            }
        }
        else if (instance != this)
        {
            Destroy(this);
        }
    }

    public void Get<T>(
        string endpoint,
        Action<T> onSuccess,
        Action<string> onError = null,
        bool needSession = false
    )
    {
        StartCoroutine(GetCoroutine(endpoint, onSuccess, onError, needSession));
    }

    public IEnumerator GetCoroutine<T>(
        string endpoint,
        Action<T> onSuccess,
        Action<string> onError = null,
        bool needSession = false
    )
    {
        UnityWebRequest request = CreateRequest(endpoint, UnityWebRequest.kHttpVerbGET);
        yield return SendRequest(request, onSuccess, onError, needSession);
    }

    public void Post<T>(
        string endpoint,
        object body,
        Action<T> onSuccess,
        Action<string> onError = null,
        bool needSession = false
    )
    {
        StartCoroutine(PostCoroutine(endpoint, body, onSuccess, onError, needSession));
    }

    public IEnumerator PostCoroutine<T>(
        string endpoint,
        object body,
        Action<T> onSuccess,
        Action<string> onError = null,
        bool needSession = false
    )
    {
        UnityWebRequest request = CreateRequest(
            endpoint,
            UnityWebRequest.kHttpVerbPOST,
            SerializeBody(body)
        );

        yield return SendRequest(request, onSuccess, onError, needSession);
    }

    public void Patch<T>(
        string endpoint,
        object body,
        Action<T> onSuccess,
        Action<string> onError = null,
        bool needSession = false
    )
    {
        StartCoroutine(PatchCoroutine(endpoint, body, onSuccess, onError, needSession));
    }

    public IEnumerator PatchCoroutine<T>(
        string endpoint,
        object body,
        Action<T> onSuccess,
        Action<string> onError = null,
        bool needSession = false
    )
    {
        UnityWebRequest request = CreateRequest(endpoint, "PATCH", SerializeBody(body));
        yield return SendRequest(request, onSuccess, onError, needSession);
    }

    public void Delete<T>(
        string endpoint,
        Action<T> onSuccess,
        Action<string> onError = null,
        bool needSession = false
    )
    {
        StartCoroutine(DeleteCoroutine(endpoint, onSuccess, onError, needSession));
    }

    public IEnumerator DeleteCoroutine<T>(
        string endpoint,
        Action<T> onSuccess,
        Action<string> onError = null,
        bool needSession = false
    )
    {
        UnityWebRequest request = CreateRequest(endpoint, UnityWebRequest.kHttpVerbDELETE);
        yield return SendRequest(request, onSuccess, onError, needSession);
    }

    private IEnumerator SendRequest<T>(
        UnityWebRequest request,
        Action<T> onSuccess,
        Action<string> onError,
        bool needSession
    )
    {
        using (request)
        {
            if (needSession && !PlayerPrefs.HasKey("sessionId"))
            {
                onError?.Invoke("401\n로그인이 필요합니다. sessionId가 없습니다.");
                yield break;
            }

            ConfigureRequest(request, needSession);
            yield return request.SendWebRequest();
            HandleResponse(request, onSuccess, onError);
        }
    }

    private UnityWebRequest CreateRequest(string endpoint, string method, string json = null)
    {
        UnityWebRequest request = new UnityWebRequest(endpointSO.BaseUrl + endpoint, method);

        request.downloadHandler = new DownloadHandlerBuffer();

        if (json != null)
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));

        return request;
    }

    private static void ConfigureRequest(UnityWebRequest request, bool needSession)
    {
        request.timeout = 10;
        request.SetRequestHeader("Accept", "application/json");

        if (request.uploadHandler != null)
            request.SetRequestHeader("Content-Type", "application/json");

        if (needSession)
            request.SetRequestHeader("Session-Id", PlayerPrefs.GetString("sessionId"));
    }

    private static string SerializeBody(object body)
    {
        return body == null ? null : JsonConvert.SerializeObject(body);
    }

    private static void HandleResponse<T>(
        UnityWebRequest request,
        Action<T> onSuccess,
        Action<string> onError
    )
    {
        string responseBody = request.downloadHandler?.text;

        if (request.result != UnityWebRequest.Result.Success)
        {
            string errorBody = string.IsNullOrWhiteSpace(responseBody)
                ? request.error
                : responseBody;

            onError?.Invoke($"{request.responseCode}\n{errorBody}");
            return;
        }

        if (request.responseCode == 204 || string.IsNullOrWhiteSpace(responseBody))
        {
            onSuccess?.Invoke(default);
            return;
        }

        try
        {
            T result = JsonConvert.DeserializeObject<T>(responseBody);
            onSuccess?.Invoke(result);
        }
        catch (Exception exception)
        {
            onError?.Invoke($"{request.responseCode}\nJSON 변환 실패: {exception.Message}");
        }
    }
}
