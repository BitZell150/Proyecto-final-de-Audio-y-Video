using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NotasFlotantes : MonoBehaviour
{
    [Header("Sprite de la nota")]
    public Sprite spriteNota; // Arrastra aqui tu PNG de nota musical (fondo transparente)

    [Header("Configuracion")]
    public int cantidadMaxima = 12;      // Cuantas notas activas al mismo tiempo
    public float intervaloSpawn = 1.2f;  // Cada cuanto aparece una nota nueva (segundos)
    public float velocidadSubida = 25f;  // Pixeles por segundo que sube
    public float duracionVida = 6f;      // Cuanto vive cada nota antes de desvanecerse
    public float tamanoMin = 20f;
    public float tamanoMax = 40f;
    public float alphaMaxima = 0.35f;    // Que tan visibles son (0 a 1). Bajo = sutil
    public float amplitudZigzag = 15f;   // Movimiento lateral tipo "deriva"

    private RectTransform canvasRect;
    private List<GameObject> notasActivas = new List<GameObject>();

    void Start()
    {
        canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (notasActivas.Count < cantidadMaxima)
            {
                CrearNota();
            }
            yield return new WaitForSeconds(intervaloSpawn);
        }
    }

    void CrearNota()
    {
        GameObject nota = new GameObject("Nota");
        nota.transform.SetParent(transform, false);

        Image img = nota.AddComponent<Image>();
        img.sprite = spriteNota;
        img.color = new Color(1f, 1f, 1f, 0f); // arranca invisible, se desvanece hacia adentro

        RectTransform rt = nota.GetComponent<RectTransform>();
        float tamano = Random.Range(tamanoMin, tamanoMax);
        rt.sizeDelta = new Vector2(tamano, tamano);

        // Posicion inicial: aleatoria en el ancho del canvas, abajo de la pantalla
        float mitadAncho = canvasRect.rect.width / 2f;
        float mitadAlto = canvasRect.rect.height / 2f;
        float posX = Random.Range(-mitadAncho * 0.8f, mitadAncho * 0.8f);
        float posY = -mitadAlto - tamano;
        rt.anchoredPosition = new Vector2(posX, posY);

        notasActivas.Add(nota);
        StartCoroutine(AnimarNota(nota, rt, img, posX, mitadAlto));
    }

    IEnumerator AnimarNota(GameObject nota, RectTransform rt, Image img, float posXInicial, float mitadAlto)
    {
        float tiempo = 0f;
        float offsetZigzag = Random.Range(0f, Mathf.PI * 2f);

        while (tiempo < duracionVida && nota != null)
        {
            tiempo += Time.deltaTime;
            float progreso = tiempo / duracionVida;

            // Subida constante
            rt.anchoredPosition += new Vector2(0f, velocidadSubida * Time.deltaTime);

            // Deriva lateral tipo "flotando"
            float zigzag = Mathf.Sin((tiempo * 1.2f) + offsetZigzag) * amplitudZigzag * Time.deltaTime;
            rt.anchoredPosition += new Vector2(zigzag, 0f);

            // Alpha: sube al inicio, se mantiene, baja al final (fade in/out suave)
            float alpha;
            if (progreso < 0.15f) alpha = Mathf.Lerp(0f, alphaMaxima, progreso / 0.15f);
            else if (progreso > 0.8f) alpha = Mathf.Lerp(alphaMaxima, 0f, (progreso - 0.8f) / 0.2f);
            else alpha = alphaMaxima;

            img.color = new Color(1f, 1f, 1f, alpha);

            yield return null;
        }

        if (nota != null)
        {
            notasActivas.Remove(nota);
            Destroy(nota);
        }
    }
}