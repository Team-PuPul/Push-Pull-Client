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

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponent<Canvas>();
    }

    protected virtual void Start()
    {
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
        SelectButton();
        canvas.enabled = true;
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
        canvas.enabled = false;
        canvasGroup.alpha = 0f;
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
        canvasGroup.DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                canvas.enabled = false;
            });
    }
}
