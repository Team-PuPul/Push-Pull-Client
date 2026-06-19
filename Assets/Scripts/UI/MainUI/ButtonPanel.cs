using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonPanel : MonoBehaviour
{
    [SerializeField] private bool baseEnabled = false;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private Button firstSelectButton;

    private bool isDisabled = false;
    private CanvasGroup canvasGroup;

    // 이 패널을 떠날 때 마지막으로 선택돼 있던 버튼 (돌아왔을 때 복원용)
    private GameObject lastSelectedButton;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        if(!baseEnabled) DisablePanel();
    }

    private void Update()
    {
        if (isDisabled) return;
        canvasGroup.blocksRaycasts = UIInputManager.instance.currentDevice == InputDeviceType.Mouse;
    }

    // 패널을 활성화시키고 효과 및 선택 버튼을 설정
    public virtual void EnablePanel()
    {
        SelectButton();
        canvasGroup.alpha = 0f;
        FadeOut();
    }

    // 패널을 비활성화 시키고 알파를 0으로 바꿈
    public virtual void DisablePanel()
    {
        SaveSelection();
        isDisabled = true;
        FadeIn();
    }

    // 마지막 선택 버튼이 기록돼 있으면 그 위치를, 없으면(첫 진입) 첫 버튼을 선택
    private void SelectButton()
    {
        if (lastSelectedButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(lastSelectedButton);
        else
            firstSelectButton.Select();
    }

    // 패널을 떠나기 직전 현재 선택된 버튼을 기록 (이 패널 소속 버튼일 때만)
    private void SaveSelection()
    {
        if (EventSystem.current == null) return;

        GameObject current = EventSystem.current.currentSelectedGameObject;
        if (current != null && current.transform.IsChildOf(transform))
            lastSelectedButton = current;
    }

    public void FadeOut()
    {
        canvasGroup.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            canvasGroup.blocksRaycasts = true;
        });
    }
    public void FadeIn()
    {
        canvasGroup.blocksRaycasts = false;
        canvasGroup.DOFade(0f, fadeDuration);
    }
}
