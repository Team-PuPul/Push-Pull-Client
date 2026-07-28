using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerGrabController : MonoBehaviour
{
    [SerializeField]
    private float rotationSpeed = 1f;

    [SerializeField]
    private float maxAngle = 35f;

    [SerializeField]
    private float aimSmooth = 8f;

    [SerializeField]
    private float grabDeadzone = 0.15f;

    [SerializeField]
    private float oscillationDelay = 0.5f;

    [SerializeField]
    [Min(0f)]
    private float rotationSyncInterval = 0.05f;

    [SerializeField]
    [Min(0f)]
    private float rotationSyncAngleThreshold = 0.5f;

    private InputPlayer player;
    private float pressStartedAt;
    private float nextRotationSyncTime;
    private float lastSyncedRotationAngle;
    private bool isGrabHolding;
    private bool hasSyncedRotationAngle;
    private Vector2 grabControlInput;

    public bool GrabHeld { get; private set; }

    private void Awake()
    {
        player = GetComponent<InputPlayer>();
    }

    private void Update()
    {
        if (!player.CanProcessGameplay)
            return;

        if (GrabHeld)
            player.ChargeUI?.OnGrab();
        else
            player.ChargeUI?.OffGrab();

        HandleMouseInput();

        if (GrabHeld && player.GrabGlove != null && !player.GrabGlove.grabing && !isGrabHolding)
        {
            isGrabHolding = true;
            pressStartedAt = Time.time;
        }

        if (player.GrabGlove != null && !player.GrabGlove.grabing)
            UpdateRotation();
    }

    public void HandleInput(InputAction.CallbackContext context)
    {
        if (!player.isLocalPlayer)
            return;

        if (!player.CanProcessGameplay)
        {
            ResetStageSpawnState();
            return;
        }

        if (context.started)
            BeginGrabInput();
        else if (context.canceled)
            EndGrabInput();
    }

    public void HandleControlInput(InputAction.CallbackContext context)
    {
        if (player.isLocalPlayer && player.CanProcessGameplay)
            grabControlInput = context.ReadValue<Vector2>();
    }

    private void HandleMouseInput()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            player.ControlScheme = "Keyboard, Mouse";
            BeginGrabInput();
        }

        if (mouse.rightButton.wasReleasedThisFrame)
            EndGrabInput();
    }

    private void BeginGrabInput()
    {
        if (GrabHeld)
            return;

        GrabHeld = true;

        if (player.GrabGlove == null || player.GrabGlove.grabing)
            return;

        isGrabHolding = true;
        pressStartedAt = Time.time;

        if (player.GrabObject != null)
        {
            ApplyGrabRotation(0f);
            SyncGrabRotation(0f, true);
        }
    }

    private void EndGrabInput()
    {
        if (!GrabHeld)
            return;

        GrabHeld = false;
        isGrabHolding = false;
        SyncGrabRotation(GetGrabRotationAngle(), true);
        player.GrabGlove?.DOGrab();
        player.PlayPlayerSound("PlayerPull_1", global::PlayerSounds.Pull);
        grabControlInput = Vector2.zero;
    }

    private void UpdateRotation()
    {
        if (player.GrabObject == null || !isGrabHolding)
            return;

        bool isKeyboard =
            player.ControlScheme != null
            && player.ControlScheme.ToLowerInvariant().Contains("keyboard");

        if (isKeyboard)
            UpdateMouseRotation();
        else
            UpdateStickRotation();
    }

    private void UpdateMouseRotation()
    {
        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;

        if (cam == null || mouse == null)
        {
            ApplyOscillationIfNeeded();
            return;
        }

        Vector2 screenPos = mouse.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane)
        );
        world.z = player.GrabObject.position.z;

        Vector3 direction = world - player.GrabObject.position;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            ApplyOscillationIfNeeded();
            return;
        }

        float facingSign = player.Movement.IsFlipped ? 1f : -1f;
        float desiredAngle = Mathf.Atan2(-direction.y, direction.x * facingSign) * Mathf.Rad2Deg;
        SetSmoothedAngle(Mathf.Clamp(desiredAngle, -Mathf.Abs(maxAngle), Mathf.Abs(maxAngle)));
    }

    private void UpdateStickRotation()
    {
        if (grabControlInput.magnitude < grabDeadzone)
        {
            ApplyOscillationIfNeeded();
            return;
        }

        float rawAngle = Mathf.Atan2(-grabControlInput.y, -grabControlInput.x) * Mathf.Rad2Deg;
        SetSmoothedAngle(Mathf.Clamp(rawAngle, -Mathf.Abs(maxAngle), Mathf.Abs(maxAngle)));
    }

    private void ApplyOscillationIfNeeded()
    {
        float angle = 0f;

        if (Time.time - pressStartedAt >= oscillationDelay)
        {
            float elapsed = Time.time - pressStartedAt - oscillationDelay;
            angle = Mathf.Sin(elapsed * rotationSpeed) * maxAngle;
        }

        SetSmoothedAngle(angle);
    }

    private void SetSmoothedAngle(float targetAngle)
    {
        float currentAngle = player.GrabObject.localEulerAngles.z;
        float smoothAngle = Mathf.LerpAngle(
            currentAngle,
            targetAngle,
            Time.deltaTime * aimSmooth
        );
        ApplyGrabRotation(smoothAngle);
        SyncGrabRotation(smoothAngle, false);
    }

    public void ApplyRemoteGrabRotation(float localZAngle)
    {
        ApplyGrabRotation(localZAngle);
    }

    private void ApplyGrabRotation(float localZAngle)
    {
        if (player.GrabObject != null)
            player.GrabObject.localRotation = Quaternion.Euler(0f, 0f, localZAngle);
    }

    private float GetGrabRotationAngle()
    {
        return player.GrabObject != null ? player.GrabObject.localEulerAngles.z : 0f;
    }

    private void SyncGrabRotation(float localZAngle, bool force)
    {
        if (!player.isLocalPlayer)
            return;

        float normalizedAngle = NormalizeAngle(localZAngle);
        float angleDelta = Mathf.Abs(
            Mathf.DeltaAngle(lastSyncedRotationAngle, normalizedAngle)
        );

        if (
            !force
            && hasSyncedRotationAngle
            && (Time.time < nextRotationSyncTime || angleDelta < rotationSyncAngleThreshold)
        )
            return;

        hasSyncedRotationAngle = true;
        lastSyncedRotationAngle = normalizedAngle;
        nextRotationSyncTime = Time.time + rotationSyncInterval;
        player.SyncGrabRotation(normalizedAngle);
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
    }

    public void ResetStageSpawnState()
    {
        GrabHeld = false;
        isGrabHolding = false;
        grabControlInput = Vector2.zero;
        pressStartedAt = 0f;
        nextRotationSyncTime = 0f;
        lastSyncedRotationAngle = 0f;
        hasSyncedRotationAngle = false;
        player.ChargeUI?.OffGrab();

        if (player.GrabObject != null)
            ApplyGrabRotation(0f);
    }
}
