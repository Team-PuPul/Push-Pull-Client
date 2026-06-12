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
                PlayStep();
                break;
            case MoveDirection.Right:
                onNext?.Invoke();
                PlayStep();
                break;
            default:
                base.OnMove(eventData);
                break;
        }
    }

    // 마우스 클릭은 오른쪽(다음) 이동으로 처리한다. (클릭 한 번 = 리스트 오른쪽 이동)
    // 기본 UIButton의 클릭 사운드/캔버스 전환 로직 대신 스텝 동작으로 대체한다.
    public override void OnPointerClick(PointerEventData eventData)
    {
        onNext?.Invoke();
        PlayStep();
    }

    // 좌/우 이동 시 재생할 사운드. 클릭 사운드를 재사용한다.
    private void PlayStep()
    {
        if (soundInfo != null && soundInfo.ClickSound != null)
        {
            SoundManager.Instance?.SFXPlay("UI_Step", soundInfo.ClickSound);
        }
    }
}
