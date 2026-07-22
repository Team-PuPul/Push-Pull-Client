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

        // 서버 권한 물리가 설정된 경우, 서버가 아닌 클라이언트에서는 직접 물리를 시뮬레이션하지 않고
        // NetworkTransform이 전달하는 위치를 그대로 반영한다.
        // (양쪽이 각자 물리를 돌리면 밀기/당기기 결과가 어긋나기 때문이다.)
        if (!isServer)
            TryEnableServerAuthorityMode();

        if (exploded)
            ApplyExplodedState();
    }

    // NetworkTransform이 ServerToClient(서버 권한)로 설정돼 있을 때만
    // 비서버 클라이언트의 Rigidbody2D를 Kinematic으로 전환한다.
    // NetworkTransform이 없으면 기존 방식(전 클라이언트 물리)과 호환되도록 그대로 둔다.
    private void TryEnableServerAuthorityMode()
    {
        if (boxRigidbody == null)
            return;

        NetworkTransformBase netTransform = GetComponent<NetworkTransformBase>();

        if (netTransform != null && netTransform.syncDirection == SyncDirection.ServerToClient)
            boxRigidbody.bodyType = RigidbodyType2D.Kinematic;
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
                ClientApplyExplosionForce(playerIdentity.netId, direction, explosionForce);

                continue;
            }

            ApplyExplosionForceToObject(targetRb, direction);
        }
    }

    // 플레이어가 아닌 대상(다른 박스 등)에 폭발력을 적용한다.
    // 서버 권한 물리 오브젝트는 서버에서만 힘을 주고 NetworkTransform이 위치를 동기화하며,
    // NetworkTransform이 없는 오브젝트는 전 클라이언트가 동일한 힘을 적용한다.
    // (기존에는 서버에서만 AddForce를 호출해 클라이언트 쪽 박스가 밀리지 않았다.)
    [Server]
    private void ApplyExplosionForceToObject(Rigidbody2D targetRb, Vector2 direction)
    {
        NetworkIdentity targetIdentity = targetRb.GetComponent<NetworkIdentity>();

        if (targetIdentity == null || IsServerAuthoritativePhysics(targetIdentity))
        {
            targetRb.AddForce(direction * explosionForce, ForceMode2D.Impulse);
            return;
        }

        RpcApplyExplosionForce(targetIdentity.netId, direction, explosionForce);
    }

    // 대상이 서버 권한 물리인지 판별한다.
    // NetworkTransform이 ServerToClient(서버 권한)로 설정돼 있으면 서버가 물리를 시뮬레이션한다.
    private static bool IsServerAuthoritativePhysics(NetworkIdentity identity)
    {
        NetworkTransformBase netTransform = identity.GetComponent<NetworkTransformBase>();

        return netTransform != null && netTransform.syncDirection == SyncDirection.ServerToClient;
    }

    [ClientRpc]
    private void RpcApplyExplosionForce(uint targetNetId, Vector2 direction, float force)
    {
        // 이 경로는 서버에서 힘을 주지 않으므로, 호스트를 포함한 모든 클라이언트가 직접 적용한다.
        if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity identity))
            return;

        AddExplosionImpulse(identity, direction, force);
    }

    private static void AddExplosionImpulse(
        NetworkIdentity identity,
        Vector2 direction,
        float force
    )
    {
        Rigidbody2D targetRb = identity.GetComponent<Rigidbody2D>();

        if (targetRb == null)
            return;

        targetRb.AddForce(direction * force, ForceMode2D.Impulse);
    }

    [Server]
    private bool TryGetPlayerIdentity(Rigidbody2D targetRb, out NetworkIdentity playerIdentity)
    {
        playerIdentity = null;

        if (targetRb == null)
            return false;

        playerIdentity = targetRb.GetComponentInParent<NetworkIdentity>();

        if (playerIdentity == null)
            return false;

        return playerIdentity.CompareTag("Player");
    }

    // 플레이어는 NetworkTransform이 ClientToServer(클라이언트 권한)라 소유자만 물리를 시뮬레이션한다.
    // 따라서 전 클라이언트에 브로드캐스트하되, 힘은 자기 로컬 플레이어에게만 적용한다.
    [ClientRpc]
    private void ClientApplyExplosionForce(uint targetNetId, Vector2 direction, float force)
    {
        if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity identity))
            return;

        if (!identity.isLocalPlayer)
            return;

        AddExplosionImpulse(identity, direction, force);
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
