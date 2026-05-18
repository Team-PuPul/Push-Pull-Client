using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class Door : NetworkBehaviour
{
    [SerializeField]
    private int requiredPlayers = 2;

    [SyncVar]
    public bool isCleared;

    private readonly HashSet<uint> enteredPlayers = new HashSet<uint>();

    private int keyCount;

    [SerializeField]
    private GameObject clearUI;
    private KeyCounter keyCounter;

    [SerializeField]
    private AudioClip clip1;

    [SerializeField]
    private AudioClip clip2;

    private void Awake()
    {
        keyCounter = FindObjectOfType<KeyCounter>();
    }

    private void Start()
    {
        Key[] currentKeys = FindObjectsOfType<Key>();
        keyCount = currentKeys.Length;

        if (keyCounter != null)
        {
            keyCounter.OnKeyCountChanged += TryClearStage;
        }
        else
        {
            Debug.LogError("KeyCounter를 찾을 수 없습니다.");
        }
    }

    private void OnDestroy()
    {
        if (keyCounter != null)
        {
            keyCounter.OnKeyCountChanged -= TryClearStage;
        }
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!TryGetPlayerIdentity(collision, out NetworkIdentity playerIdentity))
            return;

        enteredPlayers.Add(playerIdentity.netId);
        TryClearStage();
    }

    [ServerCallback]
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!TryGetPlayerIdentity(collision, out NetworkIdentity playerIdentity))
            return;

        enteredPlayers.Remove(playerIdentity.netId);
    }

    [Server]
    private void TryClearStage()
    {
        if (isCleared)
            return;

        if (enteredPlayers.Count < requiredPlayers)
            return;

        if (keyCounter == null)
        {
            Debug.LogError("KeyCounter를 찾을 수 없습니다.");
            return;
        }

        if (keyCounter.KeyCount != keyCount)
            return;

        isCleared = true;
        StartCoroutine(ClearStage());
    }

    [Server]
    private bool TryGetPlayerIdentity(Collider2D collision, out NetworkIdentity playerIdentity)
    {
        playerIdentity = collision.GetComponentInParent<NetworkIdentity>();
        return playerIdentity != null && playerIdentity.connectionToClient != null;
    }

    [Server]
    private IEnumerator ClearStage()
    {
        RpcPlayClearStart();
        yield return new WaitForSeconds(1.5f);
        RpcShowClearUI();
    }

    [ClientRpc]
    private void RpcPlayClearStart()
    {
        foreach (InputPlayer inputPlayer in FindObjectsOfType<InputPlayer>())
        {
            inputPlayer.Cleared();
        }

        SoundManager.Instance.SFXPlay("Clear", clip1);
    }

    [ClientRpc]
    private void RpcShowClearUI()
    {
        SoundManager.Instance.SFXPlay("Clear", clip2);
        clearUI.SetActive(true);
    }
}
