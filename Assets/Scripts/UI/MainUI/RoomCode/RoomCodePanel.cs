using TMPro;
using UnityEngine;

public class RoomCodePanel : ButtonPanel
{
    [SerializeField] private TMP_InputField roomCodeField;

    public override void EnablePanel()
    {
        roomCodeField.text = "";
        base.EnablePanel();
    }
}
