using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Necesario para sliders y dropdown
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MenuSystem : MonoBehaviour, IMenuNavegable
{
    public const string ClaveIdioma = "Idioma"; // 0 = Español, 1 = Inglés (lo lee también PauseMenu)

    public GameObject opcionesPanel;   // Panel que se activa/desactiva
    public GameObject panelMenuPrincipal; // Contiene los botones Jugar / Opciones / Salir
    public GameObject panelModos;         // Contiene los botones de seleccion de modo

    [Header("Escenas (deben estar en Build Settings)")]
    public string escenaModo1 = "Laberinto1Disco";
    public string escenaModo2 = "Laberinto2Disco";

    [Header("Referencias UI")]
    public Slider volumenMusica;       // Slider para música
    public TMP_Dropdown idiomaDropdown;   // Dropdown con Español/Inglés
    private float volumenMinimo = 0.2f;
    private float volumenInicial = 0.3f;

    public TextMeshProUGUI JugarText;
    public TextMeshProUGUI OpcionesText;
    public TextMeshProUGUI SalirText;

    public TMP_Text fraseText;
    public TMP_Text Cancion1Text;
    public TMP_Text Cancion2Text;

    // Textos del panel de opciones
    public TextMeshProUGUI OpcionesTitulo;
    public TextMeshProUGUI MusicaText;
    public TextMeshProUGUI IdiomaText;

    public TextMeshProUGUI Cancion1;
    public TextMeshProUGUI Cancion2;

    GameObject seleccionPrevia; // Botón seleccionado antes de abrir las opciones

    public bool DropdownAbierto => idiomaDropdown != null && idiomaDropdown.IsExpanded;

    void Start()
    {
        Time.timeScale = 1f;

        // Ocultar el panel al inicio
        if (opcionesPanel != null)
        {
            opcionesPanel.SetActive(false);
        }

        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
        if (panelModos != null) panelModos.SetActive(false);

        float volumen = Mathf.Clamp(PlayerPrefs.GetFloat("VolumenMusica", volumenInicial), volumenMinimo, 1f);

        if (volumenMusica != null)
        {
            volumenMusica.minValue = 0f;
            volumenMusica.maxValue = 1f;
            volumenMusica.SetValueWithoutNotify(volumen);
        }

        AudioListener.volume = volumen; // aplica el volumen real al iniciar

        // Idioma elegido en una visita anterior (menú o pausa)
        if (idiomaDropdown != null && PlayerPrefs.HasKey(ClaveIdioma))
        {
            idiomaDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt(ClaveIdioma));
            CambiarIdioma();
        }
    }

    // Método para iniciar el juego: muestra la selección de modo
    public void Jugar()
    {
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelModos != null) panelModos.SetActive(true);

        SeleccionarPrimero();
    }

    public void VolverAlMenuPrincipal()
    {
        if (panelModos != null) panelModos.SetActive(false);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);

        SeleccionarPrimero();
    }

    public void SeleccionarModo1()
    {
        PlayerPrefs.SetInt("ModoSeleccionado", 1);
        CargarEscena(escenaModo1);
    }

    public void SeleccionarModo2()
    {
        PlayerPrefs.SetInt("ModoSeleccionado", 2);
        CargarEscena(escenaModo2);
    }

    private void CargarEscena(string nombreEscena)
    {
        if (!Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            Debug.LogError($"[MenuSystem] La escena '{nombreEscena}' no está en Build Settings.");
            return;
        }

        PlayerPrefs.Save();
        SceneManager.LoadScene(nombreEscena);
    }

    // Método para salir del juego
    public void Salir()
    {
        Debug.Log("Saliendo del juego...");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Método para abrir/cerrar el panel de opciones
    public void Opciones()
    {
        if (opcionesPanel == null) return;

        bool abrir = !opcionesPanel.activeSelf;

        if (abrir)
        {
            seleccionPrevia = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        }

        opcionesPanel.SetActive(abrir);

        // Con el panel de opciones abierto, el mando/teclado no debe poder llegar a los botones de atrás.
        BloquearNavegacionMenuPrincipal(abrir);

        if (abrir)
        {
            SeleccionarPrimero();
        }
        else if (seleccionPrevia != null && seleccionPrevia.activeInHierarchy)
        {
            if (MenuInputManager.Instance != null) MenuInputManager.Instance.Seleccionar(seleccionPrevia);
        }
        else
        {
            SeleccionarPrimero();
        }
    }

    // Vuelve atrás (botón B del mando / Escape): cierra opciones o sale de la selección de modo.
    public void Atras()
    {
        if (opcionesPanel != null && opcionesPanel.activeSelf)
        {
            Opciones();
        }
        else if (panelModos != null && panelModos.activeSelf)
        {
            VolverAlMenuPrincipal();
        }
    }

    // Primer elemento seleccionable del panel que esté al frente.
    public Selectable ObtenerPrimerSeleccionable()
    {
        GameObject raiz = null;

        if (opcionesPanel != null && opcionesPanel.activeSelf) raiz = opcionesPanel;
        else if (panelModos != null && panelModos.activeSelf) raiz = panelModos;
        else if (panelMenuPrincipal != null && panelMenuPrincipal.activeSelf) raiz = panelMenuPrincipal;

        return raiz != null ? raiz.GetComponentInChildren<Selectable>() : null;
    }

    void SeleccionarPrimero()
    {
        if (MenuInputManager.Instance == null) return;

        Selectable primero = ObtenerPrimerSeleccionable();
        if (primero != null) MenuInputManager.Instance.Seleccionar(primero.gameObject);
    }

    void BloquearNavegacionMenuPrincipal(bool bloquear)
    {
        if (panelMenuPrincipal == null) return;

        foreach (Selectable s in panelMenuPrincipal.GetComponentsInChildren<Selectable>(true))
        {
            Navigation nav = s.navigation;
            nav.mode = bloquear ? Navigation.Mode.None : Navigation.Mode.Automatic;
            s.navigation = nav;
        }
    }

    // Método para aplicar cambios de idioma
    public void CambiarIdioma()
    {
        PlayerPrefs.SetInt(ClaveIdioma, idiomaDropdown.value);

        if (idiomaDropdown.value == 0) // Español
        {
            JugarText.text = "Jugar";
            OpcionesText.text = "Opciones";
            SalirText.text = "Salir";

            fraseText.text = "Sigue la musica, llega al corazon";

            Cancion1Text.text = "Cancion 1";
            Cancion2Text.text = "Cancion 2";

            OpcionesTitulo.text = "OPCIONES";
            MusicaText.text = "Música";
            IdiomaText.text = "Idioma";
        }
        else // Inglés
        {
            JugarText.text = "Play";
            OpcionesText.text = "Options";
            SalirText.text = "Exit";

            fraseText.text = "Follow the music; it reaches the heart.";

            Cancion1Text.text = "Song 1";
            Cancion2Text.text = "Song 2";

            OpcionesTitulo.text = "OPTIONS";
            MusicaText.text = "Music";
            IdiomaText.text = "Language";
        }
    }

    // Método para aplicar cambios de volumen
    public void CambiarVolumenMusica()
    {
        // Si el usuario intenta bajar de 0.2, el botón vuelve a 0.2
        if (volumenMusica.value < volumenMinimo)
        {
            volumenMusica.SetValueWithoutNotify(volumenMinimo);
        }

        float volumen = volumenMusica.value;
        AudioListener.volume = volumen;
        PlayerPrefs.SetFloat("VolumenMusica", volumen);
    }

}
