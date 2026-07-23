using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using TMPro;

public class UIButton : Button
{
    [SerializeField] protected UISoundInfo soundInfo;

    [SerializeField] private bool useTextColorTransition = false;   // 버튼 상태에 따라 내부 텍스트 색도 전환할지 여부
    [SerializeField] private TMP_Text targetText;                   // 버튼 상태(색) 전환을 함께 따라갈 내부 텍스트

    [SerializeField] private ButtonCanvas disableCanvas;        // 비활성화 할(보이지 않게 할 캔버스) 오브젝트
    [SerializeField] private  ButtonCanvas enableCanvas;        // 활성화 할(보이게 할 캔버스) 오브젝트

    [SerializeField] private ButtonPanel disablePanel;          // 비활성화 할(보이지 않게 할 캔버스) 오브젝트
    [SerializeField] private ButtonPanel enablePanel;           // 활성화 할(보이게 할 캔버스) 오브젝트

    [SerializeField] private ButtonType buttonType;

    // 같은 버튼이 직전과 동일하게 재선택될 때 호버 사운드 중복 재생을 막기 위한 가드
    private static UIButton _lastHovered;

    // 네비게이션은 유지하되(interactable=true) 클릭/서브밋만 막는 잠금 상태.
    // interactable을 직접 끄면 Selectable이 네비게이션 대상에서 제외되어
    // 방향키/게임패드 이동 경로가 끊기므로, 시각적 비활성화 + 입력 차단으로 대체한다.
    private bool _interactableLocked;

    // 외부에서 잠금 상태를 토글한다. interactable은 건드리지 않으므로 네비게이션은 그대로 유지된다.
    public void SetInteractableLock(bool locked)
    {
        if (_interactableLocked == locked) return;
        _interactableLocked = locked;

        // 잠금 상태 변화에 맞춰 즉시 비주얼을 갱신한다.
        DoStateTransition(currentSelectionState, false);
    }

    protected override void Start()
    {
        base.Start();

        if(buttonType != ButtonType.ChangeCanvas)
        {
            disableCanvas = GetComponentInParent<ButtonCanvas>();
        }

        // 텍스트 색 전환을 쓸 때만, 인스펙터에서 비워두면 자식에서 자동으로 찾아 둔다.
        if (useTextColorTransition && targetText == null)
        {
            targetText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    // 버튼 상태(Normal/Highlighted/Pressed/Selected/Disabled)가 바뀔 때마다 호출된다.
    // 기본 Selectable은 targetGraphic 하나만 틴트하므로, 내부 TMP에도 같은 색을 적용해
    // isInteractable이 꺼지면 텍스트도 비활성화 색을 따라가도록 한다.
    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // 잠금 상태에서는 항상 Disabled 비주얼로 강제한다(클릭은 OnPointerClick/OnSubmit에서 차단).
        if (_interactableLocked) state = SelectionState.Disabled;

        base.DoStateTransition(state, instant);

        // 텍스트 색 전환 기능이 꺼져 있으면 아무것도 하지 않는다.
        if (!useTextColorTransition) return;

        // DoStateTransition은 Start보다 먼저 호출될 수 있어 lazy 가드를 둔다.
        if (targetText == null)
        {
            targetText = GetComponentInChildren<TMP_Text>(true);
            // 끝내 찾지 못하면 기능을 꺼 매 상태 전환마다 GetComponentInChildren가 반복 호출되는 것을 막는다.
            if (targetText == null)
            {
                useTextColorTransition = false;
                return;
            }
        }

        // Color Tint 트랜지션이 아닐 때는 색을 건드리지 않는다.
        if (transition != Transition.ColorTint) return;

        // 텍스트 색은 활성(Normal)/비활성(Disabled) 상태에서만 전환한다.
        // Highlighted/Pressed/Selected 등 나머지 상태에서는 색을 바꾸지 않고 normalColor를 유지한다.
        Color targetColor = state == SelectionState.Disabled
            ? colors.disabledColor
            : colors.normalColor;

        // colorMultiplier를 곱해 기존 targetGraphic 전환과 톤을 맞춘다.
        targetText.CrossFadeColor(
            targetColor * colors.colorMultiplier,
            instant ? 0f : colors.fadeDuration,
            true,   // ignoreTimeScale
            true);  // useAlpha
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        // 마우스 모드가 아니면 포인터 호버는 무시 (키보드/게임패드 선택 유지)
        if (UIInputManager.instance != null &&
            UIInputManager.instance.currentDevice != InputDeviceType.Mouse)
        {
            return;
        }

        base.OnPointerEnter(eventData);

        // 선택만 시킨다. 호버 사운드는 OnSelect에서 단 한 번만 재생됨.
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        PlayHover();
    }
    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        // 다른 버튼으로 이동했다가 돌아오면 다시 호버 사운드가 나도록 가드 해제
        if (_lastHovered == this) _lastHovered = null;
    }

    // 호버 사운드 단일 진입점
    private void PlayHover()
    {
        if (_interactableLocked) return;    // 잠금(비활성 표시) 상태에서는 호버 사운드 무음
        if (_lastHovered == this) return;   // 같은 버튼 연속 재선택 시 무음
        _lastHovered = this;

        // 시스템이 유발한 선택 복구(빈 공간 클릭 후 등)는 무음 처리
        if (UIInputManager.instance != null && UIInputManager.instance.SuppressHoverSound) return;

        if (soundInfo != null && soundInfo.HoverSound != null)
        {
            SoundManager.Instance?.SFXPlay("UI_Hover", soundInfo.HoverSound);
        }
    }

    #region Click/Submit
    public override void OnPointerClick(PointerEventData eventData)
    {
        // 리바인딩 진행 중에는 버튼이 눌리지 않도록 무시
        if (RebindingManager.IsRebinding) return;
        // 잠금 상태에서는 클릭을 무시 (비활성처럼 동작)
        if (_interactableLocked) return;

        base.OnPointerClick(eventData);
        PlayClick();
        OnClicked();
    }
    public override void OnSubmit(BaseEventData eventData)
    {
        if (RebindingManager.IsRebinding) return;
        // 잠금 상태에서는 서브밋을 무시 (비활성처럼 동작)
        if (_interactableLocked) return;

        base.OnSubmit(eventData);
        PlayClick();
        OnClicked();
    }

    // 클릭 사운드 단일 진입점
    private void PlayClick()
    {
        if (soundInfo != null && soundInfo.ClickSound != null)
        {
            SoundManager.Instance?.SFXPlay("UI_Click", soundInfo.ClickSound);
        }
    }
    #endregion

    public void OnClicked()
    {
        switch (buttonType)
        {
            case ButtonType.ChangeCanvas:
                changeCanvas(); break;
            case ButtonType.ChangePanel:
                changePanel(); break;
            case ButtonType.GoMain:
                enableCanvas = FindObjectsOfType<ButtonCanvas>().Where(canvas => canvas.MainCanvas == true).First();
                changeCanvas(); break;
            case ButtonType.Quit:
#if UNITY_EDITOR
                // 에디터에서는 Application.Quit()이 동작하지 않으므로 플레이 모드를 종료한다.
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    private void changeCanvas() => StartCoroutine(changeCanvasCoroutine());
    private void changePanel() => StartCoroutine(changePanelCoroutine());

    // WaitForSecondsRealtime: 일시정지로 timeScale이 0이 되면 WaitForSeconds가
    // 영원히 대기해 캔버스/패널 전환이 멈추므로 실시간 기준으로 기다린다.
    private IEnumerator changeCanvasCoroutine()
    {
        disableCanvas.FadeIn();
        yield return new WaitForSecondsRealtime(0.2f);
        enableCanvas.EnableCanvas();
    }
    private IEnumerator changePanelCoroutine()
    {
        disablePanel.DisablePanel();
        yield return new WaitForSecondsRealtime(0.2f);
        enablePanel.EnablePanel();
    }
}

public enum ButtonType
{
    None,           // 기본 (소리만 나거나 단순 클릭 로그용)
    ChangeCanvas,   // 현재 창을 끄고 다른 창을 엶 (UI 이동)
    ChangePanel,    // 현재 패널을 끄고 다른 패널을 엶 (UI 이동)
    OpenPopup,      // 현재 창은 두고 위에 팝업을 띄움
    ClosePopup,     // 현재 팝업을 닫음
    Submit,         // 데이터 확인, 아이템 구매 등 서버/데이터 연동
    GameStart,      // 씬 전환 (Scene Load)
    GoMain,
    Quit            // 게임 종료
}
