using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 화면 해상도 / 전체화면을 설정하는 패널
// 기존 Setting.cs의 기능을 옮겨오되, 전체화면은 Toggle 대신 UIButton(일반 버튼)으로 토글한다.
public class VideoSettingPanel : ButtonPanel
{
    [SerializeField] private TMP_Text screenText;
    [SerializeField] private TMP_Text fullScreenText;
    [SerializeField] private int resolutionNum;

    private readonly List<Resolution> resolutions = new List<Resolution>();

    private FullScreenMode fullScreenMode;
    private bool isFullScreen;

    public override void EnablePanel()
    {
        base.EnablePanel();

        resolutions.Clear();
        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            Resolution res = Screen.resolutions[i];

            // 주사율 30Hz 이상 + 16:9 비율 + 가로 1280 이상만 허용
            if (res.refreshRateRatio.value >= 30 &&
                res.width * 9 == res.height * 16 &&
                res.width >= 1280)
            {
                resolutions.Add(res);
            }
        }

        // 기본값은 가장 높은 해상도
        resolutionNum = resolutions.Count - 1;

        // 현재 전체화면 상태를 읽어와 동기화
        isFullScreen = Screen.fullScreen;
        fullScreenMode = isFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        // 현재 화면 해상도와 일치하는 항목이 있으면 그 인덱스를 선택
        for (int i = 0; i < resolutions.Count; i++)
        {
            if (resolutions[i].width == Screen.width &&
                resolutions[i].height == Screen.height &&
                Mathf.Approximately((float)resolutions[i].refreshRateRatio.value,
                                    (float)Screen.currentResolution.refreshRateRatio.value))
            {
                resolutionNum = i;
                break;
            }
        }

        UpdateScreenText();
        UpdateFullScreenText();
    }

    // 선택된 해상도를 텍스트에 갱신
    private void UpdateScreenText()
    {
        if (resolutions.Count == 0) return;

        Resolution res = resolutions[resolutionNum];
        screenText.text = $"{res.width} x {res.height} {(int)res.refreshRateRatio.value}hz";
    }

    // 전체화면/창모드 상태를 텍스트에 갱신
    private void UpdateFullScreenText()
    {
        if (fullScreenText == null) return;
        fullScreenText.text = isFullScreen ? "켜짐" : "꺼짐";
    }

    // 해상도 목록에서 이전 항목으로 이동
    public void MoveScreenListLeft()
    {
        resolutionNum = (resolutionNum - 1 + resolutions.Count) % resolutions.Count;
        UpdateScreenText();
    }

    // 해상도 목록에서 다음 항목으로 이동
    public void MoveScreenListRight()
    {
        resolutionNum = (resolutionNum + 1) % resolutions.Count;
        UpdateScreenText();
    }

    // 전체화면 <-> 창모드 토글 (UIButton의 OnClick에 연결)
    public void ToggleFullScreen()
    {
        isFullScreen = !isFullScreen;
        fullScreenMode = isFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        UpdateFullScreenText();
    }

    // 선택한 해상도와 전체화면 모드를 실제 화면에 적용 (적용 버튼에 연결)
    public void SetScreenSize()
    {
        if (resolutions.Count == 0) return;

        Resolution res = resolutions[resolutionNum];
        Screen.SetResolution(res.width, res.height, fullScreenMode);
    }
}
