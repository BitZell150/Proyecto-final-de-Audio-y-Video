using System.Collections.Generic;
using Resonance;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantallas de victoria y game over de los laberintos, con el mismo estilo que el menú inicial
/// (fondo verde, título en fuente Stone, notas flotantes y botones).
///  - Victoria: el jugador toca el trofeo (TrofeoVictoria llama a MostrarVictoria).
///    Opcionalmente también al completar la ruta de waypoints (victoriaAlCompletarRuta).
///  - Game over: la canción termina antes de que el jugador llegue al trofeo.
/// La interfaz se construye por código; basta con tener este componente en la escena.
/// </summary>
public class EndScreen : MonoBehaviour, IMenuNavegable
{
    [Header("Escena del menú principal (debe estar en Build Settings)")]
    [SerializeField] string escenaMenu = "Menu";

    [Header("Estilo (mismo que el menú)")]
    [SerializeField] TMP_FontAsset fuente;
    [SerializeField] TMP_FontAsset fuenteTitulo;
    [SerializeField] Sprite spriteBoton;
    [SerializeField] Sprite spriteFondo;
    [SerializeField] Sprite spriteNota;

    [SerializeField] float duracionFade = 0.8f;

    [Tooltip("Si está activo, llegar al último waypoint también da la victoria (sin tocar el trofeo).")]
    [SerializeField] bool victoriaAlCompletarRuta = false;

    static readonly Color ColorVictoria = new Color(1f, 0.92f, 0.55f, 1f);
    static readonly Color ColorGameOver = new Color(1f, 0.5f, 0.4f, 1f);

    public static EndScreen Instance { get; private set; }

    ResonanceMusicDirector director;
    ReversibleSongPlayer song;
    PauseMenu pausa;
    MenuInputManager inputManager;
    EstiloUI estilo;

    GameObject raiz;
    CanvasGroup grupo;
    TextMeshProUGUI tTitulo, tSubtitulo, tReintentar, tMenu;

    readonly List<Behaviour> bloqueados = new List<Behaviour>();
    bool mostrada, victoria, cancionTerminada;
    float tiempoMostrada;
    float ultimoUsoMando = -100f, ultimoUsoOtro = -100f;

    public bool DropdownAbierto => false;

    void Awake()
    {
        Instance = this;
        ConstruirUI();
        inputManager = MenuUI.PrepararEventSystem();
        raiz.SetActive(false);
    }

    void Start()
    {
        director = FindFirstObjectByType<ResonanceMusicDirector>();
        pausa = FindFirstObjectByType<PauseMenu>();
        if (director == null) Debug.LogWarning("[EndScreen] No hay ResonanceMusicDirector en la escena.");

        // La canción es el tiempo límite: si termina antes de tocar el trofeo, es game over.
        song = FindFirstObjectByType<ReversibleSongPlayer>();
        if (song != null) song.SongEnded += AlTerminarCancion;
    }

    void OnDestroy()
    {
        if (song != null) song.SongEnded -= AlTerminarCancion;
        if (Instance == this) Instance = null;
    }

    void AlTerminarCancion()
    {
        cancionTerminada = true;
    }

    /// <summary>Lo llama el trofeo cuando el jugador lo toca.</summary>
    public void MostrarVictoria()
    {
        if (!mostrada) Mostrar(true);
    }

    void Update()
    {
        RegistrarDispositivo();

        if (mostrada)
        {
            tiempoMostrada += Time.unscaledDeltaTime;
            grupo.alpha = Mathf.Clamp01(tiempoMostrada / Mathf.Max(0.01f, duracionFade));
            return;
        }

        if (pausa != null && pausa.Pausado) return;

        if (cancionTerminada)
        {
            Mostrar(false);
        }
        else if (director != null)
        {
            if (victoriaAlCompletarRuta && director.State == ResonanceMusicDirector.MusicState.Completed) Mostrar(true);
            else if (director.State == ResonanceMusicDirector.MusicState.TimeUp) Mostrar(false);
        }
    }

    // ------------------------------------------------------------ ESTADO

    void Mostrar(bool gano)
    {
        mostrada = true;
        victoria = gano;
        tiempoMostrada = 0f;

        // Ya no tiene sentido pausar: el juego terminó.
        if (pausa != null) pausa.enabled = false;

        // El jugador no debe moverse ni mirar. La música no se toca: en victoria termina de sonar.
        bloqueados.Clear();
        BloquearSiActivo(FindFirstObjectByType<MouseLook>());
        BloquearSiActivo(FindFirstObjectByType<PlayerMovement>());
        BloquearSiActivo(FindFirstObjectByType<Interaccion>());

        AplicarTextos();
        grupo.alpha = 0f;

        // Si venía jugando con el mando, la pantalla se maneja con el mando (cursor oculto).
        MenuInputManager.Dispositivo dispositivo = ultimoUsoMando > ultimoUsoOtro
            ? MenuInputManager.Dispositivo.Mando
            : MenuInputManager.Dispositivo.Raton;

        inputManager.Destino = this;
        inputManager.enabled = true;   // OnEnable suelta el cursor
        inputManager.ForzarDispositivo(dispositivo);

        raiz.SetActive(true);
        Selectable primero = ObtenerPrimerSeleccionable();
        if (primero != null) inputManager.Seleccionar(primero.gameObject);
    }

    void BloquearSiActivo(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        bloqueados.Add(b);
    }

    void Reintentar()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void VolverAlMenuPrincipal()
    {
        if (!Application.CanStreamedLevelBeLoaded(escenaMenu))
        {
            Debug.LogError($"[EndScreen] La escena '{escenaMenu}' no está en Build Settings.");
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(escenaMenu);
    }

    void RegistrarDispositivo()
    {
        Gamepad pad = Gamepad.current;
        if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f
                            || pad.rightStick.ReadValue().sqrMagnitude > 0.04f
                            || pad.buttonSouth.isPressed))
            ultimoUsoMando = Time.unscaledTime;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if ((kb != null && kb.anyKey.isPressed)
            || (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0f || mouse.leftButton.isPressed)))
            ultimoUsoOtro = Time.unscaledTime;
    }

    // ------------------------------------------------------------ IMenuNavegable

    public Selectable ObtenerPrimerSeleccionable()
    {
        return raiz != null ? raiz.GetComponentInChildren<Selectable>() : null;
    }

    // No hay a dónde volver desde esta pantalla.
    public void Atras() { }

    // ------------------------------------------------------------ TEXTOS

    // Sin acentos ni eñes: la fuente del menú (IM Fell English SC) no incluye esos caracteres.
    void AplicarTextos()
    {
        bool es = PlayerPrefs.GetInt(MenuSystem.ClaveIdioma, 0) == 0;

        if (victoria)
        {
            tTitulo.text = es ? "VICTORIA" : "VICTORY";
            tTitulo.color = ColorVictoria;
            tSubtitulo.text = es ? "Llegaste al corazon del laberinto" : "You reached the heart of the maze";
            tReintentar.text = es ? "Jugar de nuevo" : "Play again";
        }
        else
        {
            tTitulo.text = "GAME OVER";
            tTitulo.color = ColorGameOver;
            tSubtitulo.text = es ? "La musica termino antes de llegar al corazon" : "The music ended before you reached the heart";
            tReintentar.text = es ? "Reintentar" : "Try again";
        }

        tMenu.text = es ? "Menu principal" : "Main menu";
    }

    // ------------------------------------------------------------ CONSTRUCCIÓN DE LA UI

    void ConstruirUI()
    {
        estilo = new EstiloUI { fuente = fuente, fuenteTitulo = fuenteTitulo, spriteBoton = spriteBoton, spriteFondo = spriteFondo };

        Canvas canvas = MenuUI.CrearCanvas(transform, "EndCanvas", 110);

        RectTransform rRaiz = MenuUI.Nuevo("Raiz", canvas.transform);
        MenuUI.Estirar(rRaiz);
        grupo = rRaiz.gameObject.AddComponent<CanvasGroup>();
        raiz = rRaiz.gameObject;

        // Fondo verde, igual que "Fondo" del menú (bloquea los clics hacia atrás)
        RectTransform fondo = MenuUI.Nuevo("Fondo", rRaiz);
        MenuUI.Estirar(fondo);
        MenuUI.Imagen(fondo, spriteFondo, MenuUI.ColorFondo, true);

        // Notas musicales flotantes, con los mismos valores que el menú
        RectTransform notas = MenuUI.Nuevo("Notas", rRaiz);
        MenuUI.Estirar(notas);
        NotasFlotantes efecto = notas.gameObject.AddComponent<NotasFlotantes>();
        efecto.spriteNota = spriteNota;
        efecto.cantidadMaxima = 25;
        efecto.intervaloSpawn = 0.5f;
        efecto.velocidadSubida = 25f;
        efecto.duracionVida = 6f;
        efecto.tamanoMin = 25f;
        efecto.tamanoMax = 55f;
        efecto.alphaMaxima = 0.7f;
        efecto.amplitudZigzag = 15f;

        tTitulo = MenuUI.Texto(estilo, rRaiz, "Titulo", 150f, new Vector2(0f, 230f), new Vector2(1500f, 220f), true);
        tSubtitulo = MenuUI.Texto(estilo, rRaiz, "Subtitulo", 54f, new Vector2(0f, 70f), new Vector2(1500f, 100f));
        MenuUI.CrearBoton(estilo, rRaiz, "Reintentar", new Vector2(0f, -100f), Reintentar, out tReintentar, 640f);
        MenuUI.CrearBoton(estilo, rRaiz, "MenuPrincipal", new Vector2(0f, -240f), VolverAlMenuPrincipal, out tMenu, 640f);
    }
}
