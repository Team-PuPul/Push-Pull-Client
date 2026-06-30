using TMPro;
using UnityEngine;

public class RoomCodePanel : ButtonPanel
{
    [SerializeField] private TMP_InputField roomCodeField;

    public override void EnablePanel()
    {
        // 인스펙터 할당 누락 등으로 인한 NullReferenceException 방지
        if (roomCodeField != null) roomCodeField.text = "";
        base.EnablePanel();
    }
}
