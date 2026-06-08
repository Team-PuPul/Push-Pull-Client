using System;
using Mirror;
using UnityEngine;

public class KeyCounter : NetworkBehaviour
{
    public event Action OnKeyCountChanged;

    [SyncVar(hook = nameof(OnKeyCountSynced))]
    private int keyCount;

    [SyncVar(hook = nameof(OnMaxCountSynced))]
    private int maxCount;

    [SerializeField]
    private KeyCountUI countUI;

    public int KeyCount => keyCount;
    public int MaxCount => maxCount;

    // 기존 코드 호환용. Door/UI에서 _maxCount를 쓰고 있었다면 당장 안 터지게 둠.
    public int _maxCount => maxCount;

    public override void OnStartServer()
    {
        base.OnStartServer();

        Key[] currentKeys = FindObjectsOfType<Key>();

        keyCount = 0;
        maxCount = currentKeys.Length;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        CacheUI();
        UpdateUI();
    }

    [Server]
    public void AddCount(int amount = 1)
    {
        int nextCount = Mathf.Clamp(keyCount + amount, 0, maxCount);

        if (keyCount == nextCount)
            return;

        keyCount = nextCount;

        OnKeyCountChanged?.Invoke();
    }

    private void OnKeyCountSynced(int oldValue, int newValue)
    {
        UpdateUI();
    }

    private void OnMaxCountSynced(int oldValue, int newValue)
    {
        UpdateUI();
    }

    private void CacheUI()
    {
        if (countUI == null)
            countUI = FindObjectOfType<KeyCountUI>();
    }

    private void UpdateUI()
    {
        CacheUI();

        if (countUI != null)
            countUI.SetCountText(keyCount, maxCount);
    }
}
