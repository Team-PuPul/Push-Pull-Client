using UnityEngine;

public interface IMovingSurface
{
    bool CanCarryPlayer { get; }
    Vector3 CarryPosition { get; }
}
