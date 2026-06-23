using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerEyes : NetworkBehaviour
{
    [Header("Eye Position Range")]
    [SerializeField]
    private float leftX = 0.1f;

    [SerializeField]
    private float rightX = -0.2f;

    [SerializeField]
    private float downY = -0.3f;

    [SerializeField]
    private float upY = 0.3f;

    [Header("Target")]
    [SerializeField]
    private Transform playerRoot;

    [Header("Settings")]
    [SerializeField]
    private float sendInterval = 0.05f;

    [SerializeField]
    private float moveSmooth = 20f;

    [SyncVar(hook = nameof(OnEyePositionChanged))]
    private Vector2 syncedLocalPosition;

    private Camera mainCamera;
    private float lastSendTime;
    private Vector3 targetLocalPosition;
    private float fixedZ;

    private void Awake()
    {
        mainCamera = Camera.main;
        fixedZ = transform.localPosition.z;

        if (playerRoot == null)
        {
            playerRoot = transform.root;
        }

        targetLocalPosition = transform.localPosition;
        syncedLocalPosition = new Vector2(transform.localPosition.x, transform.localPosition.y);
    }

    private void Update()
    {
        if (isLocalPlayer)
        {
            UpdateLocalEyeByMouse();
        }

        ApplyEyePositionSmooth();
    }

    private void UpdateLocalEyeByMouse()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                return;
        }

        if (Mouse.current == null)
            return;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(
            new Vector3(
                mouseScreenPosition.x,
                mouseScreenPosition.y,
                -mainCamera.transform.position.z
            )
        );

        Vector2 direction = mouseWorldPosition - playerRoot.position;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();

        if (!IsPlayerFlipped())
        {
            direction.x *= -1f;
        }

        float tX = (direction.x + 1f) * 0.5f;
        float tY = (direction.y + 1f) * 0.5f;

        Vector2 nextLocalPosition = new Vector2(
            Mathf.Lerp(leftX, rightX, tX),
            Mathf.Lerp(downY, upY, tY)
        );

        targetLocalPosition = new Vector3(nextLocalPosition.x, nextLocalPosition.y, fixedZ);

        if (Time.time - lastSendTime >= sendInterval)
        {
            lastSendTime = Time.time;
            CmdSetEyePosition(nextLocalPosition);
        }
    }

    private bool IsPlayerFlipped()
    {
        if (playerRoot == null)
            return false;

        return playerRoot.localScale.x < 0f;
    }

    private void ApplyEyePositionSmooth()
    {
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetLocalPosition,
            Time.deltaTime * moveSmooth
        );
    }

    [Command]
    private void CmdSetEyePosition(Vector2 localPosition)
    {
        syncedLocalPosition = localPosition;
    }

    private void OnEyePositionChanged(Vector2 oldPosition, Vector2 newPosition)
    {
        targetLocalPosition = new Vector3(newPosition.x, newPosition.y, fixedZ);
    }
}
