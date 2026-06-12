using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class BoomBox : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private SpriteRenderer boxRenderer;

    [SerializeField]
    private Collider2D boxCollider;

    [SerializeField]
    private Rigidbody2D boxRigidbody;

    [SerializeField]
    private ParticleSystem boomEffect;

    [Header("Explosion")]
    [SerializeField]
    private float explosionRadius = 3f;

    [SerializeField]
    private float explosionForce = 10f;

    [SerializeField]
    private LayerMask explosionTargetLayer;

    [SyncVar(hook = nameof(OnExplodedChanged))]
    private bool exploded;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (exploded)
            ApplyExplodedState();
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        Explode();
    }

    [ServerCallback]
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player"))
            return;

        Explode();
    }

    [Server]
    private void Explode()
    {
        if (exploded)
            return;

        ApplyExplosionForce();

        exploded = true;
        RpcPlayExplosionEffect();
    }

    private void OnExplodedChanged(bool oldValue, bool newValue)
    {
        if (!newValue)
            return;

        ApplyExplodedState();
    }

    private void ApplyExplodedState()
    {
        if (boxRenderer != null)
            boxRenderer.enabled = false;

        if (boxCollider != null)
            boxCollider.enabled = false;

        StopBoomBoxPhysics();
    }

    [Server]
    private void ApplyExplosionForce()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            explosionRadius,
            explosionTargetLayer
        );

        HashSet<Rigidbody2D> affectedRigidbodies = new HashSet<Rigidbody2D>();

        foreach (Collider2D hit in hits)
        {
            Rigidbody2D targetRb = hit.attachedRigidbody;

            if (targetRb == null)
                continue;

            if (targetRb == boxRigidbody)
                continue;

            if (!affectedRigidbodies.Add(targetRb))
                continue;

            Vector2 direction = targetRb.position - (Vector2)transform.position;

            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.up;
            else
                direction.Normalize();

            if (TryGetPlayerIdentity(targetRb, out NetworkIdentity playerIdentity))
            {
                TargetApplyExplosionForce(
                    playerIdentity.connectionToClient,
                    playerIdentity.netId,
                    direction,
                    explosionForce
                );
            }
            else
            {
                targetRb.AddForce(direction * explosionForce, ForceMode2D.Impulse);
            }
        }
    }

    [Server]
    private bool TryGetPlayerIdentity(Rigidbody2D targetRb, out NetworkIdentity playerIdentity)
    {
        playerIdentity = null;

        if (targetRb == null)
            return false;

        if (!targetRb.CompareTag("Player"))
            return false;

        playerIdentity = targetRb.GetComponent<NetworkIdentity>();

        if (playerIdentity == null)
            playerIdentity = targetRb.GetComponentInParent<NetworkIdentity>();

        if (playerIdentity == null)
            return false;

        if (playerIdentity.connectionToClient == null)
        {
            Debug.LogWarning(
                $"[BoomBox] Player connection is null. "
                    + $"player={playerIdentity.name}, netId={playerIdentity.netId}, "
                    + $"isServer={NetworkServer.active}"
            );

            return false;
        }

        return true;
    }

    [TargetRpc]
    private void TargetApplyExplosionForce(
        NetworkConnectionToClient target,
        uint targetNetId,
        Vector2 direction,
        float force
    )
    {
        if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity identity))
            return;

        if (!identity.isLocalPlayer)
            return;

        Rigidbody2D targetRb = identity.GetComponent<Rigidbody2D>();

        if (targetRb == null)
            return;

        targetRb.AddForce(direction * force, ForceMode2D.Impulse);
    }

    private void StopBoomBoxPhysics()
    {
        if (boxRigidbody == null)
            return;

        boxRigidbody.velocity = Vector2.zero;
        boxRigidbody.angularVelocity = 0f;
        boxRigidbody.gravityScale = 0f;
        boxRigidbody.constraints |= RigidbodyConstraints2D.FreezePosition;
    }

    [ClientRpc]
    private void RpcPlayExplosionEffect()
    {
        ApplyExplodedState();

        if (boomEffect == null)
            return;

        boomEffect.gameObject.SetActive(true);
        boomEffect.Clear(true);
        boomEffect.Play(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
