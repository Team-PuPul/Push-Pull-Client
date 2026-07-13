using Mirror;
using UnityEngine;

// 버튼을 밟으면 연결된 벽을 사라지게 하는 기믹.
// 멀티플레이 동기화를 위해 NetworkBehaviour로 전환했다.
// 트리거는 서버에서만 감지하고, 결과 상태를 SyncVar로 모든 클라이언트에 전파한다.
public class DisappearButton : NetworkBehaviour
{
    [SerializeField] GameObject disappearObj;

    // 버튼을 누를 수 있는 오브젝트의 레이어(플레이어 + 미는 박스 등).
    // 비워 두면(Nothing) Rigidbody2D를 가진 모든 오브젝트를 허용한다.
    [SerializeField] LayerMask pressableMask;

    Animator anim;

    // 버튼 눌림 상태. 한 번 눌리면 벽이 영구히 사라진다.
    [SyncVar(hook = nameof(OnPressedChanged))]
    bool isPressed;

    // 벽 활성 상태. 서버가 권한을 갖고 모든 클라이언트에 동기화한다.
    [SyncVar(hook = nameof(OnWallActiveChanged))]
    bool isWallActive = true;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 나중에 접속한 게스트도 현재 상태를 그대로 반영받는다.
        ApplyWallActive(isWallActive);

        if (isPressed && anim != null)
            anim.Play("Push");
    }

    [ServerCallback]
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (isPressed)
            return;

        if (!IsPressable(collision))
            return;

        isPressed = true;
        isWallActive = false;

        // 호스트 화면에도 즉시 반영한다.
        ApplyWallActive(false);

        if (anim != null)
            anim.Play("Push");
    }

    // 트리거 대상이 버튼을 누를 수 있는 오브젝트인지 판별한다.
    bool IsPressable(Collider2D collision)
    {
        if (collision.attachedRigidbody == null)
            return false;

        // 레이어 마스크가 지정되지 않았으면 물리 오브젝트를 모두 허용한다.
        if (pressableMask.value == 0)
            return true;

        return (pressableMask.value & (1 << collision.gameObject.layer)) != 0;
    }

    void OnPressedChanged(bool oldValue, bool newValue)
    {
        if (newValue && anim != null)
            anim.Play("Push");
    }

    void OnWallActiveChanged(bool oldValue, bool newValue)
    {
        ApplyWallActive(newValue);
    }

    void ApplyWallActive(bool active)
    {
        if (disappearObj != null)
            disappearObj.SetActive(active);
    }
}
