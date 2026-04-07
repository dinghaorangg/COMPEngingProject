using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    private CharacterController controller;
    private float verticalVelocity;
    public Vector3 velocity { get; private set; }
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float climbSpeed = 4f;
    public float fastClimbSpeed = 7f;
    public float gravity = -9.81f;
    public float fallMultiplier = 3f;
    public float upMultiplier = 2f;
    public float sensitivity = 800f;
    public float jumpHeight = 2f;
    private Vector3 moveDir;
    public bool IsGrounded { get; private set; }
    private float moveSpeed;

    public UdpGameClient netClient;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (netClient != null)
        {
            if (netClient.TryConsumeCorrection(out Vector3 correctedPosition))
            {
                ApplyServerCorrection(correctedPosition);
            }

            if (!netClient.IsConnected)
            {
                return;
            }
        }

        IsGrounded = controller.isGrounded;

        float g = gravity;

        if (verticalVelocity < 0f)
        {
            g *= fallMultiplier;
        }
        else
        {
            g *= upMultiplier;
        }

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        NormalMove(x, z, g);

        if (netClient != null)
        {
            netClient.TickPositionSync(transform.position);
        }

        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up, mouseX * sensitivity * Time.deltaTime * 20);
    }

    void NormalMove(float x, float z, float g)
    {
        Vector3 inputDir = (transform.right * x + transform.forward * z).normalized;

        if (IsGrounded)
        {
            moveDir = inputDir;
            moveSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        }

        if (IsGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += g * Time.deltaTime;

        Vector3 newVelocity = moveDir * moveSpeed;
        newVelocity.y = verticalVelocity;
        velocity = newVelocity;

        controller.Move(newVelocity * Time.deltaTime);

        if (IsGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * g);
        }
    }

    private void ApplyServerCorrection(Vector3 correctedPosition)
    {
        controller.enabled = false;
        transform.position = correctedPosition;
        controller.enabled = true;

        verticalVelocity = 0f;
        moveDir = Vector3.zero;
        velocity = Vector3.zero;

        Debug.Log($"Player snapped back to server position: {correctedPosition}");
    }
}
