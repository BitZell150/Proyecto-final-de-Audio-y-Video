using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [Header("Sensibilidades")]
    public float mouseSensitivity = 0.2f; 
    public float gamepadSensitivity = 200f; // Requiere un valor alto porque depende del tiempo

    public Transform playerBody;

    [Header("Controles")]
    public InputActionReference lookAction;

    float xRotation = 0f;

    void OnEnable()
    {
        lookAction.action.Enable();
    }

    void OnDisable()
    {
        lookAction.action.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Vector2 inputLook = lookAction.action.ReadValue<Vector2>();
        
        float lookX = 0f;
        float lookY = 0f;

        // Identifica qué dispositivo de entrada está activo en este fotograma
        var activeControl = lookAction.action.activeControl;
        if (activeControl != null)
        {
            if (activeControl.device is Gamepad)
            {
                // Cálculo para el mando: se multiplica por la sensibilidad del mando y por Time.deltaTime
                lookX = inputLook.x * gamepadSensitivity * Time.deltaTime;
                lookY = inputLook.y * gamepadSensitivity * Time.deltaTime;
            }
            else
            {
                // Cálculo para el ratón/teclado: solo se multiplica por la sensibilidad del ratón
                lookX = inputLook.x * mouseSensitivity;
                lookY = inputLook.y * mouseSensitivity;
            }
        }

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -70f, 70f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * lookX);
    }
}