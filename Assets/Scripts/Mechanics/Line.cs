using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(75)]
public class Line : MonoBehaviour
{
    LineRenderer line;
    PlayerVisualInterpolator visualInterpolator;
    public Transform TargetGO;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        visualInterpolator = GetComponentInParent<PlayerVisualInterpolator>();
        //TargetGO = transform.Find("LinePos");
    }

    private void Start()
    {
        line.positionCount = 2;
    }

    private void LateUpdate()
    {
        Vector3 visualOffset =
            visualInterpolator != null ? visualInterpolator.VisualOffset : Vector3.zero;

        line.SetPosition(0, TargetGO.position + visualOffset);
        line.SetPosition(1, transform.position + visualOffset);
    }
}
