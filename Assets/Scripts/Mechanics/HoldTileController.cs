using System.Collections.Generic;
using Mirror;
using UnityEngine;

// 버튼을 밟으면 올라가고, 모두 내려오면 원위치로 돌아오는 발판.
// 이동 경로가 고정(원위치 ↔ pushPos)이라 결정적이므로, "밟힘 여부"만 SyncVar로 동기화하고
// 각 클라이언트가 동일한 MoveTowards 계산을 로컬로 수행해 위치를 일치시킨다.
// (NetworkTransform이 필요 없어 네트워크 지터 없이 발판이 플레이어를 안정적으로 실어 나른다.)
// IMovingSurface를 구현해 PlayerMovement의 캐리 로직으로 발판 위 플레이어를 실어 나른다.
public class HoldTileController : NetworkBehaviour, IMovingSurface
{
    [SerializeField] float speed;

    // 버튼을 밟았을 때 이동할 목표 위치(로컬 좌표).
    [SerializeField] Vector3 pushPos;

    // 아무도 밟지 않았을 때의 원위치(시작 시점의 로컬 좌표).
    Vector3 _restPos;

    // 서버 전용: 현재 발판을 밟고 있는 대상 집합.
    readonly HashSet<Rigidbody2D> _holders = new HashSet<Rigidbody2D>();

    // 밟힘 여부. 서버가 권한을 갖고 모든 클라이언트에 동기화한다.
    // 각 클라이언트는 이 값에 따라 목표 위치를 정하고 로컬에서 이동한다.
    [SyncVar]
    bool isPushed;

    // IMovingSurface 구현: 이 발판은 항상 플레이어를 실어 나른다.
    public bool CanCarryPlayer => true;
    public Vector3 CarryPosition => transform.position;

    void Awake()
    {
        // 원위치는 씬에 배치된 초기 로컬 좌표. 모든 클라이언트에서 동일하다.
        _restPos = transform.localPosition;
    }

    void Update()
    {
        // 서버·클라이언트 모두 동일한 목표 위치를 향해 같은 속도로 이동한다.
        Vector3 target = isPushed ? pushPos : _restPos;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            target,
            speed * Time.deltaTime
        );
    }

    [Server]
    public void AddHolder(Rigidbody2D rb)
    {
        if (rb == null)
            return;

        _holders.Add(rb);
        isPushed = true;
    }

    [Server]
    public void RemoveHolder(Rigidbody2D rb)
    {
        if (rb == null)
            return;

        _holders.Remove(rb);

        if (_holders.Count == 0)
            isPushed = false;
    }
}
