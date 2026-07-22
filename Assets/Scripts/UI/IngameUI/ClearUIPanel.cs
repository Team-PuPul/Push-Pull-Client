using UnityEngine;

public class ClearUIMain : ButtonPanel
{
    [SerializeField] private ClearUIMainPanel mainPanel;
    public override void EnablePanel()
    {
        if(mainPanel != null) 
            mainPanel.SaveMainSelection();

        base.EnablePanel();
    }

    public override void DisablePanel()
    {
        if(mainPanel != null)
            mainPanel.SelectMainButton();

        base.DisablePanel();
    }

    public void ReturnMain()
    {
        Debug.Log("메인 화면으로 돌아가기");
    }
}
