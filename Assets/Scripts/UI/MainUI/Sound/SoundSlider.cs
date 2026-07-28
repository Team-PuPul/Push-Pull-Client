using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SoundSlider : UIButton
{
    public enum SoundType
    {
        ALL,
        BGM,
        SFX
    }

    [SerializeField] private Slider soundSlider;
    [SerializeField] private Color editingColor;
    [SerializeField] private SoundType soundType;

    private event Action<float> onSoundChanged;

    protected override void Start()
    {
        base.Start();
        onSoundChanged += OnSoundChanged;

        soundSlider.onValueChanged.RemoveAllListeners();
        soundSlider.onValueChanged.AddListener(OnSoundChanged);

        RefreshFromSaved();
    }

    // 저장된 볼륨 값을 슬라이더에 반영한다.
    //
    // SoundManager는 시작할 때 PlayerPrefs 값을 오디오 믹서에 복원하지만
    // 슬라이더는 그 값을 읽지 않아, 실제 볼륨과 UI 표시가 어긋나 있었다.
    // (예: 50%로 저장하고 재접속하면 소리는 50%인데 슬라이더는 100%)
    //
    // SetValueWithoutNotify를 쓰는 이유: value에 직접 대입하면 onValueChanged가 발화해
    // 방금 읽은 값을 그대로 다시 저장하고 PlayerPrefs.Save()까지 호출된다.
    public void RefreshFromSaved()
    {
        if (soundSlider == null || SoundManager.Instance == null)
            return;

        soundSlider.SetValueWithoutNotify(SoundManager.Instance.GetSoundVolume(GetVolumeKey()));
    }

    private void OnSoundChanged(float value)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetSoundVolume(GetVolumeKey(), value);
    }

    // soundType을 SoundManager의 볼륨 키로 변환한다.
    private string GetVolumeKey()
    {
        switch (soundType)
        {
            case SoundType.BGM:
                return SoundManager.BgmVolumeKey;
            case SoundType.SFX:
                return SoundManager.SfxVolumeKey;
            default:
                return SoundManager.MasterVolumeKey;
        }
    }

    public override void OnMove(AxisEventData eventData)
    {
        switch(eventData.moveDir)
        {
            case MoveDirection.Left:
                soundSlider.value -= 0.1f;
                onSoundChanged?.Invoke(soundSlider.value);
                break;
            case MoveDirection.Right:
                soundSlider.value += 0.1f;
                onSoundChanged?.Invoke(soundSlider.value);
                break;
        }

        base.OnMove(eventData);
    }
}
