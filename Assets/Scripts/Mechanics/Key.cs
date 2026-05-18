using Mirror;
using UnityEngine;

public class Key : NetworkBehaviour
{
    private KeyCounter counter;
    private bool collected;

    [SerializeField]
    AudioClip clip;
    private AudioClip clip;

    private void Start()
    {
        counter = FindObjectOfType<KeyCounter>();
    }

    [Server]
    public void CollectOnServer()
    {
        if (collected)
            return;

        collected = true;
        RpcApplyCollect();

        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcApplyCollect()
    {
        SoundManager.Instance.SFXPlay("GetKey", clip);

        if (counter == null)
            counter = FindObjectOfType<KeyCounter>();

        counter.AddCount();
    }
}
