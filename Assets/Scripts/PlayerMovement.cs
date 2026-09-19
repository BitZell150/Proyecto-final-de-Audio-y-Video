using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public float speed = 12f;
    public float gravity = -9.81f;
    public float jumpHeight = 3f;

    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Controles")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;

    Vector3 velocity;
    bool isGrounded;

    void OnEnable()
    {
        // Activa las acciones cuando el script está habilitado
        moveAction.action.Enable();
        jumpAction.action.Enable();
    }

    void OnDisable()
    {
        // Desactiva las acciones para liberar memoria cuando el script se apaga
        moveAction.action.Disable();
        jumpAction.action.Disable();
    }

    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Leer movimiento como un Vector2 (X, Y)
        Vector2 inputMove = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * inputMove.x + transform.forward * inputMove.y;
        
        controller.Move(move * speed * Time.deltaTime);

        // Leer salto como un botón pulsado en este frame
        if (jumpAction.action.WasPressedThisFrame() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}