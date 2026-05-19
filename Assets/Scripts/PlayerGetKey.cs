using Mirror;
using UnityEngine;

public class PlayerGetKey : NetworkBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isLocalPlayer)
            return;

        if (collision.gameObject.TryGetComponent<Key>(out Key key))
        {
            CmdCollectKey(key.netIdentity);
        }
    }

    [Command]
    private void CmdCollectKey(NetworkIdentity keyIdentity)
    {
        if (keyIdentity == null)
            return;

        Key key = keyIdentity.GetComponent<Key>();
        if (key == null)
            return;

        key.CollectOnServer();
    }
}
