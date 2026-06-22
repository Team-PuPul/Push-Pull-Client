using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 클릭(또는 Submit) 시 해당 액션의 키 리바인딩을 시작하는 버튼
public class KeyButton : UIButton
{
    [SerializeField] private InputActionReference actionReference;   // 변경할 액션
    [SerializeField] private string controlScheme = "Keyboard, Mouse";  // 입력 스킴 (PlayerAction 에셋: "Keyboard, Mouse" / "Gamepad")

    [Header("Key Display")]
    [SerializeField] private KeySpriteSO keySpriteSO;               // 키 → 스프라이트 매핑 테이블
    [SerializeField] private Image keyIcon;                         // 현재 키를 표시할 아이콘
    [SerializeField] private Sprite waitingSprite;                  // 입력 대기 중 아이콘에 표시할 스프라이트

    private bool _isRebinding;

    protected override void Start()
    {
        base.Start();
        RefreshDisplay();
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        // 리바인딩 진행 중에는 무시 (다른 버튼이 진행 중인 경우 포함)
        if (RebindingManager.IsRebinding) return;

        base.OnPointerClick(eventData);
        BeginRebind();
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (RebindingManager.IsRebinding) return;

        base.OnSubmit(eventData);
        BeginRebind();
    }

    // 리바인딩 시작: RebindingManager에 위임하고 대기 상태를 표시
    private void BeginRebind()
    {
        if (_isRebinding) return;
        if (RebindingManager.Instance == null) return;

        _isRebinding = true;
        // 대기 상태 표시: 아이콘은 대기 스프라이트로, 라벨은 대기 문구로
        if (keyIcon != null && waitingSprite != null) keyIcon.sprite = waitingSprite;

        RebindingManager.Instance.StartRebinding(actionReference, controlScheme, OnRebindFinished);
    }

    // 완료/취소 후 표시를 현재 바인딩으로 갱신
    private void OnRebindFinished()
    {
        _isRebinding = false;
        // 교체(swap)로 다른 버튼의 키도 바뀌었을 수 있으므로 모든 KeyButton을 갱신
        foreach (KeyButton button in FindObjectsOfType<KeyButton>())
            button.RefreshDisplay();
    }

    // 현재 바인딩을 아이콘(+선택적 텍스트)으로 표시
    public void RefreshDisplay()
    {
        if (actionReference == null) return;

        InputAction action = actionReference.action;
        int bindingIndex = action.GetBindingIndex(InputBinding.MaskByGroup(controlScheme));
        if (bindingIndex == -1) return;

        // 스프라이트 아이콘 갱신
        if (keyIcon != null && keySpriteSO != null)
        {
            keyIcon.sprite = keySpriteSO.GetSprite(GetControlName(action, bindingIndex));
        }
    }

    // 바인딩의 컨트롤 경로에서 마지막 토막만 추출 (예: "<Keyboard>/a" → "a")
    private string GetControlName(InputAction action, int bindingIndex)
    {
        string path = action.bindings[bindingIndex].effectivePath;
        if (string.IsNullOrEmpty(path)) return null;

        int slashIndex = path.LastIndexOf('/');
        return slashIndex >= 0 ? path.Substring(slashIndex + 1) : path;
    }
}
