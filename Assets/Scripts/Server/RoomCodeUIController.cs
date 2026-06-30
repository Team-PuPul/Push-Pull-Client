using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoomCodeUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private SteamLobby steamLobby;

    [Header("Room Code")]
    [SerializeField]
    private TMP_InputField roomCodeInput;

    [Header("Output")]
    [SerializeField]
    private TMP_Text createdRoomCodeText;

    [SerializeField]
    private TMP_Text statusText;

    [Header("Copy Room Code")]
    [SerializeField]
    private Button copyRoomCodeButton;

    [SerializeField]
    private TMP_Text copyFeedbackText;

    [SerializeField]
    [Min(0.5f)]
    private float copyFeedbackDuration = 2f;

    private bool isSubscribed;
    private string currentRoomCode;
    private Coroutine copyFeedbackCoroutine;

    private void Awake()
    {
        ResolveSteamLobby();
        RefreshCopyButton();

        if (copyFeedbackText != null)
            copyFeedbackText.text = string.Empty;
    }

    private void OnEnable()
    {
        BindSteamLobby();
        RefreshCopyButton();
    }

    private void Start()
    {
        // UI의 Awake가 NetworkManager보다 먼저 실행된 경우 한 번 더 찾는다.
        BindSteamLobby();
    }

    private void OnDisable()
    {
        if (steamLobby != null && isSubscribed)
        {
            steamLobby.RoomCodeCreated -= HandleRoomCodeCreated;
            steamLobby.StatusChanged -= HandleStatusChanged;
            steamLobby.ErrorOccurred -= HandleError;
            isSubscribed = false;
        }

        if (copyFeedbackCoroutine != null)
        {
            StopCoroutine(copyFeedbackCoroutine);
            copyFeedbackCoroutine = null;
        }
    }

    public void CreateRoom()
    {
        if (!ResolveSteamLobby())
        {
            HandleError("SteamLobby를 찾을 수 없습니다.");
            return;
        }

        steamLobby.HostSteamLobby();
    }

    public void CreatePvpRoom()
    {
        if (!ResolveSteamLobby())
        {
            HandleError("SteamLobby를 찾을 수 없습니다.");
            return;
        }

        steamLobby.HostPvpSteamLobby();
    }

    public void JoinRoom()
    {
        if (!ResolveSteamLobby())
        {
            HandleError("SteamLobby를 찾을 수 없습니다.");
            return;
        }

        string roomCode =
            roomCodeInput != null ? roomCodeInput.text.Trim().ToUpperInvariant() : null;

        steamLobby.JoinRoomByCode(roomCode);
    }

    public void LeaveRoom()
    {
        steamLobby?.LeaveCurrentRoom();
    }

    public void CopyRoomCode()
    {
        // 이벤트를 놓친 경우 SteamLobby의 현재 값을 다시 확인한다.
        if (
            string.IsNullOrWhiteSpace(currentRoomCode)
            && ResolveSteamLobby()
            && !string.IsNullOrWhiteSpace(steamLobby.CurrentRoomCode)
        )
        {
            HandleRoomCodeCreated(steamLobby.CurrentRoomCode);
        }

        if (string.IsNullOrWhiteSpace(currentRoomCode))
        {
            RefreshCopyButton();
            ShowCopyFeedback("복사할 방 코드가 없습니다.");

            Debug.LogWarning("[RoomCodeUI] 복사할 방 코드가 없습니다.");

            return;
        }

        GUIUtility.systemCopyBuffer = currentRoomCode;

        ShowCopyFeedback("방 코드가 복사되었습니다.");

        Debug.Log($"[RoomCodeUI] 방 코드 복사 완료: {currentRoomCode}");
    }

    private bool ResolveSteamLobby()
    {
        if (steamLobby == null)
            steamLobby = FindObjectOfType<SteamLobby>();

        return steamLobby != null;
    }

    private void BindSteamLobby()
    {
        if (!ResolveSteamLobby() || isSubscribed)
            return;

        steamLobby.RoomCodeCreated += HandleRoomCodeCreated;
        steamLobby.StatusChanged += HandleStatusChanged;
        steamLobby.ErrorOccurred += HandleError;
        isSubscribed = true;

        // MainUI에서 이벤트가 발생한 뒤 WaitingRoom이 열린 경우
        // 현재 방 코드를 복원한다.
        if (!string.IsNullOrWhiteSpace(steamLobby.CurrentRoomCode))
            HandleRoomCodeCreated(steamLobby.CurrentRoomCode);

        if (!string.IsNullOrWhiteSpace(steamLobby.CurrentStatus))
            HandleStatusChanged(steamLobby.CurrentStatus);
    }

    private void HandleRoomCodeCreated(string roomCode)
    {
        currentRoomCode = string.IsNullOrWhiteSpace(roomCode)
            ? null
            : roomCode.Trim().ToUpperInvariant();

        if (createdRoomCodeText != null)
            createdRoomCodeText.text = currentRoomCode ?? string.Empty;

        RefreshCopyButton();
    }

    private void RefreshCopyButton()
    {
        if (copyRoomCodeButton == null)
            return;

        copyRoomCodeButton.interactable = !string.IsNullOrWhiteSpace(currentRoomCode);
    }

    private void ShowCopyFeedback(string message)
    {
        if (copyFeedbackText == null)
        {
            Debug.LogWarning("[RoomCodeUI] copyFeedbackText가 연결되지 않았습니다.");

            return;
        }

        if (copyFeedbackCoroutine != null)
            StopCoroutine(copyFeedbackCoroutine);

        copyFeedbackCoroutine = StartCoroutine(ShowCopyFeedbackCoroutine(message));
    }

    private IEnumerator ShowCopyFeedbackCoroutine(string message)
    {
        copyFeedbackText.text = message;

        yield return new WaitForSecondsRealtime(copyFeedbackDuration);

        copyFeedbackText.text = string.Empty;
        copyFeedbackCoroutine = null;
    }

    private void HandleStatusChanged(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void HandleError(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.LogError($"[RoomCodeUI] {message}");
    }
}
