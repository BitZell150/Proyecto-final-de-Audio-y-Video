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
        // Se respeta la cámara asignada en el Inspector; Camera.main solo es el respaldo
        // (si hay varias cámaras con la etiqueta MainCamera devuelve una cualquiera).
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void OnEnable()
    {
        if (interactAction == null) return;
        interactAction.action.Enable();
        interactAction.action.performed += HandleInteraction;
    }

    void OnDisable()
    {
        if (interactAction == null) return;
        interactAction.action.performed -= HandleInteraction;
        interactAction.action.Disable();
    }

    private void HandleInteraction(InputAction.CallbackContext context)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Dibuja una línea roja en la pestaña "Scene" de Unity que dura 2 segundos
        Debug.DrawRay(mainCamera.transform.position, mainCamera.transform.forward * interactionDistance, Color.red, 2f);

        Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        // Las escenas actuales tienen Interactable Layer vacío; mantener este fallback
        // conserva el comportamiento previo de los discos mientras se configura una capa dedicada.
        int mask = interactableLayer.value == 0 ? Physics.DefaultRaycastLayers : interactableLayer.value;
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // El propio jugador no bloquea el rayo; cualquier otro collider sí.
            if (hit.collider.GetComponentInParent<PlayerMovement>() != null) continue;

            InteractableItem item = hit.collider.GetComponentInParent<InteractableItem>();
            if (item != null) item.TryPickUp(hit.distance);
            return; // conserva la regla: solo el primer collider visible puede interactuarse.
        }
    }

    void OnValidate()
    {
        interactionDistance = Mathf.Max(0f, interactionDistance);
        if (interactAction == null)
            Debug.LogError("[Interaccion] Falta Interact Action.", this);
        if (interactableLayer.value == 0)
            Debug.LogWarning("[Interaccion] Interactable Layer está vacío; se usarán todas las capas por compatibilidad.", this);
    }

}
