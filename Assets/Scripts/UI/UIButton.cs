using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class UIButton : Button
{
    [SerializeField] protected UISoundInfo soundInfo;

    [SerializeField] private ButtonCanvas disableCanvas;        // 비활성화 할(보이지 않게 할 캔버스) 오브젝트
    [SerializeField] private  ButtonCanvas enableCanvas;        // 활성화 할(보이게 할 캔버스) 오브젝트

    [SerializeField] private ButtonPanel disablePanel;          // 비활성화 할(보이지 않게 할 캔버스) 오브젝트
    [SerializeField] private ButtonPanel enablePanel;           // 활성화 할(보이게 할 캔버스) 오브젝트

    [SerializeField] private ButtonType buttonType;

    // 같은 버튼이 직전과 동일하게 재선택될 때 호버 사운드 중복 재생을 막기 위한 가드
    private static UIButton _lastHovered;

    protected override void Start()
    {
        base.Start();

        if(buttonType != ButtonType.ChangeCanvas)
        {
            disableCanvas = GetComponentInParent<ButtonCanvas>();
        }
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
        base.OnPointerClick(eventData);
        PlayClick();
        OnClicked();
    }
    public override void OnSubmit(BaseEventData eventData)
    {
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
        }
    }

    private void changeCanvas() => StartCoroutine(changeCanvasCoroutine());
    private void changePanel() => StartCoroutine(changePanelCoroutine());

    private IEnumerator changeCanvasCoroutine()
    {
        disableCanvas.FadeIn();
        yield return new WaitForSeconds(0.2f);
        enableCanvas.EnableCanvas();
    }
    private IEnumerator changePanelCoroutine()
    {
        disablePanel.DisablePanel();
        yield return new WaitForSeconds(0.2f);
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
