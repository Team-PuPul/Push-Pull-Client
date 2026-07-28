using Mirror;
using UnityEngine;

public sealed class WaitingRoomUIController : MonoBehaviour
{
    [Header("Host UI")]
    [SerializeField]
    private GameObject gameStartButton;

    private void OnEnable()
    {
        NetworkClient.OnConnectedEvent += RefreshAuthorityUI;
        NetworkClient.OnDisconnectedEvent += RefreshAuthorityUI;

        RefreshAuthorityUI();
    }

    private void OnDisable()
    {
        NetworkClient.OnConnectedEvent -= RefreshAuthorityUI;
        NetworkClient.OnDisconnectedEvent -= RefreshAuthorityUI;
    }

    private void RefreshAuthorityUI()
    {
        if (gameStartButton == null)
        {
            Debug.LogWarning("[WaitingRoomUI] GameStartButton이 연결되지 않았습니다.");

            return;
        }

        // 호스트는 NetworkServer가 활성화되어 있고,
        // 게스트 클라이언트는 NetworkServer가 비활성화되어 있다.
        bool isHost = NetworkServer.active;

        gameStartButton.SetActive(isHost);

        Debug.Log($"[WaitingRoomUI] GameStartButton 표시 상태: {isHost}");
    }
}
