using System.Collections.Generic;
using Mirror;
using UnityEngine;

// Server-authoritative hold tile. The server moves this platform object and
// the parent NetworkTransformReliable replicates the resulting local position.
public class HoldTileController : NetworkBehaviour, IMovingSurface
{
    [SerializeField] private float speed;

    // Target local position while at least one valid object is pressing a button.
    [SerializeField] private Vector3 pushPos;

    private readonly HashSet<Rigidbody2D> holders = new HashSet<Rigidbody2D>();

    private Vector3 restPos;
    private bool isPushed;

    public bool CanCarryPlayer => true;
    public Vector3 CarryPosition => transform.position;

    private void Awake()
    {
        restPos = transform.localPosition;
    }

    [ServerCallback]
    private void Update()
    {
        Vector3 target = isPushed ? pushPos : restPos;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            target,
            speed * Time.deltaTime
        );
    }

    [Server]
    public void AddHolder(Rigidbody2D rb)
    {
        if (rb == null)
            return;

        holders.Add(rb);
        isPushed = true;
    }

    [Server]
    public void RemoveHolder(Rigidbody2D rb)
    {
        if (rb == null)
            return;

        holders.Remove(rb);

        if (holders.Count == 0)
            isPushed = false;
    }
}
