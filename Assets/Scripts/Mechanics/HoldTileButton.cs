using System.Collections.Generic;
using Mirror;
using UnityEngine;

// HoldTile 발판을 올리는 버튼.
// 서버에서만 밟은 대상을 감지해 컨트롤러에 전달하고,
// 버튼 자신의 눌림 애니메이션은 SyncVar로 모든 클라이언트에 동기화한다.
public class HoldTileButton : NetworkBehaviour
{
    [SerializeField] HoldTileController controller;

    // 버튼을 누를 수 있는 오브젝트의 레이어(플레이어 + 미는 박스 등).
    // 비워 두면(Nothing) Rigidbody2D를 가진 모든 오브젝트를 허용한다.
    [SerializeField] LayerMask pressableMask;

    Animator anim;

    // 서버 전용: 현재 버튼을 밟고 있는 대상 집합.
    readonly HashSet<Rigidbody2D> _pressers = new HashSet<Rigidbody2D>();

    [SyncVar(hook = nameof(OnPressedChanged))]
    bool isPressed;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isPressed && anim != null)
            anim.Play("Push");
    }

    [ServerCallback]
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsPressable(collision))
            return;

        Rigidbody2D rb = collision.attachedRigidbody;

        if (!_pressers.Add(rb))
            return;

        controller.AddHolder(rb);
        UpdatePressedState();
    }

    [ServerCallback]
    void OnTriggerExit2D(Collider2D collision)
    {
        Rigidbody2D rb = collision.attachedRigidbody;

        if (rb == null)
            return;

        if (!_pressers.Remove(rb))
            return;

        controller.RemoveHolder(rb);
        UpdatePressedState();
    }

    [Server]
    void UpdatePressedState()
    {
        bool pressed = _pressers.Count > 0;

        if (isPressed == pressed)
            return;

        // SyncVar만 변경하면 호스트를 포함한 모든 클라이언트에서 hook이 호출되어
        // 버튼 눌림 애니메이션이 반영된다. (수동 호출은 호스트에서 중복 실행을 유발한다.)
        isPressed = pressed;
    }

    bool IsPressable(Collider2D collision)
    {
        if (collision.attachedRigidbody == null)
            return false;

        if (pressableMask.value == 0)
            return true;

        return (pressableMask.value & (1 << collision.gameObject.layer)) != 0;
    }

    void OnPressedChanged(bool oldValue, bool newValue)
    {
        if (anim != null)
            anim.Play(newValue ? "Push" : "Pull");
    }
}
