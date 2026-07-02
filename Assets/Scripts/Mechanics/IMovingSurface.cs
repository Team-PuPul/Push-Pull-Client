using UnityEngine;

public interface IMovingSurface
{
    bool CanCarryPlayer { get; }
    Vector3 CarryPosition { get; }

    // 발판의 월드 속도. 플레이어가 발판 위에서 이 속도를 자신의 velocity에 더해
    // 상대 미끄러짐(마찰로 인한 속도 저하)을 없애는 데 사용한다.
    Vector3 CarryVelocity { get; }
}
