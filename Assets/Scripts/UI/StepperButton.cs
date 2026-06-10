using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// 좌/우 방향 입력을 받아 이벤트로 알리는 범용 스테퍼 버튼.
// 값이나 도메인(해상도, 품질, 볼륨 등)은 전혀 알지 못하며,
// 입력 해석이라는 단일 책임만 가진다. (SRP)
// 실제 값 변경은 onPrev/onNext에 연결된 쪽(예: VideoSettingPanel)이 담당한다.
public class StepperButton : UIButton
{
    [SerializeField] private UnityEvent onPrev;   // 왼쪽(←) 입력 시 호출
    [SerializeField] private UnityEvent onNext;   // 오른쪽(→) 입력 시 호출

    // 키보드/게임패드 방향 입력 처리.
    // 좌/우는 값 변경 이벤트로 소모하고, 상/하는 기본 네비게이션(선택 이동)에 넘긴다.
    public override void OnMove(AxisEventData eventData)
    {
        switch (eventData.moveDir)
        {
            case MoveDirection.Left:
                onPrev?.Invoke();
                break;
            case MoveDirection.Right:
                onNext?.Invoke();
                break;
            default:
                base.OnMove(eventData);
                break;
        }
    }
}
