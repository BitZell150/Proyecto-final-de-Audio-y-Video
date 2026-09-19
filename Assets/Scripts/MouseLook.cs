using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    // La sensibilidad ahora debe ser un valor bajo, entre 0.1 y 2
    public float sensitivity = 0.5f; 
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
        
        // Se ha eliminado Time.deltaTime para evitar la doble reducción
        float lookX = inputLook.x * sensitivity;
        float lookY = inputLook.y * sensitivity;

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * lookX);
    }
}