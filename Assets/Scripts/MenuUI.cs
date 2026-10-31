using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Recursos visuales compartidos por las pantallas creadas por código (pausa, victoria, game over).</summary>
public class EstiloUI
{
    public TMP_FontAsset fuente;        // texto normal (IM Fell English SC)
    public TMP_FontAsset fuenteTitulo;  // títulos (Stone, como "AUDIO MAZE"); si es null se usa fuente
    public Sprite spriteBoton;
    public Sprite spriteFondo;
    public Sprite spriteKnob;
}

/// <summary>Construcción de UI por código con el mismo aspecto que la escena Menu.</summary>
public static class MenuUI
{
    public static readonly Color ColorBoton = new Color(0.645f, 0.675f, 0.608f, 1f);
    public static readonly Color ColorPanel = new Color(0.352f, 0.42f, 0.224f, 0.9f);
    public static readonly Color ColorFondo = new Color(0.498f, 0.533f, 0.43f, 0.9f);

    public static Canvas CrearCanvas(Transform padre, string nombre, int orden)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Busca (o crea) el EventSystem y le añade un MenuInputManager desactivado.</summary>
    public static MenuInputManager PrepararEventSystem()
    {
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject go = new GameObject("EventSystem");
            go.SetActive(false); // se activa al final para que el módulo arranque ya configurado
            es = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            go.SetActive(true);
        }

        if (!es.TryGetComponent(out MenuInputManager manager)) manager = es.gameObject.AddComponent<MenuInputManager>();
        manager.enabled = false; // solo trabaja mientras hay un menú abierto
        return manager;
    }

    public static RectTransform Nuevo(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    public static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Centrar(RectTransform rt, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tam;
        rt.anchoredPosition = pos;
    }

    public static Image Imagen(RectTransform rt, Sprite sprite, Color color, bool cortado)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sprite != null && cortado ? Image.Type.Sliced : Image.Type.Simple;
        return img;
    }

    public static TextMeshProUGUI Texto(EstiloUI estilo, RectTransform padre, string nombre, float tamano, Vector2 pos, Vector2 tam, bool titulo = false)
    {
        RectTransform rt = Nuevo(nombre, padre);
        Centrar(rt, pos, tam);

        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset fuente = titulo && estilo.fuenteTitulo != null ? estilo.fuenteTitulo : estilo.fuente;
        if (fuente != null) t.font = fuente;
        t.fontSize = tamano;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    public static Button CrearBoton(EstiloUI estilo, RectTransform padre, string nombre, Vector2 pos, UnityAction alHacerClic, out TextMeshProUGUI texto, float ancho = 560f)
    {
        RectTransform rt = Nuevo(nombre, padre);
        Centrar(rt, pos, new Vector2(ancho, 100f));

        Image img = Imagen(rt, estilo.spriteBoton, ColorBoton, true);

        Button boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        boton.colors = ColoresBoton();
        boton.onClick.AddListener(alHacerClic);

        texto = Texto(estilo, rt, "Texto", 44f, Vector2.zero, Vector2.zero);
        Estirar(texto.rectTransform);
        return boton;
    }

    public static Slider CrearSlider(EstiloUI estilo, RectTransform padre, Vector2 pos, Vector2 tam)
    {
        RectTransform rt = Nuevo("Volumen", padre);
        Centrar(rt, pos, tam);

        RectTransform fondo = Nuevo("Background", rt);
        fondo.anchorMin = new Vector2(0f, 0.25f);
        fondo.anchorMax = new Vector2(1f, 0.75f);
        fondo.offsetMin = fondo.offsetMax = Vector2.zero;
        Imagen(fondo, estilo.spriteFondo, new Color(0.12f, 0.14f, 0.08f, 1f), true);

        RectTransform areaRelleno = Nuevo("Fill Area", rt);
        areaRelleno.anchorMin = new Vector2(0f, 0.25f);
        areaRelleno.anchorMax = new Vector2(1f, 0.75f);
        areaRelleno.offsetMin = new Vector2(5f, 0f);
        areaRelleno.offsetMax = new Vector2(-15f, 0f);

        RectTransform relleno = Nuevo("Fill", areaRelleno);
        relleno.sizeDelta = new Vector2(10f, 0f);
        Imagen(relleno, estilo.spriteBoton, new Color(0.86f, 0.986f, 0.818f, 1f), true);

        RectTransform areaMango = Nuevo("Handle Slide Area", rt);
        Estirar(areaMango);
        areaMango.offsetMin = new Vector2(10f, 0f);
        areaMango.offsetMax = new Vector2(-10f, 0f);

        RectTransform mango = Nuevo("Handle", areaMango);
        mango.sizeDelta = new Vector2(30f, 0f);
        Image imgMango = Imagen(mango, estilo.spriteKnob, Color.white, false);

        Slider slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = relleno;
        slider.handleRect = mango;
        slider.targetGraphic = imgMango;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        ColorBlock colores = ColoresBoton();
        colores.normalColor = Color.white;
        slider.colors = colores;
        return slider;
    }

    public static ColorBlock ColoresBoton()
    {
        ColorBlock c = ColorBlock.defaultColorBlock;
        c.normalColor = new Color(0.8602416f, 0.9858491f, 0.81844074f, 1f);
        c.highlightedColor = new Color(0.70333564f, 0.7311321f, 0.44488698f, 1f);
        c.pressedColor = new Color(0.5206835f, 0.6179246f, 0.21569066f, 1f);
        c.selectedColor = new Color(1f, 0.28960377f, 0f, 1f);
        c.disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.1f;
        return c;
    }
}
