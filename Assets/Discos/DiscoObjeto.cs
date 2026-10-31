using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class InteractableItem : MonoBehaviour
{
    [Header("Configuración de Rotación")]
    [Tooltip("Velocidad de rotación en los ejes X, Y, Z")]
    public Vector3 rotationSpeed = new Vector3(0f, 50f, 0f);

    [Header("Secuencia")]
    [Tooltip("Arrastra aquí el siguiente objeto que debe aparecer. Déjalo vacío si es el último.")]
    public GameObject nextItemToSpawn;

    [Header("Interacción")]
    [Tooltip("Distancia máxima (m) desde la cámara para poder agarrar el disco con E / X (mando).")]
    public float distanciaInteraccion = 1f;

    bool recogido;
    int frameActivado;
    PlayerMovement jugador;

    void OnEnable()
    {
        recogido = false;
        // Un disco recién activado no debe reaccionar a la pulsación que activó al anterior.
        frameActivado = Time.frameCount;
    }

    void Update()
    {
        // Rotación constante sobre su propio eje en tiempo local
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);

        // El disco detecta por sí mismo la tecla E o el botón X del mando (Xbox) y comprueba que el
        // jugador lo esté mirando. Así no depende del LayerMask del componente Interaccion.
        if (Time.frameCount != frameActivado && PulsoInteractuar() && JugadorPuedeInteractuar() && JugadorMiraEsteDisco())
        {
            PickUp();
        }
    }

    public void PickUp()
    {
        if (recogido) return;
        recogido = true;

        // 1. Activar el siguiente objeto de la secuencia (si existe)
        if (nextItemToSpawn != null)
        {
            nextItemToSpawn.SetActive(true);
        }

        // 2. Desactivar el objeto actual
        gameObject.SetActive(false);
    }

    static bool PulsoInteractuar()
    {
        Keyboard teclado = Keyboard.current;
        Gamepad mando = Gamepad.current;

        // En un mando de Xbox el botón X es buttonWest.
        return (teclado != null && teclado.eKey.wasPressedThisFrame)
            || (mando != null && mando.buttonWest.wasPressedThisFrame);
    }

    // No se puede agarrar con el juego en pausa o con una pantalla final abierta (el jugador está desactivado).
    bool JugadorPuedeInteractuar()
    {
        if (jugador == null) jugador = FindFirstObjectByType<PlayerMovement>();
        return jugador != null && jugador.enabled;
    }

    bool JugadorMiraEsteDisco()
    {
        Camera camara = Camera.main;
        if (camara == null) return false;

        Ray rayo = new Ray(camara.transform.position, camara.transform.forward);
        RaycastHit[] impactos = Physics.RaycastAll(rayo, distanciaInteraccion, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(impactos, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit impacto in impactos)
        {
            // El propio jugador no cuenta; lo primero que se vea por delante es el objetivo.
            if (impacto.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            return impacto.collider.GetComponentInParent<InteractableItem>() == this;
        }

        return false;
    }
}
