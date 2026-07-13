using System.Collections;
using Mirror;
using UnityEngine;

// 플레이어가 밟으면 흔들린 뒤 부서지는 바닥.
// 멀티플레이 동기화를 위해 NetworkBehaviour로 전환했다.
// 충돌 판정은 서버에서만 하고, 부서짐 상태를 SyncVar로 모든 클라이언트에 전파한다.
// 각 클라이언트는 로컬에서 흔들림 연출을 재생한 뒤 콜라이더/렌더러를 비활성화한다.
public class BreakTile : NetworkBehaviour
{
    // 흔들림 지속 시간과 세기(매직 넘버 분리).
    [SerializeField] float shakeDuration = 1f;
    [SerializeField] float shakeStrength = 0.1f;

    // 부서짐 상태. 한 번 부서지면 되돌아오지 않는다.
    [SyncVar(hook = nameof(OnBrokenChanged))]
    bool isBroken;

    Collider2D _collider;
    SpriteRenderer _renderer;

    void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _renderer = GetComponent<SpriteRenderer>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 늦게 접속한 게스트는 이미 부서진 바닥을 흔들림 없이 즉시 비활성화 상태로 반영한다.
        if (isBroken)
            ApplyBroken();
    }

    [ServerCallback]
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isBroken)
            return;

        if (collision.transform.GetComponent<InputPlayer>() == null)
            return;

        isBroken = true;
    }

    void OnBrokenChanged(bool oldValue, bool newValue)
    {
        if (newValue)
            StartCoroutine(Shake());
    }

    IEnumerator Shake()
    {
        Vector3 origin = transform.localPosition;
        float time = 0f;

        while (time < shakeDuration)
        {
            time += Time.deltaTime;

            float damper = 1f - (time / shakeDuration);
            Vector3 randomOffset = Random.insideUnitSphere * shakeStrength * damper;
            transform.localPosition = origin + randomOffset;

            yield return null;
        }

        transform.localPosition = origin;
        ApplyBroken();
    }

    // 콜라이더/렌더러를 비활성화해 바닥을 사라지게 한다.
    // 오브젝트 자체를 파괴하지 않아 흔들림 연출을 보존하고, 네트워크 파괴 경고도 피한다.
    void ApplyBroken()
    {
        if (_collider != null)
            _collider.enabled = false;

        if (_renderer != null)
            _renderer.enabled = false;
    }
}
