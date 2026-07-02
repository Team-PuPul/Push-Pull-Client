using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 4f;

    [SerializeField]
    private float jumpForce = 15f;

    [SerializeField]
    private float flipThreshold = 0.2f;

    [Header("Moving Surface")]
    [SerializeField]
    private float againstPlatformBoost = 0.5f;

    private InputPlayer player;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool moveLeft;
    private bool moveRight;
    private bool isFlipped;
    private bool jumpAble = true;

    private IMovingSurface currentMovingSurface;
    private Vector3 lastMovingSurfacePosition;

    public Vector2 MoveInput => moveInput;
    public bool IsFlipped => isFlipped;
    public bool JumpAble
    {
        get => jumpAble;
        set => jumpAble = value;
    }

    private void Awake()
    {
        player = GetComponent<InputPlayer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (!player.CanProcessGameplay)
            return;

        if (moveRight ^ moveLeft)
        {
            Vector3 move = new Vector3(moveInput.x * moveSpeed * Time.fixedDeltaTime, 0f, 0f);
            transform.Translate(move);
        }

        ApplyMovingSurfaceCarry();

        if (
            Mathf.Abs(moveInput.x) > flipThreshold
            && player.GrabGlove != null
            && !player.GrabGlove.grabing
        )
        {
            if (moveInput.x > 0f && !isFlipped)
                Flip();
            else if (moveInput.x < 0f && isFlipped)
                Flip();
        }
    }

    public void HandleMoveLeft(InputAction.CallbackContext context)
    {
        if (!player.isLocalPlayer)
            return;

        if (context.started || context.performed)
            moveLeft = true;
        else if (context.canceled)
            moveLeft = false;

        UpdateMoveInput();
    }

    public void HandleMoveRight(InputAction.CallbackContext context)
    {
        if (!player.isLocalPlayer)
            return;

        if (context.started || context.performed)
            moveRight = true;
        else if (context.canceled)
            moveRight = false;

        UpdateMoveInput();
    }

    public void HandleJump(InputAction.CallbackContext context)
    {
        if (!player.isLocalPlayer || !context.performed || !jumpAble)
            return;

        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        player.ForceAirborne();
        player.PlayPlayerSound("PlayerJump_1", global::PlayerSounds.Jump);
    }

    public void Flip()
    {
        isFlipped = !isFlipped;
        ApplyFlip(isFlipped);

        if (player.isLocalPlayer)
            player.SyncFlip(isFlipped);
    }

    public void ApplyFlip(bool flipped)
    {
        isFlipped = flipped;

        Vector3 scale = transform.localScale;
        scale.x = flipped ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    private void UpdateMoveInput()
    {
        if (moveLeft && moveRight)
            moveInput = Vector2.zero;
        else if (moveLeft)
            moveInput = Vector2.left;
        else if (moveRight)
            moveInput = Vector2.right;
        else
            moveInput = Vector2.zero;
    }

    private void ApplyMovingSurfaceCarry()
    {
        if (currentMovingSurface == null)
            return;

        if (currentMovingSurface is Object surfaceObject && surfaceObject == null)
        {
            currentMovingSurface = null;
            return;
        }

        if (!currentMovingSurface.CanCarryPlayer)
        {
            currentMovingSurface = null;
            return;
        }

        Vector3 surfacePosition = currentMovingSurface.CarryPosition;
        Vector3 surfaceDelta = surfacePosition - lastMovingSurfacePosition;
        Vector3 extraMove = Vector3.zero;

        if (Mathf.Abs(moveInput.x) > 0.01f && Mathf.Abs(surfaceDelta.x) > 0.0001f)
        {
            bool movingAgainstPlatform = Mathf.Sign(moveInput.x) != Mathf.Sign(surfaceDelta.x);

            if (movingAgainstPlatform)
                extraMove.x = moveInput.x * Mathf.Abs(surfaceDelta.x) * againstPlatformBoost;
        }

        Vector3 carryDelta = surfaceDelta + extraMove;

        if (carryDelta != Vector3.zero)
            transform.position += carryDelta;

        lastMovingSurfacePosition = surfacePosition;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!player.isLocalPlayer)
            return;

        if (!TryGetMovingSurface(collision, out IMovingSurface movingSurface))
            return;

        if (!IsGroundContact(collision))
        {
            if (ReferenceEquals(currentMovingSurface, movingSurface))
                currentMovingSurface = null;

            return;
        }

        if (ReferenceEquals(currentMovingSurface, movingSurface))
            return;

        currentMovingSurface = movingSurface;
        lastMovingSurfacePosition = movingSurface.CarryPosition;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!player.isLocalPlayer)
            return;

        if (!TryGetMovingSurface(collision, out IMovingSurface movingSurface))
            return;

        if (ReferenceEquals(currentMovingSurface, movingSurface))
            currentMovingSurface = null;
    }

    private bool TryGetMovingSurface(Collision2D collision, out IMovingSurface movingSurface)
    {
        movingSurface = collision.collider.GetComponentInParent<IMovingSurface>();
        return movingSurface != null && movingSurface.CanCarryPlayer;
    }

    private bool IsGroundContact(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            if (contact.normal.y > 0.5f)
                return true;
        }

        return false;
    }
}
