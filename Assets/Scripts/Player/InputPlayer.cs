using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerPushController))]
[RequireComponent(typeof(PlayerGrabController))]
public class InputPlayer : NetworkBehaviour
{
    [SerializeField]
    private ExChargeUi UI;

    public PlayerInput PlayerInput;
    public Transform GrabObject;
    public PushGlove PushGlove;
    public Grab GrabGlove;

    [Header("Animators")]
    public Animator Anim;

    public List<AudioClip> PlayerSounds = new List<AudioClip>();
    public bool cantMove;
    public string ControlScheme = "Keyboard, Mouse";

    [Header("Animation")]
    [SerializeField]
    private float verticalAnimDeadZone = 0.15f;

    [SerializeField]
    private float groundRecheckDelay = 0.08f;

    private Rigidbody2D rb;
    private string lastAnimName = "";
    private bool hasStartedFalling;
    private bool isGrounded = true;
    private float ignoreGroundUntilTime;
    private bool lastIsMoving;
    private bool lastIsJumping;
    private bool lastIsFalling;
    private bool clearedStarted;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int IsClearedHash = Animator.StringToHash("IsCleared");

    [SyncVar(hook = nameof(OnFlipChanged))]
    private bool syncFlip;

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

    public PlayerMovement Movement { get; private set; }
    public PlayerPushController PushController { get; private set; }
    public PlayerGrabController GrabController { get; private set; }

    internal ExChargeUi ChargeUI => UI;
    internal bool CanProcessGameplay => isLocalPlayer && Time.timeScale != 0f && !cantMove;

    // 기존 외부 코드가 InputPlayer를 통해 상태를 읽고 쓰는 API는 그대로 유지한다.
    public bool jumpAble
    {
        get => Movement != null && Movement.JumpAble;
        set
        {
            if (Movement != null)
                Movement.JumpAble = value;
        }
    }

    public float PushCharge
    {
        get => PushController != null ? PushController.PushCharge : 0f;
        set
        {
            if (PushController != null)
                PushController.PushCharge = value;
        }
    }

    public bool Push
    {
        get => PushController != null && PushController.IsPushing;
        set
        {
            if (PushController != null)
                PushController.IsPushing = value;
        }
    }

    public bool PushHeld => PushController != null && PushController.PushHeld;
    public bool GrabHeld => GrabController != null && GrabController.GrabHeld;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        Movement = GetComponent<PlayerMovement>();
        PushController = GetComponent<PlayerPushController>();
        GrabController = GetComponent<PlayerGrabController>();

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
            RefreshControlScheme();
        }

        PlayerVisualInterpolator visualInterpolator = GetComponent<PlayerVisualInterpolator>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.interpolation =
                visualInterpolator != null
                    ? RigidbodyInterpolation2D.None
                    : RigidbodyInterpolation2D.Interpolate;
        }

        CameraFollow cameraFollow = Camera.main?.GetComponent<CameraFollow>();

        if (cameraFollow != null)
            cameraFollow.SetTarget(
                visualInterpolator != null ? visualInterpolator.CameraTarget : transform
            );

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
        RefreshControlScheme();
        ResetVerticalAnimationTracker();
    }

    private void Update()
    {
        if (CanProcessGameplay)
            RefreshControlScheme();
    }

    private void LateUpdate()
    {
        if (CanProcessGameplay)
            UpdateAnimatorParameters();
    }

    private void RefreshControlScheme()
    {
        if (
            PlayerInput != null
            && PlayerInput.enabled
            && !string.IsNullOrEmpty(PlayerInput.currentControlScheme)
        )
            ControlScheme = PlayerInput.currentControlScheme;
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

        if (!grounded)
            return;

        hasStartedFalling = false;
        ResetVerticalAnimationTracker();

        if (!wasGrounded || IsInAirAnimatorState())
            ForceGroundedAnimatorState(Mathf.Abs(GetMoveInput().x) > 0.01f);
    }

    public void ForceAirborne()
    {
        ignoreGroundUntilTime = Time.time + groundRecheckDelay;
        isGrounded = false;
        jumpAble = false;
        hasStartedFalling = false;
        ResetVerticalAnimationTracker();
    }

    private Vector2 GetMoveInput()
    {
        return Movement != null ? Movement.MoveInput : Vector2.zero;
    }

    private void ResetVerticalAnimationTracker()
    {
        hasStartedFalling = false;
    }

    private void UpdateAnimatorParameters()
    {
        if (Anim == null)
            return;

        bool isMoving = Mathf.Abs(GetMoveInput().x) > 0.01f;
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
        if (Anim != null)
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
        if (!isLocalPlayer)
            SetAnimatorBool(IsMovingHash, newValue);
    }

    private void OnJumpingChanged(bool oldValue, bool newValue)
    {
        if (!isLocalPlayer)
            SetAnimatorBool(IsJumpingHash, newValue);
    }

    private void OnFallingChanged(bool oldValue, bool newValue)
    {
        if (!isLocalPlayer)
            SetAnimatorBool(IsFallingHash, newValue);
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        if (!isLocalPlayer)
            SetAnimatorBool(IsDeadHash, newValue);
    }

    private void OnAnimChanged(string oldValue, string newValue)
    {
        if (!isLocalPlayer)
            PlayAnimLocal(newValue);
    }

    public void PlayAnim(string animName)
    {
        if (string.IsNullOrEmpty(animName) || animName == lastAnimName)
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
        if (!string.IsNullOrEmpty(animName))
            Anim?.Play(animName);
    }

    [Command]
    private void CmdPlayAnimation(string animName)
    {
        syncAnimName = animName;
    }

    private void OnFlipChanged(bool oldValue, bool newValue)
    {
        if (!isLocalPlayer)
            Movement?.ApplyFlip(newValue);
    }

    internal void SyncFlip(bool isFlipped)
    {
        CmdSyncFlip(isFlipped);
    }

    [Command]
    private void CmdSyncFlip(bool isFlipped)
    {
        syncFlip = isFlipped;
    }

    public void Flip()
    {
        Movement?.Flip();
    }

    public void SyncPunchAnim()
    {
        if (isLocalPlayer)
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
        if (!isLocalPlayer)
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
        // 서버 권한 물리 오브젝트(NetworkTransform이 ServerToClient)는 서버에서만 위치를 옮기고
        // NetworkTransform이 모든 클라이언트에 위치를 동기화한다.
        if (NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity identity)
            && IsServerAuthoritativePhysics(identity))
        {
            identity.transform.position = targetPos;
            return;
        }

        // 그 외 오브젝트는 기존 방식대로 전 클라이언트에 위치를 직접 반영한다.
        RpcMoveTarget(targetNetId, targetPos);
    }

    [ClientRpc]
    private void RpcMoveTarget(uint targetNetId, Vector3 targetPos)
    {
        if (NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity identity))
            identity.transform.position = targetPos;
    }

    public void SyncApplyPush(uint targetNetId, Vector2 direction, float power)
    {
        if (isLocalPlayer)
            CmdApplyPush(targetNetId, direction, power);
    }

    [Command]
    private void CmdApplyPush(uint targetNetId, Vector2 direction, float power)
    {
        // 서버 권한 물리 오브젝트는 서버에서만 힘을 적용하고 NetworkTransform이 위치를 동기화한다.
        // (전 클라이언트가 각자 힘을 적용하면 물리 시뮬레이션이 어긋나 위치가 벌어진다.)
        if (NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity identity)
            && IsServerAuthoritativePhysics(identity))
        {
            ApplyPushForceTo(identity, direction, power);
            return;
        }

        // 그 외 오브젝트는 기존 방식대로 전 클라이언트가 동일한 힘을 적용한다.
        RpcApplyPush(targetNetId, direction, power);
    }

    [ClientRpc]
    private void RpcApplyPush(uint targetNetId, Vector2 direction, float power)
    {
        if (NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity identity))
            ApplyPushForceTo(identity, direction, power);
    }

    // 대상 Rigidbody2D에 밀치기 힘(옆 방향 + 살짝 위)을 가한다.
    private void ApplyPushForceTo(NetworkIdentity identity, Vector2 direction, float power)
    {
        Rigidbody2D targetBody = identity.GetComponent<Rigidbody2D>();

        if (targetBody == null)
            return;

        Vector2 impulse = direction * power + Vector2.up * power / 2f;
        targetBody.AddForce(impulse, ForceMode2D.Impulse);
    }

    // 대상이 서버 권한 물리인지 판별한다.
    // NetworkTransform이 ServerToClient(서버 권한)로 설정돼 있으면 서버가 물리를 시뮬레이션한다.
    private static bool IsServerAuthoritativePhysics(NetworkIdentity identity)
    {
        NetworkTransformBase netTransform = identity.GetComponent<NetworkTransformBase>();

        return netTransform != null
            && netTransform.syncDirection == SyncDirection.ServerToClient;
    }

    // PlayerInput의 기존 Unity Event 연결을 유지하는 전달용 콜백들이다.
    public void OnMoveLeft(InputAction.CallbackContext context)
    {
        Movement?.HandleMoveLeft(context);
    }

    public void OnMoveRight(InputAction.CallbackContext context)
    {
        Movement?.HandleMoveRight(context);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        Movement?.HandleJump(context);
    }

    public void OnPush(InputAction.CallbackContext context)
    {
        PushController?.HandleInput(context);
    }

    public void OnGrab(InputAction.CallbackContext context)
    {
        GrabController?.HandleInput(context);
    }

    public void OnGrabControll(InputAction.CallbackContext context)
    {
        GrabController?.HandleControlInput(context);
    }

    public bool ConsumePush(out float charge)
    {
        if (PushController != null)
            return PushController.ConsumePush(out charge);

        charge = 0f;
        return false;
    }

    internal void PlayPlayerSound(string soundName, global::PlayerSounds sound)
    {
        int soundIndex = (int)sound;

        if (soundIndex < 0 || soundIndex >= PlayerSounds.Count)
            return;

        SoundManager.Instance?.SFXPlay(soundName, PlayerSounds[soundIndex]);
    }

    public void Die()
    {
        if (!isLocalPlayer)
            return;

        PlayPlayerSound("PlayerDied_1", global::PlayerSounds.Die);
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

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (sprites.Length > 0 || texts.Length > 0)
            StartCoroutine(FadeOutClearVisuals(sprites, texts, 0.5f));
    }

    private IEnumerator FadeOutClearVisuals(
        SpriteRenderer[] sprites,
        TMP_Text[] texts,
        float duration
    )
    {
        float[] startSpriteAlphas = new float[sprites.Length];
        float[] startTextAlphas = new float[texts.Length];

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                startSpriteAlphas[i] = sprites[i].color.a;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                startTextAlphas[i] = texts[i].color.a;
        }

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];

                if (sprite == null)
                    continue;

                Color newColor = sprite.color;
                newColor.a = Mathf.Lerp(startSpriteAlphas[i], 0f, t);
                sprite.color = newColor;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];

                if (text == null)
                    continue;

                Color newColor = text.color;
                newColor.a = Mathf.Lerp(startTextAlphas[i], 0f, t);
                text.color = newColor;
            }

            yield return null;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];

            if (sprite == null)
                continue;

            Color finalColor = sprite.color;
            finalColor.a = 0f;
            sprite.color = finalColor;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null)
                continue;

            Color finalColor = text.color;
            finalColor.a = 0f;
            text.color = finalColor;
        }
    }
}
