using UnityEngine;

public class PausePanel : ButtonPanel
{
    [SerializeField] private PauseMainPanel pauseMainPanel;

    public override void EnablePanel()
    {
        if (pauseMainPanel != null)
            pauseMainPanel.SaveMainSelection();

        base.EnablePanel();
    }

    public override void DisablePanel()
    {
        if (pauseMainPanel != null)
            pauseMainPanel.SelectMainButton();

        base.DisablePanel();
    }
}
