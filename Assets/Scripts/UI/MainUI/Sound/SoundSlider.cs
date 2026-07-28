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

    // 방향키·게임패드로 조절할 때 한 번에 움직이는 볼륨 폭.
    private const float VolumeStep = 0.1f;

    [SerializeField] private Slider soundSlider;
    [SerializeField] private Color editingColor;
    [SerializeField] private SoundType soundType;

    protected override void Start()
    {
        base.Start();

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
    // 방금 읽은 값을 그대로 다시 적용하는 불필요한 왕복이 생긴다.
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
        // soundSlider.value에 대입하면 onValueChanged가 발화해 OnSoundChanged가 실행된다.
        // 이전에는 여기서 한 번 더 직접 호출해 볼륨 적용과 저장이 두 번씩 일어났다.
        switch (eventData.moveDir)
        {
            case MoveDirection.Left:
                soundSlider.value -= VolumeStep;
                break;
            case MoveDirection.Right:
                soundSlider.value += VolumeStep;
                break;
        }

        base.OnMove(eventData);
    }
}
