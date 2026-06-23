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
    }

    private void OnSoundChanged(float value)
    {
        if(SoundManager.Instance != null)
        {
            switch(soundType)
            {
                case SoundType.ALL: 
                    SoundManager.Instance.SetSoundVolume(SoundManager.MasterVolumeKey, value); return;
                case SoundType.BGM:
                    SoundManager.Instance.SetSoundVolume(SoundManager.BgmVolumeKey, value); return;
                case SoundType.SFX:
                    SoundManager.Instance.SetSoundVolume(SoundManager.SfxVolumeKey, value); return;
            }
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
