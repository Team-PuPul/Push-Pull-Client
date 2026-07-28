using Mirror;
using UnityEngine;

public class DisappearButton : NetworkBehaviour
{
    private const string PressedAnimationState = "Push";

    [SerializeField]
    private GameObject disappearObj;

    [SerializeField]
    private LayerMask pressableMask;

    private Animator anim;
    private NetworkAnimator networkAnimator;
    private Renderer[] wallRenderers;
    private Collider2D[] wallColliders;
    private bool[] initialRendererEnabledStates;
    private bool[] initialColliderEnabledStates;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        CacheWallComponents();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        ApplyWallActive(isWallActive);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        ApplyWallActive(isWallActive);

        if (isPressed)
            PlayPressedAnimationLocal();
    }

    [SyncVar(hook = nameof(OnPressedChanged))]
    private bool isPressed;

    [SyncVar(hook = nameof(OnWallActiveChanged))]
    private bool isWallActive = true;

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isPressed)
            return;

        if (!IsPressable(collision))
            return;

        isPressed = true;
        isWallActive = false;

        ApplyWallActive(isWallActive);
        PlayPressedAnimation();
    }

    private bool IsPressable(Collider2D collision)
    {
        if (collision.attachedRigidbody == null)
            return false;

        if (pressableMask.value == 0)
            return true;

        return (pressableMask.value & (1 << collision.gameObject.layer)) != 0;
    }

    private void OnPressedChanged(bool oldValue, bool newValue)
    {
        if (!newValue)
            return;

        if (isServer)
            return;

        if (networkAnimator == null)
            PlayPressedAnimationLocal();
    }

    private void OnWallActiveChanged(bool oldValue, bool newValue)
    {
        ApplyWallActive(newValue);
    }

    private void ApplyWallActive(bool active)
    {
        CacheWallComponents();

        if (disappearObj == null)
            return;

        if (!disappearObj.activeSelf)
            disappearObj.SetActive(true);

        for (int i = 0; i < wallRenderers.Length; i++)
        {
            if (wallRenderers[i] == null)
                continue;

            wallRenderers[i].enabled = active && initialRendererEnabledStates[i];
        }

        for (int i = 0; i < wallColliders.Length; i++)
        {
            if (wallColliders[i] == null)
                continue;

            wallColliders[i].enabled = active && initialColliderEnabledStates[i];
        }
    }

    private void CacheWallComponents()
    {
        if (disappearObj == null || wallRenderers != null || wallColliders != null)
            return;

        wallRenderers = disappearObj.GetComponentsInChildren<Renderer>(true);
        wallColliders = disappearObj.GetComponentsInChildren<Collider2D>(true);

        initialRendererEnabledStates = new bool[wallRenderers.Length];
        initialColliderEnabledStates = new bool[wallColliders.Length];

        for (int i = 0; i < wallRenderers.Length; i++)
            initialRendererEnabledStates[i] = wallRenderers[i] != null && wallRenderers[i].enabled;

        for (int i = 0; i < wallColliders.Length; i++)
            initialColliderEnabledStates[i] = wallColliders[i] != null && wallColliders[i].enabled;
    }

    private void PlayPressedAnimation()
    {
        PlayPressedAnimationLocal();
    }

    private void PlayPressedAnimationLocal()
    {
        if (anim != null)
            anim.Play(PressedAnimationState, 0, 0f);
    }
}
