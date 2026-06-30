using TMPro;
using UnityEngine;

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

    private bool isSubscribed;

    private void Awake()
    {
        ResolveSteamLobby();
    }

    private void OnEnable()
    {
        BindSteamLobby();
    }

    private void Start()
    {
        // UI의 Awake가 NetworkManager보다 먼저 실행된 경우 한 번 더 찾는다.
        BindSteamLobby();
    }

    private void OnDisable()
    {
        if (steamLobby == null || !isSubscribed)
            return;

        steamLobby.RoomCodeCreated -= HandleRoomCodeCreated;
        steamLobby.StatusChanged -= HandleStatusChanged;
        steamLobby.ErrorOccurred -= HandleError;
        isSubscribed = false;
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

        string roomCode = roomCodeInput != null
            ? roomCodeInput.text.Trim().ToUpperInvariant()
            : null;
        steamLobby.JoinRoomByCode(roomCode);
    }

    public void LeaveRoom()
    {
        steamLobby?.LeaveCurrentRoom();
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

        // MainUI에서 이벤트가 발생한 뒤 WaitingRoom이 열린 경우 현재 상태를 복원한다.
        if (!string.IsNullOrWhiteSpace(steamLobby.CurrentRoomCode))
            HandleRoomCodeCreated(steamLobby.CurrentRoomCode);

        if (!string.IsNullOrWhiteSpace(steamLobby.CurrentStatus))
            HandleStatusChanged(steamLobby.CurrentStatus);
    }

    private void HandleRoomCodeCreated(string roomCode)
    {
        if (createdRoomCodeText != null)
            createdRoomCodeText.text = roomCode;
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
