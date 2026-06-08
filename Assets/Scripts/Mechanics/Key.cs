using Mirror;
using UnityEngine;

public class Key : NetworkBehaviour
{
    private KeyCounter counter;
    private bool collected;

    [SerializeField]
    private AudioClip clip;

    public override void OnStartServer()
    {
        base.OnStartServer();

        counter = FindObjectOfType<KeyCounter>();
    }

    [Server]
    public void CollectOnServer()
    {
        if (collected)
            return;

        if (counter == null)
            counter = FindObjectOfType<KeyCounter>();

        if (counter == null)
        {
            Debug.LogError(
                $"[Key:{gameObject.name}] KeyCounter를 찾을 수 없어 키 획득을 처리할 수 없습니다. "
                    + $"isServer={isServer}, isClient={isClient}, netId={netId}"
            );
            return;
        }

        collected = true;

        counter.AddCount();

        RpcPlayCollectSound();

        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcPlayCollectSound()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SFXPlay("GetKey", clip);
    }
}
