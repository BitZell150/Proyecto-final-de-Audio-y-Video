using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class InteractableItem : MonoBehaviour
{
    [Header("Configuración de Rotación")]
    [Tooltip("Velocidad de rotación en los ejes X, Y, Z")]
    public Vector3 rotationSpeed = new Vector3(0f, 50f, 0f);

    [Header("Secuencia")]
    [Tooltip("Arrastra aquí el siguiente objeto que debe aparecer. Déjalo vacío si es el último.")]
    public GameObject nextItemToSpawn;

    [Header("Cambio de escena")]
    [Tooltip("Nombre exacto de la escena a cargar al recoger el disco")]
    public string escenaDestino = "";

    [Header("Interacción")]
    [Tooltip("Alcance específico de este tipo de objeto. Interaccion conserva la línea de visión y aplica este límite.")]
    public float distanciaInteraccion = 1f;

    [Header("Mensaje al acercarse")]
    [Tooltip("Nombre de la canción que muestra el mensaje \"Presiona [E / X] para jugar: ...\". Vacío = sin mensaje.")]
    public string nombreCancion = "";
    [Tooltip("Distancia (m) a la que aparece el mensaje al acercarse al disco.")]
    public float distanciaMensaje = 1f;

    bool recogido;
    int frameActivado;
    PlayerMovement jugador;
    Collider colision;

    void Awake()
    {
        colision = GetComponent<Collider>();
    }

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

        MostrarMensajeSiEstaCerca();
    }

    /// <summary>
    /// Recibe la interacción ya validada por Interaccion. El disco conserva su propio
    /// alcance y rechaza el mismo frame en que fue activado por otro disco.
    /// </summary>
    public bool TryPickUp(float distanciaDesdeCamara)
    {
        if (recogido || Time.frameCount == frameActivado || distanciaDesdeCamara > distanciaInteraccion)
            return false;

        PickUp();
        return true;
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

        // 3. Cargar otra escena (si se indicó una)
        if (!string.IsNullOrEmpty(escenaDestino))
        {
            if (Application.CanStreamedLevelBeLoaded(escenaDestino))
            {
                SceneManager.LoadScene(escenaDestino);
            }
            else
            {
                Debug.LogError($"[InteractableItem] La escena '{escenaDestino}' no está en Build Settings.");
            }
        }
    }

    void OnValidate()
    {
        distanciaInteraccion = Mathf.Max(0f, distanciaInteraccion);
        distanciaMensaje = Mathf.Max(0f, distanciaMensaje);
    }

    // Pide el mensaje emergente mientras el jugador esté cerca (se muestra en pausa/pantallas finales: no).
    void MostrarMensajeSiEstaCerca()
    {
        if (string.IsNullOrEmpty(nombreCancion) || !JugadorPuedeInteractuar()) return;

        Camera camara = Camera.main;
        if (camara == null) return;

        Vector3 desde = camara.transform.position;
        bool colisionUsable = colision != null && !(colision is MeshCollider malla && !malla.convex);
        float distancia = colisionUsable
            ? Vector3.Distance(desde, colision.ClosestPoint(desde))
            : Vector3.Distance(desde, transform.position);

        if (distancia <= distanciaMensaje) MensajeInteraccion.Solicitar(distancia, nombreCancion);
    }

    // No se puede agarrar con el juego en pausa o con una pantalla final abierta (el jugador está desactivado).
    bool JugadorPuedeInteractuar()
    {
        if (jugador == null) jugador = FindAnyObjectByType<PlayerMovement>();
        return jugador != null && jugador.enabled;
    }

}
