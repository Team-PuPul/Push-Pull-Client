using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;

public class PlayerID : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))]
    [SerializeField]
    private string playerName;

    public string PlayerName => playerName;

    private TMP_Text idText;
    private InputPlayer player;
    private Vector3 initialTextScale;

    private void Awake()
    {
        idText = GetComponent<TMP_Text>();
        player = GetComponentInParent<InputPlayer>();
        initialTextScale = transform.localScale;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        CacheReferences();
        SetNameText(playerName);
        ApplyTextFlip();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        string localName = GetLocalPlayerName();

        SetNameText(localName);
        CmdSetPlayerName(localName);
    }

    [Command]
    private void CmdSetPlayerName(string newName)
    {
        playerName = SanitizeName(newName);
    }

    private void OnNameChanged(string oldName, string newName)
    {
        SetNameText(newName);
    }

    private void LateUpdate()
    {
        CacheReferences();
        ApplyTextFlip();
    }

    private void CacheReferences()
    {
        if (idText == null)
            idText = GetComponent<TMP_Text>();

        if (player == null)
            player = GetComponentInParent<InputPlayer>();
    }

    private string GetLocalPlayerName()
    {
        if (SteamManager.Initialized)
            return SanitizeName(SteamFriends.GetPersonaName());

        return $"Player {netId}";
    }

    private string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return $"Player {netId}";

        return name;
    }

    private void SetNameText(string name)
    {
        if (idText == null)
            return;

        idText.text = SanitizeName(name);
    }

    private void ApplyTextFlip()
    {
        if (player == null)
            return;

        bool playerFacingLeft = player.transform.localScale.x < 0f;

        Vector3 textScale = initialTextScale;
        textScale.x = playerFacingLeft
            ? -Mathf.Abs(initialTextScale.x)
            : Mathf.Abs(initialTextScale.x);

        transform.localScale = textScale;
    }
}
