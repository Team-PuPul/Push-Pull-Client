using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [SerializeField]
    private Vector3 baseOffset = new Vector3(0f, 0f, -10f);

    [SerializeField]
    private float followSmooth = 8f;

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
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        // Lerp(a, b, smooth * deltaTime)은 프레임레이트에 따라 추종 속도가 달라져
        // 떨림/끊김의 원인이 된다. 1 - exp(-smooth * deltaTime)을 보간 계수로 쓰면
        // 어떤 프레임레이트에서도 동일한 추종 속도를 보장한다.
        Vector3 mouseOffset = GetMouseOffset();
        float mouseT = 1f - Mathf.Exp(-mouseLookSmooth * Time.deltaTime);
        currentMouseOffset = Vector3.Lerp(currentMouseOffset, mouseOffset, mouseT);

        Vector3 targetPosition = target.position + baseOffset + currentMouseOffset;

        float followT = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, followT);
    }

    private Vector3 GetMouseOffset()
    {
        if (Mouse.current == null)
            return Vector3.zero;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 halfScreen = screenCenter;

        Vector2 fromCenter = mouseScreenPos - screenCenter;

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

        float adjustedMagnitude = Mathf.InverseLerp(deadZone, 1f, magnitude);
        return value.normalized * adjustedMagnitude;
    }
}
