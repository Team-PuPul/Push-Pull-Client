using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public class ButtonCanvas : MonoBehaviour
{
    public bool MainCanvas
    {
        get
        {
            return isMainCanva;
        }
    }
    // 이 캔버스가 현재 화면에 표시 중인지 여부 (PauseController의 ESC 토글 판단용)
    public bool IsVisible => canvas != null && canvas.enabled;

    [SerializeField] private bool isMainCanva = false;
    [SerializeField] protected float fadeDuration = 0.2f;
    [SerializeField] private Button firstSelectButton;

    protected CanvasGroup canvasGroup;
    protected Canvas canvas;

    // 이 캔버스를 떠날 때 마지막으로 선택돼 있던 버튼 (돌아왔을 때 복원용)
    private GameObject lastSelectedButton;

    // 초기 상태 세팅(Start)이 이미 끝났는지 여부.
    // 비활성 상태로 씬에 배치된 캔버스는 Start가 씬 진입 시점이 아니라
    // "처음 활성화되는 시점"에 뒤늦게 실행되는데, 그 시점은 곧 EnableCanvas() 직후다.
    // 가드가 없으면 방금 연 캔버스를 Start의 DisableCanvas()가 그대로 닫아버리고,
    // 페이드 트윈끼리 충돌해 배경만 남는 문제가 생긴다.
    private bool initialized;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponent<Canvas>();

        // 숨겨진 캔버스의 버튼이 방향키·게임패드 네비게이션 대상에 남지 않도록
        // 시작 시점의 상호작용 여부를 캔버스 표시 상태와 맞춘다.
        // (Selectable은 부모 CanvasGroup의 interactable이 false면 네비게이션에서 제외된다)
        //
        // Start가 아니라 Awake에서 확정하는 이유: 자식 ButtonPanel의 Start가
        // 이 캔버스의 Start보다 먼저 실행될 수 있어 그 사이가 비게 된다.
        canvasGroup.interactable = canvas.enabled;
    }

    protected virtual void Start()
    {
        if (initialized) return;

        if (isMainCanva) EnableCanvas();
        else DisableCanvas();
    }

    private void Update()
    {
        canvasGroup.blocksRaycasts = UIInputManager.instance.currentDevice == InputDeviceType.Mouse;
    }

    // 캔버스를 활성화시키고 효과 및 선택 버튼을 설정
    public virtual void EnableCanvas()
    {
        initialized = true;

        // SelectButton()보다 먼저 켜야 선택되는 버튼이 상호작용 가능 상태가 된다.
        canvasGroup.interactable = true;
        canvas.enabled = true;

        SelectButton();
        canvasGroup.alpha = 0f;
        FadeOut();
    }

    // 마지막 선택 버튼이 기록돼 있으면 그 위치를, 없으면(첫 진입) 첫 버튼을 선택
    private void SelectButton()
    {
        if (lastSelectedButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(lastSelectedButton);
        else
            firstSelectButton.Select();
    }

    // 캔버스를 떠나기 직전 현재 선택된 버튼을 기록 (이 캔버스 소속 버튼일 때만)
    private void SaveSelection()
    {
        if (EventSystem.current == null || !isMainCanva) return;

        GameObject current = EventSystem.current.currentSelectedGameObject;
        if (current != null && current.transform.IsChildOf(transform))
            lastSelectedButton = current;
    }

    // 캔버스를 비활성화 시키고 알파를 0으로 바꿈
    public virtual void DisableCanvas()
    {
        initialized = true;

        canvas.enabled = false;
        canvasGroup.alpha = 0f;
        // 렌더링만 끄면 자식 버튼이 네비게이션·클릭 대상으로 남으므로 상호작용도 함께 차단한다.
        canvasGroup.interactable = false;
    }
    
    // SetUpdate(true): 일시정지로 timeScale이 0이 되어도 페이드가 진행되도록 한다.
    // (timeScale 0에서는 기본 DOTween 트윈이 멈춰 캔버스가 alpha 0인 채로 남는다)
    public void FadeOut()
    {
        canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
    }
    public void FadeIn()
    {
        SaveSelection();
        // 페이드아웃 중에도 이 캔버스는 이미 떠난 화면이므로 상호작용을 바로 차단한다.
        // (설정 화면으로 넘어갈 때는 DisableCanvas가 아니라 이 경로를 탄다)
        canvasGroup.interactable = false;
        canvasGroup.DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                canvas.enabled = false;
            });
    }
}
