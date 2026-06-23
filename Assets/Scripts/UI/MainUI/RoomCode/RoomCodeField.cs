using TMPro;
using UnityEngine.EventSystems;

// 방 코드 입력 필드.
// 기본적으로 TMP_InputField처럼 텍스트 입력/캐럿 이동을 그대로 지원하되,
// 상/하 방향 입력 시에는 캐럿을 움직이는 대신 편집을 끝내고(선택 해제)
// Navigation 설정(SelectOnUp/SelectOnDown)을 따라 위/아래 UI로 이동한다.
public class RoomCodeField : TMP_InputField
{
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
}
