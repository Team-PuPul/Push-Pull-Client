using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerPushController : MonoBehaviour
{
    [SerializeField]
    private float maxPushCharge = 35f;

    [SerializeField]
    private float chargeTime = 1f;

    private InputPlayer player;
    private bool isCharging;

    public float PushCharge { get; set; }
    public bool IsPushing { get; set; }
    public bool PushHeld { get; private set; }

    private void Awake()
    {
        player = GetComponent<InputPlayer>();
    }

    private void Update()
    {
        if (!player.CanProcessGameplay)
            return;

        if (PushHeld)
            player.ChargeUI?.OnPush();
        else
            player.ChargeUI?.OffPush();

        if (isCharging)
        {
            float chargeRate = chargeTime > 0f ? maxPushCharge / chargeTime : maxPushCharge;
            PushCharge = Mathf.Min(PushCharge + chargeRate * Time.deltaTime, maxPushCharge);
        }

        if (player.PushGlove != null)
            player.PushGlove.PushPower = PushCharge;
    }

    public void HandleInput(InputAction.CallbackContext context)
    {
        if (!player.isLocalPlayer)
            return;

        if (!player.CanProcessGameplay)
        {
            ResetStageSpawnState();
            return;
        }

        if (context.started)
        {
            PushHeld = true;
            isCharging = true;
            PushCharge = 0f;
        }
        else if (context.canceled)
        {
            PushHeld = false;

            if (!isCharging)
                return;

            isCharging = false;
            IsPushing = true;
            player.PlayPlayerSound("PlayerPush_1", global::PlayerSounds.Push);
            player.PushGlove?.DoPunchAnim();
            player.SyncPunchAnim();
        }
    }

    public bool ConsumePush(out float charge)
    {
        if (IsPushing)
        {
            charge = PushCharge;
            IsPushing = false;
            PushCharge = 0f;
            return true;
        }

        charge = 0f;
        return false;
    }

    public void ResetStageSpawnState()
    {
        isCharging = false;
        PushHeld = false;
        IsPushing = false;
        PushCharge = 0f;
        player.ChargeUI?.OffPush();

        if (player.PushGlove != null)
            player.PushGlove.PushPower = 0f;
    }
}
