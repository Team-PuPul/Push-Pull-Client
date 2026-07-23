using UnityEngine;
using UnityEngine.UI;

public class ClearUIMainPanel : ButtonPanel
{
    public void SaveMainSelection() => SaveSelection();
    public void SelectMainButton() => SelectButton();
    public void GoNextStage()
    {
        Debug.Log("다음 스테이지로");
    }

}
