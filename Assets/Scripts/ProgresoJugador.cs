using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProgresoJugador : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public Transform correctRoute;
    public Image barraRelleno;
    public TextMeshProUGUI textoPorcentaje;

    [Header("Estilo")]
    public Color colorInicio = new Color(1f, 0.2f, 0.8f);   // rosa
    public Color colorFinal = new Color(1f, 0.85f, 0.2f);   // dorado
    public float suavizado = 5f;

    public float Progreso { get; private set; }   // 0 a 1, otros scripts pueden leerlo

    float[] acumulado;
    float total;
    float visible;

    void Start()
    {
        int n = correctRoute.childCount;
        acumulado = new float[n];
        for (int i = 1; i < n; i++)
        {
            total += Vector3.Distance(correctRoute.GetChild(i - 1).position,
                                      correctRoute.GetChild(i).position);
            acumulado[i] = total;
        }
    }

    void Update()
    {
        Progreso = CalcularProgreso(player.position);
        visible = Mathf.Lerp(visible, Progreso, Time.deltaTime * suavizado);

        barraRelleno.fillAmount = visible;
        barraRelleno.color = Color.Lerp(colorInicio, colorFinal, visible);
        textoPorcentaje.text = Mathf.RoundToInt(visible * 100f) + "%";
    }

    float CalcularProgreso(Vector3 p)
    {
        float minDist = float.MaxValue;
        float mejor = 0f;

        for (int i = 0; i < correctRoute.childCount - 1; i++)
        {
            Vector3 a = correctRoute.GetChild(i).position;
            Vector3 b = correctRoute.GetChild(i + 1).position;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            float dist = Vector3.Distance(p, a + ab * t);

            if (dist < minDist)
            {
                minDist = dist;
                mejor = acumulado[i] + t * ab.magnitude;
            }
        }
        return Mathf.Clamp01(mejor / total);
    }
}