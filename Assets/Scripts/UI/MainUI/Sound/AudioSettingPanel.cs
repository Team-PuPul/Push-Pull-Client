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
}
