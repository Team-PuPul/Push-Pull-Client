using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SoundSlider : UIButton
{
    [SerializeField] private Slider soundSlider;
    [SerializeField] private Color editingColor;


    public override void OnMove(AxisEventData eventData)
    {
        switch(eventData.moveDir)
        {
            case MoveDirection.Left:
                soundSlider.value -= 0.1f;
                break;
            case MoveDirection.Right:
                soundSlider.value += 0.1f;
                break;
        }

        base.OnMove(eventData);
    }
}
