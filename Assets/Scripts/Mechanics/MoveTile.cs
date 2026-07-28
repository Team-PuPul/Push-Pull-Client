using System.Collections;
using Mirror;
using UnityEngine;

public class MoveTile : NetworkBehaviour, IMovingSurface
{
    [SerializeField]
    private Vector3 pos1;

    [SerializeField]
    private Vector3 pos2;

    [SerializeField]
    private float speed;

    [SerializeField]
    private float waitTime;


    public bool CanCarryPlayer => true;
    public Vector3 CarryPosition => transform.position;

    private Vector3 startLocalPosition;
    private Vector3 targetPos1;
    private Vector3 targetPos2;
    private Vector3 desPos;


    [ServerCallback]
    private void Start()
    {
        startLocalPosition = transform.localPosition;

        targetPos1 = startLocalPosition + pos1;
        targetPos2 = startLocalPosition + pos2;

        desPos = targetPos1;

        StartCoroutine(Move());
    }

    [Server]
    private IEnumerator Move()
    {
        while (true)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                desPos,
                speed * Time.deltaTime
            );

            if (Vector3.Distance(transform.localPosition, desPos) < 0.01f * (speed + 1))
            {
                yield return new WaitForSeconds(waitTime);
                desPos = desPos == pos1 ? pos2 : pos1;
            }

            yield return null;
        }
    }
}
