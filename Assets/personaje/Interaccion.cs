using UnityEngine;
using UnityEngine.InputSystem;

public class Interaccion : MonoBehaviour
{
    [Header("Configuración de Interacción")]
    public float interactionDistance = 3f;
    public LayerMask interactableLayer;

    [Header("Input System")]
    public InputActionReference interactAction;

    [Header("Referencias")]
    public Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void OnEnable()
    {
        interactAction.action.performed += HandleInteraction;
    }

    void OnDisable()
    {
        interactAction.action.performed -= HandleInteraction;
    }

    private void HandleInteraction(InputAction.CallbackContext context)
    {
        // Dibuja una línea roja en la pestaña "Scene" de Unity que dura 2 segundos
        Debug.DrawRay(mainCamera.transform.position, mainCamera.transform.forward * interactionDistance, Color.red, 2f);

        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayer))
        {
            InteractableItem item = hit.collider.GetComponent<InteractableItem>();
            if (item != null)
            {
                item.PickUp();
            }
        }
    }

}