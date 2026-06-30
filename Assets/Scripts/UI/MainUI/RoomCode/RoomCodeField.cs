using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 방 코드 입력 필드.
// 기본적으로 TMP_InputField처럼 텍스트 입력/캐럿 이동을 그대로 지원하되,
// 상/하 방향 입력 시에는 캐럿을 움직이는 대신 편집을 끝내고(선택 해제)
// Navigation 설정(SelectOnUp/SelectOnDown)을 따라 위/아래 UI로 이동한다.
public class RoomCodeField : TMP_InputField
{
    [SerializeField] private UIButton startButton;

    // 입력 값 변경을 감지하기 위해 onValueChanged에 리스너를 직접 등록한다.
    // (인스펙터 연결 누락으로 동작하지 않는 상황을 방지)
    protected override void OnEnable()
    {
        base.OnEnable();
        onValueChanged.AddListener(OnValueChanged);
        // 활성화 시점의 현재 텍스트로 버튼 상태를 초기화한다.
        OnValueChanged(text);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        onValueChanged.RemoveListener(OnValueChanged);
    }

    // 네비게이션 이동 이벤트의 단일 진입점.
    // InputSystemUIInputModule이 선택된 오브젝트로 보내는 Move 이벤트를 여기서 처리한다.
    public override void OnMove(AxisEventData eventData)
    {
        switch (eventData.moveDir)
        {
            // 상/하: 편집 중이면 포커스를 풀고, Navigation에 따라 위/아래로 이동
            case MoveDirection.Up:
            case MoveDirection.Down:
                if (isFocused) DeactivateInputField();
                base.OnMove(eventData);
                break;

            // 좌/우: 편집 중에는 캐럿 이동을 위해 네비게이션 이동을 막는다.
            //        (편집 중이 아니면 일반 Selectable처럼 좌우 이동 허용)
            default:
                if (isFocused) return;
                base.OnMove(eventData);
                break;
        }
    }

    // 입력된 방 코드가 유효 조건을 충족하면 시작 버튼의 상호작용을 활성화한다.
    private void OnValueChanged(string value)
    {
        if (startButton == null) return;

        // 1자 이상 6자 이하일 때만 시작 버튼을 활성화한다.
        bool isValid = value.Length > 0 && value.Length <= 6;

        // interactable을 직접 끄면 버튼이 네비게이션 대상에서 제외되어 방향키 이동이 끊긴다.
        // 따라서 네비게이션은 유지하면서 시각적 비활성화 + 입력 차단만 적용하는 잠금 토글을 사용한다.
        startButton.SetInteractableLock(!isValid);
    }
}
