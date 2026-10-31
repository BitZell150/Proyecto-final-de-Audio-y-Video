using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa de los laberintos: Reanudar / Opciones / Volver al menú principal.
/// Se abre con Escape o Start del mando. La interfaz se construye por código, así que basta
/// con tener este componente en la escena (también crea el EventSystem si no existe).
/// Congela el juego (timeScale = 0), pausa el audio y suelta el cursor mientras está abierto.
/// </summary>
public class PauseMenu : MonoBehaviour, IMenuNavegable
{
    enum Panel { Principal, Opciones }

    [Header("Escena del menú principal (debe estar en Build Settings)")]
    [SerializeField] string escenaMenu = "Menu";

    [Header("Estilo (mismo que el menú)")]
    [SerializeField] TMP_FontAsset fuente;
    [SerializeField] TMP_FontAsset fuenteTitulo;
    [SerializeField] Sprite spriteBoton;
    [SerializeField] Sprite spriteFondo;
    [SerializeField] Sprite spriteKnob;

    const float VolumenMinimo = 0.2f;
    const string ClaveVolumen = "VolumenMusica";

    GameObject raiz, panelPrincipal, panelOpciones;
    Button botonOpciones;
    Slider sliderVolumen;
    TextMeshProUGUI tReanudar, tOpciones, tMenu, tTituloPausa, tTituloOpciones, tMusica, tIdioma, tVolver;

    EstiloUI estilo;
    MenuInputManager inputManager;
    readonly List<Behaviour> bloqueados = new List<Behaviour>();

    bool pausado;
    Panel panelActual;
    int frameApertura = -1, frameCierre = -1;
    int idioma; // 0 = Español, 1 = Inglés

    public bool Pausado => pausado;
    public bool DropdownAbierto => false;

    void Awake()
    {
        idioma = PlayerPrefs.GetInt(MenuSystem.ClaveIdioma, 0);
        ConstruirUI();
        inputManager = MenuUI.PrepararEventSystem();
        AplicarTextos();
        raiz.SetActive(false);
    }

    void OnDestroy()
    {
        // Si se destruye la escena estando en pausa, no dejamos el juego congelado.
        if (pausado)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;

        if (!pausado)
        {
            if (Time.frameCount == frameCierre) return;

            if (pad != null && pad.startButton.wasPressedThisFrame) Pausar(MenuInputManager.Dispositivo.Mando);
            else if (kb != null && kb.escapeKey.wasPressedThisFrame) Pausar(MenuInputManager.Dispositivo.Raton);
        }
        else if (pad != null && pad.startButton.wasPressedThisFrame)
        {
            Reanudar();
        }
    }

    // ------------------------------------------------------------ ESTADO

    public void Pausar(MenuInputManager.Dispositivo dispositivo)
    {
        if (pausado) return;

        pausado = true;
        frameApertura = Time.frameCount;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        // El jugador no debe moverse ni mirar mientras el menú está abierto.
        bloqueados.Clear();
        BloquearSiActivo(FindFirstObjectByType<MouseLook>());
        BloquearSiActivo(FindFirstObjectByType<PlayerMovement>());
        BloquearSiActivo(FindFirstObjectByType<Interaccion>());

        inputManager.Destino = this;
        inputManager.enabled = true;   // OnEnable suelta el cursor
        inputManager.ForzarDispositivo(dispositivo);

        raiz.SetActive(true);
        MostrarPanel(Panel.Principal);
    }

    public void Reanudar()
    {
        if (!pausado) return;

        pausado = false;
        frameCierre = Time.frameCount;

        raiz.SetActive(false);
        inputManager.enabled = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        foreach (Behaviour b in bloqueados) if (b != null) b.enabled = true;
        bloqueados.Clear();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void VolverAlMenuPrincipal()
    {
        if (!Application.CanStreamedLevelBeLoaded(escenaMenu))
        {
            Debug.LogError($"[PauseMenu] La escena '{escenaMenu}' no está en Build Settings.");
            return;
        }

        pausado = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(escenaMenu);
    }

    void BloquearSiActivo(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        bloqueados.Add(b);
    }

    void MostrarPanel(Panel panel)
    {
        panelActual = panel;
        panelPrincipal.SetActive(panel == Panel.Principal);
        panelOpciones.SetActive(panel == Panel.Opciones);

        if (inputManager == null) return;
        Selectable primero = ObtenerPrimerSeleccionable();
        if (primero != null) inputManager.Seleccionar(primero.gameObject);
    }

    void AbrirOpciones()
    {
        MostrarPanel(Panel.Opciones);
    }

    void CerrarOpciones()
    {
        MostrarPanel(Panel.Principal);
        // Al volver, queda seleccionado el botón "Opciones" en vez del primero.
        if (inputManager != null && botonOpciones != null) inputManager.Seleccionar(botonOpciones.gameObject);
    }

    // ------------------------------------------------------------ IMenuNavegable

    public Selectable ObtenerPrimerSeleccionable()
    {
        GameObject panel = panelActual == Panel.Opciones ? panelOpciones : panelPrincipal;
        return panel != null ? panel.GetComponentInChildren<Selectable>() : null;
    }

    public void Atras()
    {
        // Escape que acaba de abrir la pausa: no debe cerrarla en el mismo frame.
        if (!pausado || Time.frameCount == frameApertura) return;

        if (panelActual == Panel.Opciones) CerrarOpciones();
        else Reanudar();
    }

    // ------------------------------------------------------------ OPCIONES

    void CambiarVolumen(float valor)
    {
        if (valor < VolumenMinimo)
        {
            valor = VolumenMinimo;
            sliderVolumen.SetValueWithoutNotify(valor);
        }

        AudioListener.volume = valor;
        PlayerPrefs.SetFloat(ClaveVolumen, valor);
    }

    void CambiarIdioma()
    {
        idioma = idioma == 0 ? 1 : 0;
        PlayerPrefs.SetInt(MenuSystem.ClaveIdioma, idioma);
        AplicarTextos();
    }

    // Sin acentos ni eñes: la fuente del menú (IM Fell English SC) no incluye esos caracteres.
    void AplicarTextos()
    {
        bool es = idioma == 0;
        tTituloPausa.text = es ? "PAUSA" : "PAUSED";
        tReanudar.text = es ? "Reanudar" : "Resume";
        tOpciones.text = es ? "Opciones" : "Options";
        tMenu.text = es ? "Menu principal" : "Main menu";
        tTituloOpciones.text = es ? "OPCIONES" : "OPTIONS";
        tMusica.text = es ? "Musica" : "Music";
        tIdioma.text = es ? "Idioma: ES" : "Language: EN";
        tVolver.text = es ? "Volver" : "Back";
    }

    // ------------------------------------------------------------ CONSTRUCCIÓN DE LA UI

    void ConstruirUI()
    {
        estilo = new EstiloUI { fuente = fuente, fuenteTitulo = fuenteTitulo, spriteBoton = spriteBoton, spriteFondo = spriteFondo, spriteKnob = spriteKnob };

        Canvas canvas = MenuUI.CrearCanvas(transform, "PauseCanvas", 100);

        // Fondo oscuro a pantalla completa
        RectTransform rRaiz = MenuUI.Nuevo("Raiz", canvas.transform);
        MenuUI.Estirar(rRaiz);
        MenuUI.Imagen(rRaiz, null, new Color(0f, 0f, 0f, 0.65f), false);
        raiz = rRaiz.gameObject;

        // Panel principal
        RectTransform rPrincipal = CrearPanel("PanelPausa", rRaiz);
        panelPrincipal = rPrincipal.gameObject;
        tTituloPausa = MenuUI.Texto(estilo, rPrincipal, "Titulo", 90f, new Vector2(0f, 250f), new Vector2(700f, 130f), true);
        MenuUI.CrearBoton(estilo, rPrincipal, "Reanudar", new Vector2(0f, 70f), Reanudar, out tReanudar);
        botonOpciones = MenuUI.CrearBoton(estilo, rPrincipal, "Opciones", new Vector2(0f, -70f), AbrirOpciones, out tOpciones);
        MenuUI.CrearBoton(estilo, rPrincipal, "MenuPrincipal", new Vector2(0f, -210f), VolverAlMenuPrincipal, out tMenu);

        // Panel de opciones
        RectTransform rOpciones = CrearPanel("PanelOpciones", rRaiz);
        panelOpciones = rOpciones.gameObject;
        tTituloOpciones = MenuUI.Texto(estilo, rOpciones, "Titulo", 90f, new Vector2(0f, 250f), new Vector2(700f, 130f), true);
        tMusica = MenuUI.Texto(estilo, rOpciones, "Musica", 50f, new Vector2(0f, 135f), new Vector2(560f, 70f));
        sliderVolumen = MenuUI.CrearSlider(estilo, rOpciones, new Vector2(0f, 60f), new Vector2(520f, 40f));
        MenuUI.CrearBoton(estilo, rOpciones, "Idioma", new Vector2(0f, -70f), CambiarIdioma, out tIdioma);
        MenuUI.CrearBoton(estilo, rOpciones, "Volver", new Vector2(0f, -210f), CerrarOpciones, out tVolver);

        float volumen = Mathf.Clamp(PlayerPrefs.GetFloat(ClaveVolumen, AudioListener.volume), VolumenMinimo, 1f);
        sliderVolumen.SetValueWithoutNotify(volumen);
        sliderVolumen.onValueChanged.AddListener(CambiarVolumen);
    }

    RectTransform CrearPanel(string nombre, RectTransform padre)
    {
        RectTransform rt = MenuUI.Nuevo(nombre, padre);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 720f);
        rt.anchoredPosition = Vector2.zero;
        MenuUI.Imagen(rt, spriteFondo, MenuUI.ColorPanel, true);
        return rt;
    }
}
