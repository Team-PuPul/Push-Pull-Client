using UnityEngine;

public class ClearUICanvas : ButtonCanvas
{
    protected override void Start()
    {
        base.Start();
        canvas.worldCamera = Camera.main;
    }
}
