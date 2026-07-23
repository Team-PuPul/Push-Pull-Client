using UnityEngine;

public class ClearUIMain : ButtonPanel
{
    [SerializeField] private ClearUIMainPanel mainPanel;

    private LevelLoader levelLoader;
    private SteamLobby steamLobby;

    protected override void Start()
    {
        base.Start();
        levelLoader = FindObjectOfType<LevelLoader>();
        steamLobby = FindObjectOfType<SteamLobby>();
    }

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
        steamLobby?.LeaveCurrentRoom();
        levelLoader.LoadScene("MainUI");
    }
}
