using UnityEngine;

public class ClearUICanvas : ButtonCanvas
{
    protected override void Start()
    {
        canvas.worldCamera = Camera.main;
    }
}
