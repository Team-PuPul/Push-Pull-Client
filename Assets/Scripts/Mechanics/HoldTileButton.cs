using System.Collections.Generic;
using Mirror;
using UnityEngine;

// Server-authoritative button for HoldTileController.
public class HoldTileButton : NetworkBehaviour
{
    [SerializeField] private HoldTileController controller;

    // Empty mask allows any Rigidbody2D object to press the button.
    [SerializeField] private LayerMask pressableMask;

    private readonly HashSet<Rigidbody2D> pressers = new HashSet<Rigidbody2D>();

    private Animator anim;

    [SyncVar(hook = nameof(OnPressedChanged))]
    private bool isPressed;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isPressed && anim != null)
            anim.Play("Push");
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsPressable(collision))
            return;

        Rigidbody2D rb = collision.attachedRigidbody;

        if (!pressers.Add(rb))
            return;

        if (controller != null)
            controller.AddHolder(rb);

        UpdatePressedState();
    }

    [ServerCallback]
    private void OnTriggerExit2D(Collider2D collision)
    {
        Rigidbody2D rb = collision.attachedRigidbody;

        if (rb == null)
            return;

        if (!pressers.Remove(rb))
            return;

        if (controller != null)
            controller.RemoveHolder(rb);

        UpdatePressedState();
    }

    [Server]
    private void UpdatePressedState()
    {
        bool pressed = pressers.Count > 0;

        if (isPressed == pressed)
            return;

        isPressed = pressed;
    }

    private bool IsPressable(Collider2D collision)
    {
        if (collision.attachedRigidbody == null)
            return false;

        if (pressableMask.value == 0)
            return true;

        return (pressableMask.value & (1 << collision.gameObject.layer)) != 0;
    }

    private void OnPressedChanged(bool oldValue, bool newValue)
    {
        if (anim != null)
            anim.Play(newValue ? "Push" : "Pull");
    }
}
