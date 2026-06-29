using DG.Tweening;
using UnityEngine;

public class PauseCanvas : ButtonCanvas
{
    [SerializeField] private CanvasGroup backgroundCanvasGroup;

    // 일시정지 열림: 어두운 뒷배경을 함께 페이드인
    public override void EnableCanvas()
    {
        base.EnableCanvas();
        BackgroundFade(1f);
    }

    // 일시정지 닫힘(게임 재개): 뒷배경도 함께 페이드아웃
    // 설정 화면으로 이동할 때는 DisableCanvas가 아니라 FadeIn()이 호출되므로
    // 이 경로를 타지 않으며, 뒷배경은 그대로 유지된다.
    public override void DisableCanvas()
    {
        base.DisableCanvas();
        BackgroundFade(0f);
    }

    private void BackgroundFade(float value)
    {
        // 인스펙터 미할당 시 NullReferenceException 방지
        if (backgroundCanvasGroup != null)
        {
            backgroundCanvasGroup.DOFade(value, fadeDuration);
        }
    }
}
