using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class TurnColorObject : NetworkBehaviour
{
    [SerializeField]
    private List<GameObject> whiteTileList = new List<GameObject>();

    [SerializeField]
    private List<GameObject> blackTileList = new List<GameObject>();

    [SerializeField]
    private Animator anim;

    [SerializeField]
    private AudioClip clip;

    private InvertEffect invertEffect;

    [SyncVar(hook = nameof(OnWhiteActiveChanged))]
    private bool isWhiteActive = true;

    private void Awake()
    {
        invertEffect = FindObjectOfType<InvertEffect>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        isWhiteActive = true;
        ApplyColorState(isWhiteActive);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (invertEffect == null)
            invertEffect = FindObjectOfType<InvertEffect>();

        ApplyColorState(isWhiteActive);
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.GetComponentInParent<InputPlayer>())
            return;

        TurnColorOnServer();
    }

    [Server]
    private void TurnColorOnServer()
    {
        isWhiteActive = !isWhiteActive;

        ApplyColorState(isWhiteActive);

        RpcPlayTurnColorEffect();
    }

    private void OnWhiteActiveChanged(bool oldValue, bool newValue)
    {
        ApplyColorState(newValue);
    }

    private void ApplyColorState(bool whiteActive)
    {
        for (int i = 0; i < whiteTileList.Count; i++)
        {
            if (whiteTileList[i] != null)
                whiteTileList[i].SetActive(whiteActive);
        }

        for (int i = 0; i < blackTileList.Count; i++)
        {
            if (blackTileList[i] != null)
                blackTileList[i].SetActive(!whiteActive);
        }
    }

    [ClientRpc]
    private void RpcPlayTurnColorEffect()
    {
        if (invertEffect == null)
            invertEffect = FindObjectOfType<InvertEffect>();

        if (invertEffect != null)
            invertEffect.EffectOn();

        if (SoundManager.Instance != null)
            SoundManager.Instance.SFXPlay("TurnColor", clip);

        if (anim != null)
            anim.SetTrigger("Turn");
    }
}
