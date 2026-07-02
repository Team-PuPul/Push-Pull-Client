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

    [Header("Moving Surface")]
    [SerializeField]
    private float againstPlatformBoost = 0.5f;

    private IMovingSurface currentMovingSurface;
    private Vector3 lastMovingSurfacePosition;

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
        {
            rb.isKinematic = false;

            // 로컬 플레이어의 물리 프레임 사이 렌더링 위치 보간
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

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

        if (moving)
        {
            Vector3 move = new Vector3(moveInput.x * moveSpeed * Time.fixedDeltaTime, 0f, 0f);

            transform.Translate(move);
        }

        // 발판 이동도 플레이어 이동과 같은 물리 주기에서 처리
        ApplyMovingSurfaceCarry();

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

        // ApplyMovingSurfaceCarry()는 FixedUpdate로 이동
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

        if (groundedForAnimation && Mathf.Abs(ySpeed) <= verticalAnimDeadZone)
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
        else if (ySpeed < -verticalAnimDeadZone)
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

    private void ApplyMovingSurfaceCarry()
    {
        if (currentMovingSurface == null)
            return;

        if (currentMovingSurface is Object surfaceObject && surfaceObject == null)
        {
            currentMovingSurface = null;
            return;
        }

        if (!currentMovingSurface.CanCarryPlayer)
        {
            currentMovingSurface = null;
            return;
        }

        Vector3 surfacePosition = currentMovingSurface.CarryPosition;
        Vector3 surfaceDelta = surfacePosition - lastMovingSurfacePosition;

        Vector3 extraMove = Vector3.zero;

        if (Mathf.Abs(moveInput.x) > 0.01f && Mathf.Abs(surfaceDelta.x) > 0.0001f)
        {
            bool movingAgainstPlatform = Mathf.Sign(moveInput.x) != Mathf.Sign(surfaceDelta.x);

            if (movingAgainstPlatform)
                extraMove.x = moveInput.x * Mathf.Abs(surfaceDelta.x) * againstPlatformBoost;
        }

        Vector3 carryDelta = surfaceDelta + extraMove;

        if (carryDelta != Vector3.zero)
            transform.position += carryDelta;

        lastMovingSurfacePosition = surfacePosition;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!isLocalPlayer)
            return;

        if (!TryGetMovingSurface(collision, out IMovingSurface movingSurface))
            return;

        if (!IsGroundContact(collision))
        {
            if (ReferenceEquals(currentMovingSurface, movingSurface))
                currentMovingSurface = null;

            return;
        }

        if (ReferenceEquals(currentMovingSurface, movingSurface))
            return;

        currentMovingSurface = movingSurface;
        lastMovingSurfacePosition = movingSurface.CarryPosition;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!isLocalPlayer)
            return;

        if (!TryGetMovingSurface(collision, out IMovingSurface movingSurface))
            return;

        if (ReferenceEquals(currentMovingSurface, movingSurface))
            currentMovingSurface = null;
    }

    private bool TryGetMovingSurface(Collision2D collision, out IMovingSurface movingSurface)
    {
        movingSurface = collision.collider.GetComponentInParent<IMovingSurface>();
        return movingSurface != null && movingSurface.CanCarryPlayer;
    }

    private bool IsGroundContact(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            if (contact.normal.y > 0.5f)
                return true;
        }

        return false;
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
