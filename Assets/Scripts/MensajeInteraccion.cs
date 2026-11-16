using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Mensaje emergente "Presiona [botón] para jugar: [canción]" que aparece al acercarse a un disco.
/// El botón mostrado depende del dispositivo que se esté usando: "X" con mando, "E" con teclado y ratón.
/// Los discos (InteractableItem) lo piden con Solicitar(); si no hay uno en la escena se crea solo.
/// </summary>
public class MensajeInteraccion : MonoBehaviour
{
    public static MensajeInteraccion Instance { get; private set; }

    [Header("Estilo (mismo que el menú)")]
    [SerializeField] TMP_FontAsset fuente;
    [SerializeField] Sprite spriteFondo;

    [Header("Botones que se muestran")]
    [SerializeField] string botonTeclado = "E";
    [SerializeField] string botonMando = "X";

    [SerializeField] float duracionFade = 0.15f;

    CanvasGroup grupo;
    RectTransform panel;
    TextMeshProUGUI texto;
    string ultimoTexto;

    // Petición del frame: el disco más cercano gana.
    bool hayPeticion;
    float mejorDistancia;
    string mejorCancion;

    float ultimoUsoMando = -100f, ultimoUsoOtro = -100f;
    bool UsandoMando => ultimoUsoMando > ultimoUsoOtro;

    /// <summary>Un disco pide mostrar el mensaje este frame (hay que llamarlo cada frame mientras esté cerca).</summary>
    public static void Solicitar(float distancia, string cancion)
    {
        if (Instance == null) new GameObject("MensajeInteraccion").AddComponent<MensajeInteraccion>();
        Instance.Registrar(distancia, cancion);
    }

    void Registrar(float distancia, string cancion)
    {
        if (hayPeticion && distancia >= mejorDistancia) return;
        hayPeticion = true;
        mejorDistancia = distancia;
        mejorCancion = cancion;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ValidateStyle();
        ConstruirUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        RegistrarDispositivo();
    }

    void LateUpdate()
    {
        if (hayPeticion)
        {
            string nuevo = ComponerTexto(mejorCancion);
            if (nuevo != ultimoTexto)
            {
                ultimoTexto = nuevo;
                texto.text = nuevo;
                // El panel se ajusta al ancho del texto
                float ancho = texto.GetPreferredValues(nuevo).x;
                panel.sizeDelta = new Vector2(Mathf.Min(1800f, ancho + 120f), panel.sizeDelta.y);
            }
        }

        float objetivo = hayPeticion ? 1f : 0f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, objetivo, Time.unscaledDeltaTime / Mathf.Max(0.01f, duracionFade));
        hayPeticion = false;
    }

    string ComponerTexto(string cancion)
    {
        bool es = PlayerPrefs.GetInt(MenuSystem.ClaveIdioma, 0) == 0;
        string boton = $"<color=#FFD54A>{(UsandoMando ? botonMando : botonTeclado)}</color>";
        return es ? $"Presiona {boton} para jugar: {cancion}" : $"Press {boton} to play: {cancion}";
    }

    // El último dispositivo que se usó decide qué botón se muestra.
    void RegistrarDispositivo()
    {
        Gamepad pad = Gamepad.current;
        if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f
                            || pad.rightStick.ReadValue().sqrMagnitude > 0.04f
                            || pad.dpad.ReadValue().sqrMagnitude > 0f
                            || pad.buttonSouth.isPressed || pad.buttonWest.isPressed
                            || pad.buttonEast.isPressed || pad.buttonNorth.isPressed))
            ultimoUsoMando = Time.unscaledTime;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if ((kb != null && kb.anyKey.isPressed)
            || (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0f || mouse.leftButton.isPressed)))
            ultimoUsoOtro = Time.unscaledTime;
    }

    void ConstruirUI()
    {
        // Por debajo del menú de pausa (100) y de las pantallas finales (110)
        Canvas canvas = MenuUI.CrearCanvas(transform, "MensajeCanvas", 90);

        RectTransform raiz = MenuUI.Nuevo("Raiz", canvas.transform);
        MenuUI.Estirar(raiz);
        grupo = raiz.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false; // nunca debe tapar clics

        panel = MenuUI.Nuevo("Panel", raiz);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, 170f);
        panel.sizeDelta = new Vector2(900f, 110f);
        Image fondo = MenuUI.Imagen(panel, spriteFondo, MenuUI.ColorPanel, true);
        fondo.raycastTarget = false;

        EstiloUI estilo = new EstiloUI { fuente = fuente, spriteFondo = spriteFondo };
        texto = MenuUI.Texto(estilo, panel, "Texto", 44f, Vector2.zero, Vector2.zero);
        MenuUI.Estirar(texto.rectTransform);
    }

    void OnValidate() => ValidateStyle();

    void ValidateStyle()
    {
        if (fuente == null) Debug.LogWarning("[MensajeInteraccion] Falta Fuente; se usará la fuente TMP por defecto.", this);
        if (spriteFondo == null) Debug.LogWarning("[MensajeInteraccion] Falta Sprite Fondo; el panel se mostrará sin sprite.", this);
    }
}
