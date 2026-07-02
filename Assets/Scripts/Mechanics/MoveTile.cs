using Mirror;
using UnityEngine;

public class MoveTile : NetworkBehaviour, IMovingSurface
{
    [SerializeField]
    private Vector3 pos1;

    [SerializeField]
    private Vector3 pos2;

    [SerializeField]
    private float speed;

    [SerializeField]
    private float waitTime;

    private Vector3 desPos;
    private float waitUntil;

    private Rigidbody2D rb;

    private Vector3 carryVelocity;
    private Vector3 lastTrackedPosition;
    private bool hasTrackedPosition;

    public bool CanCarryPlayer => true;
    public Vector3 CarryPosition => transform.position;
    public Vector3 CarryVelocity => carryVelocity;

    // Rigidbody2D 없는 발판(정적 콜라이더)을 transform으로 옮기면 물리 엔진 입장에서
    // 매 프레임 텔레포트라 접촉 해석이 불안정해져, 위에 탄 플레이어가 통통거린다.
    // kinematic Rigidbody2D + MovePosition(FixedUpdate)으로 움직이면 엔진이 발판의
    // 이동 속도를 알게 되어 접촉이 안정되고, Interpolate로 렌더도 매끄러워진다.
    // 기존 프리팹에 Rigidbody2D가 없어도 동작하도록 런타임에 보장한다.
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    [ServerCallback]
    private void Start()
    {
        desPos = pos1;
    }

    // [클라이언트 전용] 발판 속도를 NetworkTransform이 보간한 위치의 델타로 추정한다.
    // 서버는 FixedUpdate에서 명령한 정확한 속도를 쓰므로 여기서 덮어쓰지 않는다
    // (위치 델타 추정은 한 프레임 늦어, 발판이 멈추는 순간 잔여 속도가 남는다).
    private void Update()
    {
        if (isServer)
            return;

        Vector3 current = transform.position;

        if (hasTrackedPosition && Time.deltaTime > 0f)
            carryVelocity = (current - lastTrackedPosition) / Time.deltaTime;

        lastTrackedPosition = current;
        hasTrackedPosition = true;
    }

    [ServerCallback]
    private void FixedUpdate()
    {
        if (Time.time < waitUntil)
        {
            // 대기(정지) 중에는 속도를 즉시 0으로 알린다. 추정 지연으로 플레이어가
            // 잔여 속도를 이어받아 Fall 애니메이션이 깜빡이는 것을 막는다.
            carryVelocity = Vector3.zero;
            return;
        }

        // pos1/pos2는 로컬 좌표이므로 부모 기준으로 월드 좌표로 변환해 이동한다.
        Vector3 desWorld =
            transform.parent != null ? transform.parent.TransformPoint(desPos) : desPos;

        Vector2 next = Vector2.MoveTowards(rb.position, desWorld, speed * Time.fixedDeltaTime);

        // 이번 물리 스텝에 실제로 명령한 이동량 기반의 정확한 속도
        carryVelocity = (next - rb.position) / Time.fixedDeltaTime;

        rb.MovePosition(next);

        if (Vector2.Distance(next, desWorld) < 0.01f * (speed + 1))
        {
            waitUntil = Time.time + waitTime;
            desPos = desPos == pos1 ? pos2 : pos1;
        }
    }
}
