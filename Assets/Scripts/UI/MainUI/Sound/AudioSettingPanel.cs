using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioSettingPanel : ButtonPanel
{
    // 패널을 열 때마다 저장된 볼륨 값을 슬라이더에 다시 반영한다.
    //
    // ButtonPanel.DisablePanel은 alpha만 0으로 만들고 GameObject는 살려두기 때문에
    // 패널을 다시 열어도 OnEnable이 호출되지 않는다.
    // 따라서 열림 시점에 직접 갱신해야 값이 항상 최신 상태로 보인다.
    public override void EnablePanel()
    {
        base.EnablePanel();

        foreach (SoundSlider slider in GetComponentsInChildren<SoundSlider>(true))
        {
            slider.RefreshFromSaved();
        }
    }

    // 사운드 설정을 떠날 때 변경된 볼륨 값을 디스크에 기록한다.
    //
    // 값이 바뀔 때마다 저장하면 슬라이더 드래그 중 매 프레임 디스크 쓰기가 발생하므로,
    // 저장은 이 시점으로 미룬다. 오디오 패널의 나가기 버튼이 ChangePanel 타입이라
    // 패널을 벗어나는 경로는 항상 DisablePanel을 거친다.
    public override void DisablePanel()
    {
        base.DisablePanel();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SaveSoundVolumes();
        }
    }
}
