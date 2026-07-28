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

    private Vector3 desPos;


    [ServerCallback]
    private void Start()
    {
        // pos1, pos2는 시작 위치 기준 오프셋이 아니라 부모 기준 로컬 좌표(절대값)다.
        // 기존에는 여기서 startLocalPosition을 더해 첫 목적지를 만들었지만,
        // 아래 Move()의 방향 전환은 pos1/pos2를 그대로 사용하기 때문에
        // 첫 이동만 좌표가 두 배로 튀는 문제가 있었다.
        desPos = pos1;

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
