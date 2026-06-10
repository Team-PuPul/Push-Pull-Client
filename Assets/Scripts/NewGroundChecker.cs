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
    }

    private bool IsGround(Collider2D collision)
    {
        return collision.CompareTag("Ground")
            || collision.CompareTag("interactive")
            || collision.CompareTag("Player");
    }
}
