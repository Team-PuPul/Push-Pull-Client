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

        Vector3 mouseOffset = GetMouseOffset();
        currentMouseOffset = Vector3.Lerp(
            currentMouseOffset,
            mouseOffset,
            mouseLookSmooth * Time.deltaTime
        );

        Vector3 targetPosition = target.position + baseOffset + currentMouseOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSmooth * Time.deltaTime
        );
    }

    private Vector3 GetMouseOffset()
    {
        if (Mouse.current == null)
            return Vector3.zero;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 direction = mouseScreenPos - screenCenter;

        if (direction.sqrMagnitude < 1f)
            return Vector3.zero;

        Vector2 normalizedDirection = direction.normalized;

        return new Vector3(
            normalizedDirection.x * mouseLookDistance,
            normalizedDirection.y * mouseLookDistance,
            0f
        );
    }
}
