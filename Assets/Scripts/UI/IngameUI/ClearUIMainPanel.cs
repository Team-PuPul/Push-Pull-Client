using UnityEngine;
using UnityEngine.UI;

public class ClearUIMainPanel : ButtonPanel
{
    private LevelLoader levelLoader;

    protected override void Start()
    {
        base.Start();
        levelLoader = FindObjectOfType<LevelLoader>();
    }

    public void SaveMainSelection() => SaveSelection();
    public void SelectMainButton() => SelectButton();
    public void GoNextStage()
    {
        levelLoader.LoadNextLevel();
    }
}
