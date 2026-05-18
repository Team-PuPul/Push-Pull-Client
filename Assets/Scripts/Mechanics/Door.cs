using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [SerializeField]
    private KeyCounter keyCounter;

    [SerializeField]
    private AudioClip clip1;

    [SerializeField]
    private AudioClip clip2;

    private string NetworkLogContext =>
        $"[Door:{gameObject.name}] scene={SceneManager.GetActiveScene().name}, "
        + $"isServer={isServer}, isClient={isClient}, netId={netId}";

    private void Awake()
    {
        if (keyCounter == null)
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
            Debug.LogError(
                $"{NetworkLogContext} KeyCounter를 찾을 수 없어 키 변경 이벤트를 구독할 수 없습니다."
            );
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
            Debug.LogError(
                $"{NetworkLogContext} KeyCounter를 찾을 수 없어 클리어 판정을 진행할 수 없습니다."
            );
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

        if (playerIdentity == null)
        {
            Debug.LogWarning(
                $"{NetworkLogContext} 문 트리거에 NetworkIdentity 없는 오브젝트가 들어왔습니다. "
                    + $"object={collision.gameObject.name}"
            );
            return false;
        }

        if (playerIdentity.connectionToClient == null)
        {
            Debug.LogWarning(
                $"{NetworkLogContext} 문 트리거에 플레이어가 아닌 네트워크 오브젝트가 들어왔습니다. "
                    + $"object={collision.gameObject.name}, targetNetId={playerIdentity.netId}"
            );
            return false;
        }

        return true;
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
