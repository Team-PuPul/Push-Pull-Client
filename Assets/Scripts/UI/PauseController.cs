using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 인게임 일시정지 입력(ESC / 게임패드 Start)과 메인 화면 복귀를 담당한다.
// PauseUI 프리팹 루트에 붙이면 자식의 PauseCanvas를 자동으로 찾는다.
public class PauseController : MonoBehaviour
{
    // PushPullNetworkManager.offlineScene과 동일한 경로를 사용한다.
    private const string MainMenuScenePath = "Assets/Scenes/InGameScenes/UI/MainUI.unity";

    [SerializeField]
    private PauseCanvas pauseCanvas;

    // 로컬 플레이(테스트 씬, 오프라인)에서만 시간을 멈춘다.
    // 네트워크 세션 중에는 한쪽만 timeScale을 0으로 만들면 두 클라이언트의
    // 물리 시뮬레이션이 어긋나므로, 시간은 흘려보내고 UI만 띄운다.
    [SerializeField]
    private bool pauseTimeScaleInLocalPlay = true;

    private PlayerInputAction _inputAction;
    private InputAction _menuAction;

    // timeScale을 이 컨트롤러가 직접 건드렸는지 여부. 복구 시점 판단에 사용한다.
    private bool _timeScaleChanged;

    // 일시정지 UI가 열려 있는지 여부. 외부에서 입력 차단 판단 등에 참조할 수 있다.
    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (pauseCanvas == null)
            pauseCanvas = GetComponentInChildren<PauseCanvas>(true);

        if (pauseCanvas == null)
        {
            Debug.LogError($"[PauseController] PauseCanvas를 찾지 못했습니다. object={gameObject.name}");
        }

        // Menu 액션은 리바인딩 금지 대상(RebindingManager._lockedActions)이라
        // 별도 인스턴스를 만들어도 저장된 바인딩 오버라이드와 어긋나지 않는다.
        _inputAction = new PlayerInputAction();
        _menuAction = _inputAction.PlayerAction.Menu;
    }

    private void OnEnable()
    {
        _menuAction.performed += OnMenuPerformed;
        _menuAction.Enable();
    }

    private void OnDisable()
    {
        _menuAction.performed -= OnMenuPerformed;
        _menuAction.Disable();
    }

    private void OnDestroy()
    {
        // 일시정지 상태로 씬을 떠나면 timeScale이 0인 채로 남으므로 반드시 복구한다.
        RestoreTimeScale();

        _inputAction?.Dispose();
    }

    // ESC(키보드) / Start(게임패드) 입력 처리
    private void OnMenuPerformed(InputAction.CallbackContext context)
    {
        // 키 재설정 중에는 ESC가 리바인딩 취소용으로 쓰이므로 무시한다.
        if (RebindingManager.IsRebinding)
            return;

        if (pauseCanvas == null)
            return;

        if (!IsPaused)
        {
            Pause();
            return;
        }

        // 일시정지 상태에서 설정 화면으로 넘어간 경우(PauseCanvas가 화면에 없음)에는
        // ESC로 바로 재개하지 않는다. 설정 화면의 뒤로가기 버튼으로 돌아와야 한다.
        if (pauseCanvas.IsVisible)
            Resume();
    }

    // 일시정지 UI를 띄운다.
    public void Pause()
    {
        if (IsPaused || pauseCanvas == null)
            return;

        IsPaused = true;

        pauseCanvas.EnableCanvas();

        // 네트워크 세션 중이 아닐 때만 시간을 멈춘다.
        if (pauseTimeScaleInLocalPlay && !NetworkClient.active && !NetworkServer.active)
        {
            Time.timeScale = 0f;
            _timeScaleChanged = true;
        }
    }

    // 일시정지를 해제하고 게임으로 돌아간다. (Continue 버튼에 연결)
    public void Resume()
    {
        if (!IsPaused || pauseCanvas == null)
            return;

        IsPaused = false;

        RestoreTimeScale();

        pauseCanvas.DisableCanvas();
    }

    // 메인 화면으로 돌아간다. (일시정지 UI의 나가기 버튼에 연결)
    public void ReturnToMainMenu()
    {
        IsPaused = false;

        // 씬 전환 전에 반드시 시간을 되돌린다. 메인 화면이 멈춘 채로 뜨는 것을 방지.
        RestoreTimeScale();

        SteamLobby steamLobby = FindSteamLobby();

        if (steamLobby != null)
        {
            // 서버 방 정리 → StopHost/StopClient → offlineScene(MainUI) 자동 로드까지 처리된다.
            steamLobby.LeaveCurrentRoom();
            return;
        }

        // SteamLobby가 없는 경우(테스트 씬 등)의 대비 경로
        if (NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive)
        {
            StopNetwork();
            return;
        }

        SceneManager.LoadScene(MainMenuScenePath);
    }

    // SteamLobby는 NetworkManager와 같은 오브젝트에 붙어 있다.
    private SteamLobby FindSteamLobby()
    {
        if (NetworkManager.singleton != null)
        {
            SteamLobby lobby = NetworkManager.singleton.GetComponent<SteamLobby>();

            if (lobby != null)
                return lobby;
        }

        return FindObjectOfType<SteamLobby>();
    }

    // SteamLobby.CloseSteamLobbyAndNetwork와 동일한 종료 순서를 따른다.
    private void StopNetwork()
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

    private void RestoreTimeScale()
    {
        if (!_timeScaleChanged)
            return;

        Time.timeScale = 1f;
        _timeScaleChanged = false;
    }
}
