using System.Collections.Generic;
using Mirror;
using UnityEngine;

// 버튼을 밟는 동안 바람 오브젝트를 켜는 기믹.
// 서버에서만 밟은 대상을 집계하고, 바람 on/off 상태를 SyncVar로 동기화한다.
public class FanButton : NetworkBehaviour
{
    [SerializeField] GameObject wind;

    // 버튼을 누를 수 있는 오브젝트의 레이어(플레이어 + 미는 박스 등).
    // 비워 두면(Nothing) Rigidbody2D를 가진 모든 오브젝트를 허용한다.
    [SerializeField] LayerMask pressableMask;

    Animator anim;

    // 서버 전용: 현재 버튼을 밟고 있는 대상 집합.
    // 두 명이 동시에 밟았다가 한 명만 내려와도 나머지가 있으면 바람이 유지된다.
    readonly HashSet<Rigidbody2D> _pressers = new HashSet<Rigidbody2D>();

    [SyncVar(hook = nameof(OnWindActiveChanged))]
    bool isWindActive;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        ApplyWindActive(isWindActive);

        if (isWindActive && anim != null)
            anim.Play("Push");
    }

    [ServerCallback]
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsPressable(collision))
            return;

        if (!_pressers.Add(collision.attachedRigidbody))
            return;

        UpdateWindState();
    }

    [ServerCallback]
    void OnTriggerExit2D(Collider2D collision)
    {
        Rigidbody2D rb = collision.attachedRigidbody;

        if (rb == null)
            return;

        if (!_pressers.Remove(rb))
            return;

        UpdateWindState();
    }

    [Server]
    void UpdateWindState()
    {
        bool active = _pressers.Count > 0;

        if (isWindActive == active)
            return;

        // SyncVar만 변경하면 호스트를 포함한 모든 클라이언트에서 hook이 호출되어
        // 바람 on/off·애니메이션이 반영된다. (수동 호출은 호스트에서 중복 실행을 유발한다.)
        isWindActive = active;
    }

    bool IsPressable(Collider2D collision)
    {
        if (collision.attachedRigidbody == null)
            return false;

        if (pressableMask.value == 0)
            return true;

        return (pressableMask.value & (1 << collision.gameObject.layer)) != 0;
    }

    void OnWindActiveChanged(bool oldValue, bool newValue)
    {
        ApplyWindActive(newValue);

        if (anim != null)
            anim.Play(newValue ? "Push" : "Pull");
    }

    void ApplyWindActive(bool active)
    {
        if (wind != null)
            wind.SetActive(active);
    }
}
