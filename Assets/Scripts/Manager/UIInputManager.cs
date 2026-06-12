using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum InputDeviceType { Mouse, Keyboard, Gamepad }

public class UIInputManager : MonoBehaviour
{
    public static UIInputManager instance;

    // 현재 입력 상태를 외부(UIButton)에서 확인할 수 있도록 속성 정의
    public InputDeviceType currentDevice { get; private set; } = InputDeviceType.Mouse;

    // 시스템이 선택을 복구할 때(빈 공간 클릭 후 등) 호버 사운드를 억제하기 위한 플래그.
    // UIButton.PlayHover가 이 값을 보고 시스템 유발 선택은 무음 처리한다.
    public bool SuppressHoverSound { get; private set; }

    private GameObject lastSelected;

    private void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Update()
    {
        CheckInputDevice();
        KeepSelection();
    }

    private void KeepSelection()
    {
        if (EventSystem.current == null) return;

        // 1. 현재 선택된 오브젝트가 있다면 기록해둠
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            lastSelected = EventSystem.current.currentSelectedGameObject;
        }
        else
        {
            // 2. 선택이 풀렸으면(빈 공간 클릭 등) 직전 오브젝트로 복구.
            //    단, 이 복구는 사용자가 직접 호버한 게 아니므로 호버 사운드를 억제한다.
            if (lastSelected != null && lastSelected.activeInHierarchy)
            {
                SuppressHoverSound = true;
                // SetSelectedGameObject는 내부적으로 OnSelect를 동기 호출하므로,
                // 플래그를 켠 상태에서 호출하면 PlayHover가 무음으로 처리된다.
                EventSystem.current.SetSelectedGameObject(lastSelected);
                SuppressHoverSound = false;
            }
        }
    }

    private void CheckInputDevice()
    {
        // 1. 키보드 입력 체크 (방향키, 엔터, 스페이스 등 UI 조작 키 위주)
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            UpdateDevice(InputDeviceType.Keyboard);
        }

        // 2. 마우스 입력 체크 (움직임이 일정 수준 이상일 때만)
        if (Mouse.current != null)
        {
            // 아주 미세한 떨림에 반응하지 않도록 sqrMagnitude 사용 (최적화)
            bool isMouseMoving = Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f;
            bool isMouseClicked = Mouse.current.leftButton.wasPressedThisFrame ||
                                 Mouse.current.rightButton.wasPressedThisFrame;

            if (isMouseMoving || isMouseClicked)
            {
                UpdateDevice(InputDeviceType.Mouse);
            }
        }
    }

    private void UpdateDevice(InputDeviceType newType)
    {        
        if (currentDevice == newType) return; // 이미 해당 모드라면 무시

        bool isTypeMouse = (newType == InputDeviceType.Mouse);

        // 키보드 모드일 때 커서를 숨기고 싶다면:
        Cursor.visible = isTypeMouse;
        // Cursor.lockState = isTypeMouse ? CursorLockMode.Locked : CursorLockMode.None;

        currentDevice = newType;

        // 디버깅용 로그
        Debug.Log($"🔌 입력 기기 전환: {newType}");
    }
}