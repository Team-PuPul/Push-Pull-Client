using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class Door : NetworkBehaviour
{
    [SerializeField] private int requiredPlayers = 2;

    [SyncVar]
    public bool isCleared;

    private readonly HashSet<uint> enteredPlayers = new HashSet<uint>();

    private int keyCount;
    [SerializeField] GameObject clearUI;
    private KeyCounter keyCounter;

    [SerializeField]
    AudioClip clip1;
    [SerializeField]
    AudioClip clip2;

    private void Start()
    {
        keyCounter = FindObjectOfType<KeyCounter>();
        Key[] currentKeys = FindObjectsOfType<Key>();
        keyCount = currentKeys.Length;

        CurrentKeyCount = keyCounter.KeyCount;
    }

    private void Update()
    {
        CurrentKeyCount = keyCounter.KeyCount;
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
            keyCounter = FindObjectOfType<KeyCounter>();

        if (keyCounter == null || keyCounter.KeyCount != keyCount)
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
        SoundManager.Instance.SFXPlay("Clear", clip1);
    }

    [ClientRpc]
    private void RpcShowClearUI()
    {
        SoundManager.Instance.SFXPlay("Clear", clip2);
        clearUI.SetActive(true);
    }
}
