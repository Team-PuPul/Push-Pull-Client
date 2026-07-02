using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class CameraFollow : MonoBehaviour
{
    [SerializeField]
    private Vector3 baseOffset = new Vector3(0f, 0f, -10f);

    [Header("Camera Follow")]
    [SerializeField]
    private float followSmooth = 15f;

    [SerializeField]
    private float teleportSnapDistance = 5f;

    [Header("Mouse Look Ahead")]
    [SerializeField]
    private float mouseLookDistance = 2f;

    [SerializeField]
    private float mouseLookSmooth = 10f;

    [SerializeField]
    private float mouseDeadZone = 0.12f;

    private Transform target;
    private Vector3 currentMouseOffset;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
        {
            transform.position = target.position + baseOffset + currentMouseOffset;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        UpdateMouseOffset();

        Vector3 targetCameraPosition = target.position + baseOffset + currentMouseOffset;

        float distance = Vector3.Distance(transform.position, targetCameraPosition);

        // 리스폰이나 순간이동 시에는 Lerp하지 않고 즉시 이동한다.
        if (distance >= teleportSnapDistance)
        {
            transform.position = targetCameraPosition;
            return;
        }

        // 프레임률에 독립적인 Lerp 계수
        float followLerpFactor = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);

        transform.position = Vector3.Lerp(
            transform.position,
            targetCameraPosition,
            followLerpFactor
        );
    }

    private void UpdateMouseOffset()
    {
        Vector3 desiredMouseOffset = GetMouseOffset();

        float mouseLerpFactor = 1f - Mathf.Exp(-mouseLookSmooth * Time.deltaTime);

        currentMouseOffset = Vector3.Lerp(currentMouseOffset, desiredMouseOffset, mouseLerpFactor);
    }

    private Vector3 GetMouseOffset()
    {
        if (Mouse.current == null)
            return Vector3.zero;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 halfScreen = new Vector2(
            Mathf.Max(screenCenter.x, 1f),
            Mathf.Max(screenCenter.y, 1f)
        );

        Vector2 fromCenter = mouseScreenPosition - screenCenter;

        Vector2 influence = new Vector2(
            Mathf.Clamp(fromCenter.x / halfScreen.x, -1f, 1f),
            Mathf.Clamp(fromCenter.y / halfScreen.y, -1f, 1f)
        );

        influence = ApplyDeadZone(influence, mouseDeadZone);

        return new Vector3(influence.x * mouseLookDistance, influence.y * mouseLookDistance, 0f);
    }

    private Vector2 ApplyDeadZone(Vector2 value, float deadZone)
    {
        float magnitude = value.magnitude;

        if (magnitude <= deadZone)
            return Vector2.zero;

        float adjustedMagnitude = Mathf.InverseLerp(deadZone, 1f, Mathf.Min(magnitude, 1f));

        return value.normalized * adjustedMagnitude;
    }
}
