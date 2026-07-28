using DG.Tweening;
using UnityEngine;

public class PauseCanvas : ButtonCanvas
{
    [SerializeField] private CanvasGroup backgroundCanvasGroup;

    // 일시정지 열림: 어두운 뒷배경을 함께 페이드인
    public override void EnableCanvas()
    {
        // base.EnableCanvas()가 SelectButton()으로 첫 버튼을 선택하는데,
        // 비활성 오브젝트는 선택되지 않으므로 반드시 먼저 활성화한다.
        gameObject.SetActive(true);

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

        // base.DisableCanvas()의 canvas.enabled = false는 렌더링만 끄기 때문에
        // GameObject와 Selectable이 그대로 살아남는다.
        // 그런데 플레이어 이동 키(키보드 A·D, 게임패드 좌스틱)가 UI의 Navigate 액션과 겹쳐서,
        // 게임에 복귀한 뒤 이동할 때마다 보이지 않는 일시정지 버튼 사이를 이동하며
        // UIButton.OnSelect → 호버 사운드가 재생됐다.
        //
        // GameObject를 끄면 Selectable이 네비게이션 목록에서 해제되고,
        // 선택이 남아 있더라도 ExecuteEvents가 비활성 오브젝트에 이벤트를 전달하지 않는다.
        // 뒷배경 페이드는 별도 오브젝트(BGCanvas)에 걸려 있어 영향을 받지 않는다.
        gameObject.SetActive(false);
    }

    private void BackgroundFade(float value)
    {
        // 인스펙터 미할당 시 NullReferenceException 방지
        if (backgroundCanvasGroup != null)
        {
            // timeScale 0에서도 배경 페이드가 진행되도록 SetUpdate(true) 적용
            backgroundCanvasGroup.DOFade(value, fadeDuration).SetUpdate(true);
        }
    }
}
