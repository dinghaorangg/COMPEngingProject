using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{    
    private CharacterController controller;
    private float verticalVelocity;
    public Vector3 velocity {get; private set;}
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



    // Start is called before the first frame update
    void Start()
    {
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        IsGrounded = controller.isGrounded;

        float g = gravity;

        // 根据上升/下降调整重力
        if (verticalVelocity < 0f) 
        {
            g *= fallMultiplier;
        }
        else
        {
            g *= upMultiplier;
        }
        
        // WASD 输入
        float x = Input.GetAxisRaw("Horizontal"); 
        float z = Input.GetAxisRaw("Vertical");   

        NormalMove(x, z, g);



        // 视角旋转
        float mouseX = Input.GetAxis("Mouse X");

        transform.Rotate(Vector3.up, mouseX * sensitivity * Time.deltaTime * 20);
        
    }

    void NormalMove(float x, float z, float g)
    {
         

        // 计算移动方向
        Vector3 inputDir = (transform.right * x + transform.forward * z).normalized;

        if (IsGrounded)
        {
            moveDir = inputDir;
            moveSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;   
        }

        // 简单重力
        if (IsGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += g * Time.deltaTime;

        Vector3 newVelocity = moveDir * moveSpeed;
        newVelocity.y = verticalVelocity;
        
        controller.Move(newVelocity * Time.deltaTime);

        if (IsGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * g);
        }
    }


}
