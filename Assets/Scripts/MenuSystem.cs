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

    void Start()
    {
        // Ocultar el panel al inicio
        if (opcionesPanel != null)
        {
            opcionesPanel.SetActive(false);
        }

        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
        if (panelModos != null) panelModos.SetActive(false);

        if (volumenMusica != null)
        {
            volumenMusica.minValue = 0f;
            volumenMusica.maxValue = 1f;
            volumenMusica.SetValueWithoutNotify(volumenInicial);

            AudioListener.volume = volumenInicial; // aplica el volumen real al iniciar
        }
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
