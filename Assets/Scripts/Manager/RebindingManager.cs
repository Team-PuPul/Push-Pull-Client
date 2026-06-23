using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

public class RebindingManager : MonoBehaviour
{
    // 기존 매니저(SoundManager 등)와 동일한 싱글톤 패턴
    public static RebindingManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindObjectOfType<RebindingManager>();
            return _instance;
        }
    }
    private static RebindingManager _instance;

    // 리바인딩 진행 중 여부. 이 동안에는 UI 버튼 클릭을 무시한다. (UGUI 입력만 차단되고 디바이스 캡처는 정상 동작)
    public static bool IsRebinding { get; private set; }

    [SerializeField] private InputActionAsset inputActions;

    private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

    private const string InputBindingsKey = "InputBindings";

    private readonly HashSet<string> _lockedActions = new HashSet<string>
    {
        "Menu",
        "GrabControll",
        "UIControll"
    };

    private void Awake()
    {
        if (_instance == null) _instance = this;
    }

    private void Start()
    {
        LoadBindings();
    }

    // onFinished: 리바인딩이 완료/취소된 뒤 호출되는 콜백 (UI 라벨 갱신 등에 사용)
    public void StartRebinding(InputActionReference actionReference, string controlScheme, Action onFinished = null)
    {
        if (IsRebinding) return;            // 이미 다른 리바인딩이 진행 중이면 무시
        if (actionReference == null) return;


        InputAction action = actionReference.action;

        if (_lockedActions.Contains(action.name)) return;


        int bindingIndex = action.GetBindingIndex(
            InputBinding.MaskByGroup(controlScheme)
        );

        if (bindingIndex == -1) return;

        // 교체(swap)용: 변경 전 키 경로를 저장해 둔다
        string previousPath = action.bindings[bindingIndex].effectivePath;

        IsRebinding = true;
        action.Disable();

        _rebindOperation = action
            .PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithCancelingThrough("<Keyboard>/escape")
            .WithoutGeneralizingPathOfSelectedControl()
            .OnMatchWaitForAnother(0.2f)
            .OnComplete(operation =>
            {
                action.Enable();
                _rebindOperation?.Dispose();
                _rebindOperation = null;
                IsRebinding = false;
                SwapConflicts(action, bindingIndex, previousPath, controlScheme);
                SaveBindings();
                onFinished?.Invoke();
            })
            .OnCancel(operation =>
            {
                action.Enable();
                _rebindOperation?.Dispose();
                _rebindOperation = null;
                IsRebinding = false;
                onFinished?.Invoke();
            })
            .Start();
    }

    // 새로 할당한 키가 같은 스킴의 다른 바인딩과 겹치면, 그 바인딩에 '이전 키'를 넘겨 서로 맞바꾼다.
    private void SwapConflicts(InputAction changedAction, int changedBindingIndex, string previousPath, string controlScheme)
    {
        string newPath = changedAction.bindings[changedBindingIndex].effectivePath;

        // 같은 키를 다시 눌렀거나 경로가 비어 있으면 교체할 것이 없음
        if (string.IsNullOrEmpty(newPath) || newPath == previousPath) return;

        InputActionMap map = changedAction.actionMap;
        if (map == null) return;

        // 1) 같은 키를 쓰는 다른 바인딩(충돌)을 수집
        List<(InputAction action, int index)> conflicts = new List<(InputAction, int)>();
        foreach (InputAction other in map.actions)
        {
            var bindings = other.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                // 방금 바꾼 바인딩 자신은 제외
                if (other == changedAction && i == changedBindingIndex) continue;

                InputBinding b = bindings[i];
                if (b.isComposite) continue;                    // 합성 헤더는 실제 키가 아님
                if (b.effectivePath != newPath) continue;       // 같은 키가 아니면 통과
                if (!BelongsToScheme(b, controlScheme)) continue;

                // 잠긴 액션의 키는 옮길 수 없으므로 교체 불가 → 이번 변경을 되돌려 충돌을 회피
                if (_lockedActions.Contains(other.name))
                {
                    changedAction.ApplyBindingOverride(changedBindingIndex, previousPath);
                    return;
                }

                conflicts.Add((other, i));
            }
        }

        // 2) 충돌한 바인딩에 이전 키를 넘겨 교체
        foreach ((InputAction act, int idx) in conflicts)
            act.ApplyBindingOverride(idx, previousPath);
    }

    // 바인딩이 지정한 컨트롤 스킴(그룹)에 속하는지 검사
    private bool BelongsToScheme(InputBinding binding, string controlScheme)
    {
        if (string.IsNullOrEmpty(binding.groups)) return true;   // 그룹 미지정이면 공용으로 간주

        foreach (string group in binding.groups.Split(InputBinding.Separator))
            if (group.Trim() == controlScheme) return true;

        return false;
    }

    public void ResetBinding(InputActionReference actionReference, int bindingIndex = 0)
    {
        if (actionReference == null) return;

        InputAction action = actionReference.action;

        if (_lockedActions.Contains(action.name)) return;

        action.RemoveBindingOverride(bindingIndex);
        // 초기화 결과도 영구 저장해야 다음 실행 시 LoadBindings로 되살아나지 않음
        SaveBindings();
    }

    private void SaveBindings()
    {
        string json = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(InputBindingsKey, json);
        PlayerPrefs.Save();
    }

    public void LoadBindings()
    {
        if (PlayerPrefs.HasKey(InputBindingsKey))
            inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(InputBindingsKey));
    }

    private void OnDestroy()
    {
        _rebindOperation?.Dispose();
        IsRebinding = false;
        if (_instance == this) _instance = null;
    }
}