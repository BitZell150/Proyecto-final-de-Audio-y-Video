using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Necesario para sliders y dropdown
using TMPro;   


public class MenuSystem : MonoBehaviour
{
    public GameObject opcionesPanel;   // Panel que se activa/desactiva
    public GameObject panelMenuPrincipal; // Contiene los botones Jugar / Opciones / Salir
    public GameObject panelModos;         // Contiene los botones de seleccion de modo

    [Header("Referencias UI")]
    public Slider volumenMusica;       // Slider para música
    public Slider volumenSonido;       // Slider para efectos
    public TMP_Dropdown idiomaDropdown;   // Dropdown con Español/Inglés

    public TextMeshProUGUI JugarText;
    public TextMeshProUGUI OpcionesText;
    public TextMeshProUGUI SalirText;

    public TMP_Text fraseText;
    public TMP_Text Cancion1Text;
    public TMP_Text Cancion2Text;

    // Textos del panel de opciones
    public TextMeshProUGUI OpcionesTitulo;
    public TextMeshProUGUI SonidoText;
    public TextMeshProUGUI MusicaText;
    public TextMeshProUGUI IdiomaText;

    public TextMeshProUGUI Cancion1; 
    public TextMeshProUGUI Cancion2; 

    void Start()
    {
        // Ocultar el panel al inicio
        if (opcionesPanel != null)
        {
            opcionesPanel.SetActive(false);
        }

        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
        if (panelModos != null) panelModos.SetActive(false);
    }

    // Método para iniciar el juego
    public void Jugar()
    {
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelModos != null) panelModos.SetActive(true);
    }

    public void VolverAlMenuPrincipal()
    {
        if (panelModos != null) panelModos.SetActive(false);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
    }

    public void SeleccionarModo1()
    {
        PlayerPrefs.SetInt("ModoSeleccionado", 1);
        CargarSiguienteEscena();
    }

    public void SeleccionarModo2()
    {
        PlayerPrefs.SetInt("ModoSeleccionado", 2);
        CargarSiguienteEscena();
    }

    private void CargarSiguienteEscena()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // Método para salir del juego
    public void Salir()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }

    // Método para abrir/cerrar el panel de opciones
    public void Opciones()
    {
        if (opcionesPanel != null)
        {
            bool activo = opcionesPanel.activeSelf;
            opcionesPanel.SetActive(!activo);
        }
    }

    // Método para aplicar cambios de idioma
    public void CambiarIdioma()
    {
        if (idiomaDropdown.value == 0) // Español
        {
            JugarText.text = "Jugar";
            OpcionesText.text = "Opciones";
            SalirText.text = "Salir";

            fraseText.text = "Sigue el sonido, encuentra la salida";

            Cancion1Text.text = "Cancion 1";
            Cancion2Text.text = "Cancion 2";

            OpcionesTitulo.text = "OPCIONES";
            SonidoText.text = "Sonido";
            MusicaText.text = "Música";
            IdiomaText.text = "Idioma";
        }
        else // Inglés
        {
            JugarText.text = "Play";
            OpcionesText.text = "Options";
            SalirText.text = "Exit";

            fraseText.text = "Follow the sound, find the way out.";

            Cancion1Text.text = "Song 1";
            Cancion2Text.text = "Song 2";

            OpcionesTitulo.text = "OPTIONS";
            SonidoText.text = "Sound";
            MusicaText.text = "Music";
            IdiomaText.text = "Language";
        }
    }

    // Método para aplicar cambios de volumen
    public void CambiarVolumenMusica()
    {
        Debug.Log("Volumen música: " + volumenMusica.value);
        // Aquí conectas con tu AudioManager
    }

    public void CambiarVolumenSonido()
    {
        Debug.Log("Volumen sonido: " + volumenSonido.value);
        // Aquí conectas con tu AudioManager
    }
}
