using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Un menú que se puede recorrer con mando/teclado (MenuSystem, PauseMenu).</summary>
public interface IMenuNavegable
{
    /// <summary>Primer elemento a seleccionar en el panel que esté al frente.</summary>
    Selectable ObtenerPrimerSeleccionable();
    /// <summary>Botón "cancelar" (Escape / B): volver atrás.</summary>
    void Atras();
    /// <summary>True si hay un desplegable abierto que consumirá el "cancelar".</summary>
    bool DropdownAbierto { get; }
}

/// <summary>
/// Detecta si el menú se está usando con ratón, teclado o mando.
///  - Mando: oculta el cursor y mantiene siempre un botón seleccionado para navegar.
///  - Teclado: cursor visible y navegación con flechas / WASD, Enter para aceptar.
///  - Ratón: cursor visible y sin botón resaltado por navegación.
/// Escape / botón B del mando = volver atrás.
/// Va en el objeto EventSystem (siempre activo) y se ejecuta después del módulo de UI.
/// El menú que controla es MenuSystem (asignado en la escena) o el que se asigne en Destino (PauseMenu).
/// </summary>
[DefaultExecutionOrder(1000)]
public class MenuInputManager : MonoBehaviour
{
    public enum Dispositivo { Raton, Teclado, Mando }

    public static MenuInputManager Instance { get; private set; }

    [SerializeField] MenuSystem menu;
    [Tooltip("Cuánto hay que mover el stick para contar como uso del mando.")]
    [SerializeField] float umbralStick = 0.5f;
    [Tooltip("Píxeles de movimiento del ratón por frame para contar como uso del ratón.")]
    [SerializeField] float umbralRaton = 0.5f;

    public Dispositivo Actual { get; private set; } = Dispositivo.Raton;

    /// <summary>Menú que se está controlando.</summary>
    public IMenuNavegable Destino { get; set; }

    bool dropdownAbiertoPrevio;

    void Awake()
    {
        Instance = this;
        if (menu != null) Destino = menu;
    }

    void OnEnable()
    {
        // Por si se vuelve de un nivel donde MouseLook bloqueó el cursor.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        dropdownAbiertoPrevio = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        EventSystem es = EventSystem.current;
        if (es == null || Destino == null) return;

        Dispositivo? detectado = Detectar();
        if (detectado.HasValue && detectado.Value != Actual) CambiarA(detectado.Value, es);

        if (Actual != Dispositivo.Raton)
        {
            GameObject sel = es.currentSelectedGameObject;
            if (sel == null || !sel.activeInHierarchy)
            {
                Selectable primero = Destino.ObtenerPrimerSeleccionable();
                if (primero != null) es.SetSelectedGameObject(primero.gameObject);
            }
        }

        // Si el desplegable de idioma estaba abierto, el propio desplegable consume el "cancelar".
        if (Cancelar() && !dropdownAbiertoPrevio) Destino.Atras();
        dropdownAbiertoPrevio = Destino != null && Destino.DropdownAbierto;
    }

    /// <summary>Fija el dispositivo con el que se abrió el menú (p. ej. Start del mando al pausar).</summary>
    public void ForzarDispositivo(Dispositivo dispositivo)
    {
        if (EventSystem.current == null) return;
        CambiarA(dispositivo, EventSystem.current);
    }

    /// <summary>Selecciona un objeto para navegar (solo si no se está usando el ratón).</summary>
    public void Seleccionar(GameObject objetivo)
    {
        if (Actual == Dispositivo.Raton || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(objetivo);
    }

    void CambiarA(Dispositivo nuevo, EventSystem es)
    {
        Actual = nuevo;

        switch (nuevo)
        {
            case Dispositivo.Mando:
                Cursor.visible = false;
                break;
            case Dispositivo.Teclado:
                Cursor.visible = true;
                break;
            case Dispositivo.Raton:
                Cursor.visible = true;
                es.SetSelectedGameObject(null); // evita dos botones resaltados a la vez
                break;
        }
    }

    Dispositivo? Detectar()
    {
        Gamepad pad = Gamepad.current;
        if (pad != null && MandoActivo(pad)) return Dispositivo.Mando;

        Keyboard kb = Keyboard.current;
        if (kb != null && TecladoNavegando(kb)) return Dispositivo.Teclado;

        Mouse mouse = Mouse.current;
        if (mouse != null && RatonActivo(mouse)) return Dispositivo.Raton;

        return null;
    }

    bool MandoActivo(Gamepad pad)
    {
        return pad.leftStick.ReadValue().magnitude > umbralStick
            || pad.dpad.ReadValue().sqrMagnitude > 0f
            || pad.buttonSouth.wasPressedThisFrame
            || pad.buttonEast.wasPressedThisFrame
            || pad.buttonWest.wasPressedThisFrame
            || pad.buttonNorth.wasPressedThisFrame
            || pad.startButton.wasPressedThisFrame
            || pad.selectButton.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame
            || pad.rightShoulder.wasPressedThisFrame;
    }

    bool TecladoNavegando(Keyboard kb)
    {
        return kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame
            || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame
            || kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame
            || kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame
            || kb.tabKey.wasPressedThisFrame
            || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
            || kb.spaceKey.wasPressedThisFrame;
    }

    bool RatonActivo(Mouse mouse)
    {
        return mouse.delta.ReadValue().sqrMagnitude > umbralRaton * umbralRaton
            || mouse.leftButton.wasPressedThisFrame
            || mouse.rightButton.wasPressedThisFrame
            || mouse.scroll.ReadValue().sqrMagnitude > 0f;
    }

    static bool Cancelar()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.escapeKey.wasPressedThisFrame)
            || (pad != null && pad.buttonEast.wasPressedThisFrame);
    }
}
