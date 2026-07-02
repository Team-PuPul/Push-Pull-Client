using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D;

[RequireComponent(typeof(Rigidbody2D))]
public class InputPlayer : NetworkBehaviour
{
    [SerializeField]
    private ExChargeUi UI;

    [SerializeField]
    private SoundManager soundManager;

    public PlayerInput PlayerInput;
    public Transform GrabObject;
    public PushGlove PushGlove;
    public Grab GrabGlove;

    [Header("Animators")]
    public Animator Anim;

    public List<AudioClip> PlayerSounds = new List<AudioClip>();

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool moveLeft = false;
    private bool moveRight = false;
    private bool moving = false;
    private float moveSpeed = 4f;

    [SerializeField]
    private bool flip;

    [SerializeField]
    private float flipThreshold = 0.2f;

    public bool cantMove = false;
    public bool jumpAble = true;

    [Header("Knockback")]
    // 밀기 넉백(AddForce Impulse) 직후, 이동 코드가 velocity.x를 덮어써
    // 외부 힘을 지워버리는 것을 막기 위한 락아웃 시간(초).
    [SerializeField]
    private float knockbackLockoutDuration = 0.25f;

    // 이 시각 이전까지는 무조건 이동 코드가 velocity를 건드리지 않는다(최소 보장 시간).
    private float knockbackLockoutUntil = 0f;

    // 넉백 비행 중 여부. 최소 시간이 지나도 공중에 떠 있는 동안에는 제어를 돌려주지
    // 않는다(입력 없이 날아가다 공중에서 뚝 멈추는 현상 방지). 착지하거나
    // 이동 입력이 들어오면 종료되어 조작감을 되찾는다.
    private bool knockbackActive = false;

    // 넉백 해제 직후 velocity.x를 목표 속도로 즉시 대입하면 넉백 속도(예: 20)에서
    // 걷기 속도(4)로 한 스텝 만에 꺾여 순간이동처럼 보인다. 이 가속도로
    // MoveTowards 블렌딩해 자연스럽게 수렴시킨다. (단위: 속도/초)
    [SerializeField]
    private float knockbackRecoverAccel = 40f;

    // 넉백에서 일반 이동으로 복귀하는 중(속도 블렌딩 구간)
    private bool knockbackRecovering = false;

    [Header("Push / Charge")]
    private float MaxPushCharge = 35f;
    private float ChargeTime = 1f;
    public float PushCharge = 0f;
    private bool isCharging = false;
    public bool Push = false;

    public bool PushHeld { get; private set; } = false;
    public bool GrabHeld { get; private set; } = false;

    [Header("Grab / Pull")]
    private float RotSpeed = 1f;
    private float maxAngle = 35f;
    private float RPressTime = 0f;
    private float aimSmooth = 8f;
    private bool isGrabHolding = false;
    private Vector2 grabControlInput = Vector2.zero;
    private float grabDeadzone = 0.15f;

    public string ControlScheme = "Keyboard, Mouse";

    private string lastAnimName = "";

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int IsClearedHash = Animator.StringToHash("IsCleared");

    [Header("Animation")]
    [SerializeField]
    private float verticalAnimDeadZone = 0.15f;

    [SerializeField]
    private float groundRecheckDelay = 0.08f;

    private bool hasStartedFalling;
    private bool isGrounded = true;
    private float ignoreGroundUntilTime;

    private bool lastIsMoving;
    private bool lastIsJumping;
    private bool lastIsFalling;

    [SyncVar(hook = nameof(OnFlipChanged))]
    private bool syncFlip = false;

    [SyncVar(hook = nameof(OnAnimChanged))]
    private string syncAnimName = "";

    [SyncVar(hook = nameof(OnMovingChanged))]
    private bool syncIsMoving;

    [SyncVar(hook = nameof(OnJumpingChanged))]
    private bool syncIsJumping;

    [SyncVar(hook = nameof(OnFallingChanged))]
    private bool syncIsFalling;

    [SyncVar(hook = nameof(OnDeadChanged))]
    private bool syncIsDead;

    private bool clearedStarted = false;

    // 현재 밟고 있는 이동 발판(수평·수직 추종 모두 velocity가 담당)
    private IMovingSurface currentMovingSurface;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (Anim == null)
            Anim = GetComponent<Animator>();

        if (PlayerInput != null)
        {
            PlayerInput.DeactivateInput();
            PlayerInput.enabled = false;
        }

        ResetVerticalAnimationTracker();
    }

    public override void OnStartLocalPlayer()
    {
        if (PlayerInput != null)
        {
            PlayerInput.enabled = true;
            PlayerInput.ActivateInput();

            if (!string.IsNullOrEmpty(PlayerInput.currentControlScheme))
                ControlScheme = PlayerInput.currentControlScheme;
        }

        if (rb != null)
            rb.isKinematic = false;

        CameraFollow cameraFollow = Camera.main?.GetComponent<CameraFollow>();
        if (cameraFollow != null)
            cameraFollow.SetTarget(transform);

        ResetVerticalAnimationTracker();

        Debug.Log(
            $"[InputPlayer] Local player started. name={name}, netId={netId}, isLocalPlayer={isLocalPlayer}"
        );
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isLocalPlayer)
        {
            if (PlayerInput != null)
            {
                PlayerInput.DeactivateInput();
                PlayerInput.enabled = false;
            }

            ApplyLocomotionAnimatorState(syncIsMoving, syncIsJumping, syncIsFalling);
            SetAnimatorBool(IsDeadHash, syncIsDead);
        }

        Debug.Log(
            $"[InputPlayer] Client player started. name={name}, netId={netId}, isLocalPlayer={isLocalPlayer}"
        );
    }

    private void OnEnable()
    {
        if (
            PlayerInput != null
            && PlayerInput.enabled
            && !string.IsNullOrEmpty(PlayerInput.currentControlScheme)
        )
            ControlScheme = PlayerInput.currentControlScheme;

        ResetVerticalAnimationTracker();
    }

    private void Update()
    {
        if (!isLocalPlayer)
            return;
        if (Time.timeScale == 0f)
            return;
        if (cantMove)
            return;

        moving = moveRight ^ moveLeft;

        if (PushHeld)
            UI.OnPush();
        else
            UI.OffPush();

        if (GrabHeld)
            UI.OnGrab();
        else
            UI.OffGrab();

        if (
            PlayerInput != null
            && PlayerInput.enabled
            && !string.IsNullOrEmpty(PlayerInput.currentControlScheme)
        )
            ControlScheme = PlayerInput.currentControlScheme;

        if (isCharging)
        {
            float chargeRate = (ChargeTime > 0f) ? (MaxPushCharge / ChargeTime) : MaxPushCharge;
            PushCharge += chargeRate * Time.deltaTime;

            if (PushCharge > MaxPushCharge)
                PushCharge = MaxPushCharge;
        }

        if (PushGlove != null)
            PushGlove.PushPower = PushCharge;

        if (GrabHeld && GrabGlove != null && !GrabGlove.grabing && !isGrabHolding)
        {
            isGrabHolding = true;
            RPressTime = Time.time;
        }

        if (GrabGlove != null && !GrabGlove.grabing)
            UpdateGrabRotation();
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer)
            return;
        if (Time.timeScale == 0f)
            return;
        if (cantMove)
            return;

        // 좌우 이동을 rb.velocity로 처리한다. transform.Translate(텔레포트)는
        // Rigidbody2D Interpolation이 보간하지 못해 카메라 추종이 지지직거렸기 때문에,
        // 보간이 정상 동작하는 velocity 기반으로 통일한다. velocity.y(중력/점프)는 그대로
        // 보존해 물리와 자연스럽게 합쳐지게 한다.
        //
        // 단, 넉백(AddForce Impulse) 중에는 velocity를 덮어쓰지 않는다. 매 스텝
        // velocity를 대입하면 넉백으로 실린 속도가 즉시 지워지기 때문이다
        // (velocity 방식의 알려진 함정).
        UpdateKnockbackState();

        if (!knockbackActive)
        {
            // 이동 발판 위에서는 발판 속도를 목표 velocity에 반영한다.
            // - 수평(x): 발판 속도 + 입력 속도. 발판 표면과의 상대 미끄러짐이 걷기
            //   속도만큼으로 유지되어 마찰로 속도가 깎이지 않는다.
            // - 수직(y): 접지 중에는 발판의 수직 속도를 그대로 따라간다(상하 발판).
            //   공중(점프/낙하)에서는 중력이 계산한 velocity.y를 보존한다.
            bool onMovingSurface = TryGetMovingSurfaceVelocity(out Vector2 surfaceVelocity);
            float inputVelocityX = moving ? moveInput.x * moveSpeed : 0f;
            float velocityY = (onMovingSurface && isGrounded) ? surfaceVelocity.y : rb.velocity.y;

            float targetVelocityX = surfaceVelocity.x + inputVelocityX;
            float newVelocityX = targetVelocityX;

            // 넉백 복귀 중에는 즉시 대입 대신 MoveTowards로 부드럽게 수렴시킨다.
            if (knockbackRecovering)
            {
                newVelocityX = Mathf.MoveTowards(
                    rb.velocity.x,
                    targetVelocityX,
                    knockbackRecoverAccel * Time.fixedDeltaTime
                );

                if (Mathf.Approximately(newVelocityX, targetVelocityX))
                    knockbackRecovering = false;
            }

            rb.velocity = new Vector2(newVelocityX, velocityY);
        }

        if (Mathf.Abs(moveInput.x) > flipThreshold && GrabGlove != null && !GrabGlove.grabing)
        {
            if (moveInput.x > 0f && !flip)
                Flip();
            else if (moveInput.x < 0f && flip)
                Flip();
        }
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer)
            return;
        if (Time.timeScale == 0f)
            return;
        if (cantMove)
            return;

        UpdateAnimatorParameters();
    }

    public void SetGrounded(bool grounded)
    {
        if (!isLocalPlayer)
            return;

        if (grounded && Time.time < ignoreGroundUntilTime)
            return;

        bool wasGrounded = isGrounded;

        isGrounded = grounded;
        jumpAble = grounded;

        if (grounded)
        {
            hasStartedFalling = false;
            ResetVerticalAnimationTracker();

            if (!wasGrounded || IsInAirAnimatorState())
                ForceGroundedAnimatorState(Mathf.Abs(moveInput.x) > 0.01f);
        }
    }

    private void ForceGroundedAnimatorState(bool isMoving)
    {
        lastIsMoving = isMoving;
        lastIsJumping = false;
        lastIsFalling = false;

        ApplyLocomotionAnimatorState(isMoving, false, false);

        if (Anim != null)
            Anim.Play(isMoving ? "Move" : "Idle", 0, 0f);

        if (isServer)
        {
            SetSyncLocomotionAnimatorState(isMoving, false, false);
            RpcForceGroundedAnimatorState(isMoving);
        }
        else
        {
            CmdForceGroundedAnimatorState(isMoving);
        }
    }

    private bool IsInAirAnimatorState()
    {
        if (Anim == null)
            return false;

        AnimatorStateInfo current = Anim.GetCurrentAnimatorStateInfo(0);

        if (current.IsName("Jump") || current.IsName("Max") || current.IsName("Fall"))
            return true;

        if (!Anim.IsInTransition(0))
            return false;

        AnimatorStateInfo next = Anim.GetNextAnimatorStateInfo(0);

        return next.IsName("Jump") || next.IsName("Max") || next.IsName("Fall");
    }

    [Command]
    private void CmdForceGroundedAnimatorState(bool isMoving)
    {
        SetSyncLocomotionAnimatorState(isMoving, false, false);
        RpcForceGroundedAnimatorState(isMoving);
    }

    [ClientRpc]
    private void RpcForceGroundedAnimatorState(bool isMoving)
    {
        if (isLocalPlayer)
            return;

        lastIsMoving = isMoving;
        lastIsJumping = false;
        lastIsFalling = false;

        ApplyLocomotionAnimatorState(isMoving, false, false);

        if (Anim != null)
            Anim.Play(isMoving ? "Move" : "Idle", 0, 0f);
    }

    public void ForceAirborne()
    {
        ignoreGroundUntilTime = Time.time + groundRecheckDelay;
        isGrounded = false;
        jumpAble = false;
        hasStartedFalling = false;
        ResetVerticalAnimationTracker();
    }

    private void ResetVerticalAnimationTracker()
    {
        hasStartedFalling = false;
    }

    private void UpdateAnimatorParameters()
    {
        if (Anim == null)
            return;

        bool isMoving = Mathf.Abs(moveInput.x) > 0.01f;
        bool groundedForAnimation = isGrounded && Time.time >= ignoreGroundUntilTime;
        float ySpeed = rb != null ? rb.velocity.y : 0f;

        // 이동 발판 위에서는 발판의 수직 속도가 velocity.y에 실리므로(상하 발판 추종),
        // 절대 속도로 판정하면 하강 발판에서 Fall ↔ Idle이 매 프레임 번갈아 나온다.
        // 발판 기준 상대 속도로 판정하고, 발판에 접촉한 채 접지 중이면 속도 추정의
        // 프레임 지연(발판 정지/반환 순간)과 무관하게 접지 애니메이션을 유지한다.
        bool onMovingSurface = TryGetMovingSurfaceVelocity(out Vector2 animSurfaceVelocity);
        if (onMovingSurface)
            ySpeed -= animSurfaceVelocity.y;

        if (groundedForAnimation && (onMovingSurface || Mathf.Abs(ySpeed) <= verticalAnimDeadZone))
        {
            hasStartedFalling = false;
            SetLocomotionAnimatorState(isMoving, false, false);

            if (IsInAirAnimatorState())
                ForceGroundedAnimatorState(isMoving);

            return;
        }

        bool isJumping = false;
        bool isFalling = false;

        if (ySpeed > verticalAnimDeadZone)
        {
            hasStartedFalling = false;
            isJumping = true;
        }
        // 착지 프레임에는 발 트리거(isGrounded)가 먼저 잡히고 velocity.y에는 아직
        // 낙하 속도가 남아 있다. 이때 Fall로 판정하면 착지 순간 Fall이 한 프레임
        // 깜빡이므로, 접지 중에는 낙하 판정을 하지 않는다.
        else if (ySpeed < -verticalAnimDeadZone && !groundedForAnimation)
        {
            hasStartedFalling = true;
            isFalling = true;
        }
        else if (!groundedForAnimation && hasStartedFalling)
        {
            isFalling = true;
        }

        SetLocomotionAnimatorState(isMoving, isJumping, isFalling);
    }

    private void SetLocomotionAnimatorState(bool isMoving, bool isJumping, bool isFalling)
    {
        if (lastIsMoving == isMoving && lastIsJumping == isJumping && lastIsFalling == isFalling)
            return;

        lastIsMoving = isMoving;
        lastIsJumping = isJumping;
        lastIsFalling = isFalling;

        ApplyLocomotionAnimatorState(isMoving, isJumping, isFalling);

        if (!isLocalPlayer)
            return;

        if (isServer)
            SetSyncLocomotionAnimatorState(isMoving, isJumping, isFalling);
        else
            CmdSyncLocomotionAnimatorState(isMoving, isJumping, isFalling);
    }

    private void ApplyLocomotionAnimatorState(bool isMoving, bool isJumping, bool isFalling)
    {
        SetAnimatorBool(IsMovingHash, isMoving);
        SetAnimatorBool(IsJumpingHash, isJumping);
        SetAnimatorBool(IsFallingHash, isFalling);
    }

    private void SetAnimatorBool(int parameterHash, bool value)
    {
        if (Anim == null)
            return;

        Anim.SetBool(parameterHash, value);
    }

    [Command]
    private void CmdSyncLocomotionAnimatorState(bool isMoving, bool isJumping, bool isFalling)
    {
        SetSyncLocomotionAnimatorState(isMoving, isJumping, isFalling);
    }

    [Server]
    private void SetSyncLocomotionAnimatorState(bool isMoving, bool isJumping, bool isFalling)
    {
        syncIsMoving = isMoving;
        syncIsJumping = isJumping;
        syncIsFalling = isFalling;
    }

    private void OnMovingChanged(bool oldValue, bool newValue)
    {
        if (isLocalPlayer)
            return;

        SetAnimatorBool(IsMovingHash, newValue);
    }

    private void OnJumpingChanged(bool oldValue, bool newValue)
    {
        if (isLocalPlayer)
            return;

        SetAnimatorBool(IsJumpingHash, newValue);
    }

    private void OnFallingChanged(bool oldValue, bool newValue)
    {
        if (isLocalPlayer)
            return;

        SetAnimatorBool(IsFallingHash, newValue);
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        if (isLocalPlayer)
            return;

        SetAnimatorBool(IsDeadHash, newValue);
    }

    // 현재 밟고 있는 이동 발판의 월드 속도를 얻는다. 발판이 없거나 파괴됐으면 false.
    // 수평·수직 추종 모두 velocity(FixedUpdate)가 담당하므로 transform 기반 carry는
    // 더 이상 사용하지 않는다(텔레포트라 Interpolation이 보간하지 못해 떨렸음).
    private bool TryGetMovingSurfaceVelocity(out Vector2 surfaceVelocity)
    {
        surfaceVelocity = Vector2.zero;

        if (currentMovingSurface == null)
            return false;

        if (currentMovingSurface is Object surfaceObject && surfaceObject == null)
        {
            currentMovingSurface = null;
            return false;
        }

        if (!currentMovingSurface.CanCarryPlayer)
        {
            currentMovingSurface = null;
            return false;
        }

        surfaceVelocity = currentMovingSurface.CarryVelocity;
        return true;
    }

    // 발판 인식은 NewGroundCheck(발밑 트리거)가 담당한다. 접지 판정과 발판 판정을
    // 같은 소스(트리거)에서 함께 갱신하므로, collision 콜백과 트리거의 타이밍이
    // 어긋나던 레이스(착지 프레임 Fall 깜빡임 등)가 원천적으로 사라진다.
    public void SetMovingSurface(IMovingSurface movingSurface)
    {
        if (!isLocalPlayer)
            return;

        currentMovingSurface = movingSurface;
    }

    private void OnAnimChanged(string oldVal, string newVal)
    {
        if (isLocalPlayer)
            return;

        PlayAnimLocal(newVal);
    }

    public void PlayAnim(string animName)
    {
        if (string.IsNullOrEmpty(animName))
            return;

        if (animName == lastAnimName)
            return;

        lastAnimName = animName;
        PlayAnimLocal(animName);

        if (!isLocalPlayer)
            return;

        if (isServer)
            syncAnimName = animName;
        else
            CmdPlayAnimation(animName);
    }

    private void PlayAnimLocal(string animName)
    {
        if (string.IsNullOrEmpty(animName))
            return;

        Anim?.Play(animName);
    }

    [Command]
    private void CmdPlayAnimation(string animName)
    {
        syncAnimName = animName;
    }

    private void OnFlipChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer)
            return;

        ApplyFlip(newVal);
    }

    private void ApplyFlip(bool isFlipped)
    {
        Vector3 scale = transform.localScale;
        scale.x = isFlipped ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    [Command]
    private void CmdSyncFlip(bool isFlipped)
    {
        syncFlip = isFlipped;
    }

    public void SyncPunchAnim()
    {
        CmdPunchAnim();
    }

    [Command]
    private void CmdPunchAnim()
    {
        RpcPunchAnim();
    }

    [ClientRpc]
    private void RpcPunchAnim()
    {
        if (isLocalPlayer)
            return;

        PushGlove?.DoPunchAnim();
    }

    public void SyncMoveTarget(uint targetNetId, Vector3 targetPos)
    {
        if (isLocalPlayer)
            CmdMoveTarget(targetNetId, targetPos);
    }

    [Command]
    private void CmdMoveTarget(uint targetNetId, Vector3 targetPos)
    {
        RpcMoveTarget(targetNetId, targetPos);
    }

    [ClientRpc]
    private void RpcMoveTarget(uint targetNetId, Vector3 targetPos)
    {
        if (NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity identity))
            identity.transform.position = targetPos;
    }

    public void SyncApplyPush(uint targetNetId, Vector2 dir, float power)
    {
        if (isLocalPlayer)
            CmdApplyPush(targetNetId, dir, power);
    }

    [Command]
    private void CmdApplyPush(uint targetNetId, Vector2 dir, float power)
    {
        RpcApplyPush(targetNetId, dir, power);
    }

    [ClientRpc]
    private void RpcApplyPush(uint targetNetId, Vector2 dir, float power)
    {
        if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity identity))
            return;

        Rigidbody2D rigid = identity.GetComponent<Rigidbody2D>();
        if (rigid == null)
            return;

        Vector2 impulseVector = dir * power + Vector2.up * power / 2f;
        rigid.AddForce(impulseVector, ForceMode2D.Impulse);

        // 밀린 대상이 플레이어라면, 그 대상의 이동 코드가 방금 실린 넉백 velocity를
        // 덮어써 지우지 않도록 짧은 락아웃을 건다. 실제 물리는 대상의 소유 클라이언트에서만
        // 시뮬레이션되므로 그쪽에서 FixedUpdate 이동이 유예된다(다른 클라에선 무해).
        InputPlayer targetPlayer = identity.GetComponent<InputPlayer>();
        if (targetPlayer != null)
            targetPlayer.BeginKnockbackLockout();
    }

    // 넉백 락아웃 시작. 밀기(RpcApplyPush)·폭발(BoomBox) 등 외부 임펄스를 받은
    // 대상에게 호출한다.
    public void BeginKnockbackLockout()
    {
        knockbackActive = true;
        knockbackRecovering = false;
        knockbackLockoutUntil = Time.time + knockbackLockoutDuration;
    }

    // 넉백 종료 판정. 최소 락아웃 시간이 지난 뒤 착지했거나 이동 입력이 들어오면
    // 제어를 돌려준다. 고정 타이머만 쓰면 공중 비행 도중 velocity가 덮여
    // 수평으로 뚝 멈추는 부자연스러운 현상이 생긴다.
    // 종료 시에는 복귀 블렌딩(knockbackRecovering)을 켜서 넉백 속도에서 목표 속도로
    // 부드럽게 수렴시킨다.
    private void UpdateKnockbackState()
    {
        if (!knockbackActive)
            return;

        if (Time.time < knockbackLockoutUntil)
            return;

        if (isGrounded || moving)
        {
            knockbackActive = false;
            knockbackRecovering = true;
        }
    }

    public void OnMoveLeft(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer)
            return;

        if (context.started || context.performed)
            moveLeft = true;
        else if (context.canceled)
            moveLeft = false;

        UpdateMoveInput();
    }

    public void OnMoveRight(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer)
            return;

        if (context.started || context.performed)
            moveRight = true;
        else if (context.canceled)
            moveRight = false;

        UpdateMoveInput();
    }

    private void UpdateMoveInput()
    {
        if (moveLeft && moveRight)
            moveInput = Vector2.zero;
        else if (moveLeft)
            moveInput = Vector2.left;
        else if (moveRight)
            moveInput = Vector2.right;
        else
            moveInput = Vector2.zero;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer)
            return;

        if (context.performed && jumpAble)
        {
            rb.AddForce(Vector2.up * 15f, ForceMode2D.Impulse);
            ForceAirborne();

            SoundManager.Instance?.SFXPlay(
                "PlayerJump_1",
                PlayerSounds[(int)global::PlayerSounds.Jump]
            );
        }
    }

    public void OnPush(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer)
            return;

        if (context.started)
        {
            PushHeld = true;
            isCharging = true;
            PushCharge = 0f;
        }
        else if (context.canceled)
        {
            PushHeld = false;

            if (isCharging)
            {
                isCharging = false;
                Push = true;

                SoundManager.Instance?.SFXPlay(
                    "PlayerPush_1",
                    PlayerSounds[(int)global::PlayerSounds.Push]
                );

                PushGlove?.DoPunchAnim();
                SyncPunchAnim();
            }
        }
    }

    public void OnGrab(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer)
            return;

        if (context.started)
        {
            GrabHeld = true;

            if (GrabGlove != null && !GrabGlove.grabing)
            {
                isGrabHolding = true;
                RPressTime = Time.time;

                if (GrabObject != null)
                    GrabObject.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }
        else if (context.canceled)
        {
            GrabHeld = false;
            isGrabHolding = false;
            GrabGlove?.DOGrab();

            SoundManager.Instance?.SFXPlay(
                "PlayerPull_1",
                PlayerSounds[(int)global::PlayerSounds.Pull]
            );

            grabControlInput = Vector2.zero;
        }
    }

    public void OnGrabControll(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer)
            return;

        grabControlInput = context.ReadValue<Vector2>();
    }

    private void UpdateGrabRotation()
    {
        if (GrabObject == null)
            return;
        if (!isGrabHolding)
            return;

        bool isKeyboard = ControlScheme != null && ControlScheme.ToLower().Contains("keyboard");

        if (isKeyboard)
        {
            Camera cam = Camera.main;
            Mouse mouse = Mouse.current;

            if (cam != null && mouse != null)
            {
                Vector2 screenPos = mouse.position.ReadValue();
                Vector3 world = cam.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane)
                );
                world.z = GrabObject.position.z;

                Vector3 dirWorld = world - GrabObject.position;

                if (dirWorld.sqrMagnitude > 0.0001f)
                {
                    float facingSign = flip ? 1f : -1f;
                    float adjustedX = dirWorld.x * facingSign;

                    float desiredLocal = Mathf.Atan2(-dirWorld.y, adjustedX) * Mathf.Rad2Deg;

                    desiredLocal = Mathf.Clamp(
                        desiredLocal,
                        -Mathf.Abs(maxAngle),
                        Mathf.Abs(maxAngle)
                    );

                    float currentLocalZ = GrabObject.localEulerAngles.z;
                    float smoothLocalZ = Mathf.LerpAngle(
                        currentLocalZ,
                        desiredLocal,
                        Time.deltaTime * aimSmooth
                    );

                    GrabObject.localRotation = Quaternion.Euler(0f, 0f, smoothLocalZ);
                }
                else
                {
                    ApplyOscillationIfNeeded();
                }
            }
            else
            {
                ApplyOscillationIfNeeded();
            }
        }
        else
        {
            Vector2 stick = grabControlInput;

            if (stick.magnitude >= grabDeadzone)
            {
                float rawAngle = Mathf.Atan2(-stick.y, -stick.x) * Mathf.Rad2Deg;

                float desiredLocal = Mathf.Clamp(
                    rawAngle,
                    -Mathf.Abs(maxAngle),
                    Mathf.Abs(maxAngle)
                );

                float currentLocalZ = GrabObject.localEulerAngles.z;
                float smoothLocalZ = Mathf.LerpAngle(
                    currentLocalZ,
                    desiredLocal,
                    Time.deltaTime * aimSmooth
                );

                GrabObject.localRotation = Quaternion.Euler(0f, 0f, smoothLocalZ);
            }
            else
            {
                ApplyOscillationIfNeeded();
            }
        }
    }

    private void ApplyOscillationIfNeeded()
    {
        if (GrabObject == null)
            return;

        if (Time.time - RPressTime >= 0.5f)
        {
            float elapsed = Time.time - RPressTime - 0.5f;
            float angle = Mathf.Sin(elapsed * RotSpeed) * maxAngle;
            float currentLocalZ = GrabObject.localEulerAngles.z;
            float smoothLocalZ = Mathf.LerpAngle(currentLocalZ, angle, Time.deltaTime * aimSmooth);
            GrabObject.localRotation = Quaternion.Euler(0f, 0f, smoothLocalZ);
        }
        else
        {
            float currentLocalZ = GrabObject.localEulerAngles.z;
            float smoothLocalZ = Mathf.LerpAngle(currentLocalZ, 0f, Time.deltaTime * aimSmooth);
            GrabObject.localRotation = Quaternion.Euler(0f, 0f, smoothLocalZ);
        }
    }

    public void Flip()
    {
        flip = !flip;
        ApplyFlip(flip);

        if (isLocalPlayer)
            CmdSyncFlip(flip);
    }

    public bool ConsumePush(out float outCharge)
    {
        if (Push)
        {
            outCharge = PushCharge;
            Push = false;
            PushCharge = 0f;
            return true;
        }

        outCharge = 0f;
        return false;
    }

    public void Die()
    {
        if (!isLocalPlayer)
            return;

        SoundManager.Instance?.SFXPlay("PlayerDied_1", PlayerSounds[(int)global::PlayerSounds.Die]);

        cantMove = true;
        SetLocomotionAnimatorState(false, false, false);
        SetAnimatorBool(IsDeadHash, true);

        if (isServer)
            syncIsDead = true;
        else
            CmdSyncDead(true);
    }

    [Command]
    private void CmdSyncDead(bool isDead)
    {
        syncIsDead = isDead;
    }

    public void Cleared()
    {
        if (isServer)
        {
            RpcCleared();
            return;
        }

        if (isLocalPlayer)
            CmdCleared();
    }

    [Command]
    private void CmdCleared()
    {
        RpcCleared();
    }

    [ClientRpc]
    private void RpcCleared()
    {
        if (clearedStarted)
            return;

        clearedStarted = true;

        cantMove = true;
        ApplyLocomotionAnimatorState(false, false, false);
        SetAnimatorBool(IsClearedHash, true);

        if (gameObject.TryGetComponent<SpriteRenderer>(out SpriteRenderer sprite))
            StartCoroutine(FadeOutSprite(sprite, 0.5f));
    }

    private IEnumerator FadeOutSprite(SpriteRenderer sprite, float duration)
    {
        Color startColor = sprite.color;
        float startAlpha = startColor.a;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            Color newColor = sprite.color;
            newColor.a = Mathf.Lerp(startAlpha, 0f, t);
            sprite.color = newColor;

            yield return null;
        }

        Color finalColor = sprite.color;
        finalColor.a = 0f;
        sprite.color = finalColor;
    }
}
