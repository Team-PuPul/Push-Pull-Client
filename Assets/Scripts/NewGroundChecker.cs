using System.Collections.Generic;
using UnityEngine;

public class NewGroundCheck : MonoBehaviour
{
    private readonly HashSet<Collider2D> groundContacts = new HashSet<Collider2D>();
    private InputPlayer player;

    private void Awake()
    {
        player = GetComponentInParent<InputPlayer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsGround(collision))
            return;

        groundContacts.Add(collision);
        UpdateGroundedState();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!IsGround(collision))
            return;

        groundContacts.Add(collision);
        UpdateGroundedState();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!IsGround(collision))
            return;

        groundContacts.Remove(collision);
        UpdateGroundedState();
    }

    private void UpdateGroundedState()
    {
        if (player == null)
            return;

        player.SetGrounded(groundContacts.Count > 0);

        // 접지 판정과 같은 소스(발밑 트리거)에서 이동 발판도 함께 갱신한다.
        // 두 판정이 항상 같은 타이밍에 움직이므로 서로 어긋나는 프레임이 없다.
        player.SetMovingSurface(FindMovingSurface());
    }

    // 현재 발밑에 닿아 있는 콜라이더 중에서 이동 발판(IMovingSurface)을 찾는다.
    private IMovingSurface FindMovingSurface()
    {
        foreach (Collider2D contact in groundContacts)
        {
            if (contact == null)
                continue;

            IMovingSurface surface = contact.GetComponentInParent<IMovingSurface>();

            if (surface != null && surface.CanCarryPlayer)
                return surface;
        }

        return null;
    }

    private bool IsGround(Collider2D collision)
    {
        return collision.CompareTag("Ground")
            || collision.CompareTag("interactive")
            || collision.CompareTag("Player");
    }
}
