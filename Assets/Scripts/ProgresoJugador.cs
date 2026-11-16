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
    public Color colorInicio = new Color(1f, 0.2f, 0.8f);
    public Color colorFinal = new Color(1f, 0.85f, 0.2f);
    public float suavizado = 5f;

    [Header("Seguimiento de ruta")]
    [Tooltip("Distancia a un waypoint para considerarlo alcanzado")]
    public float radioWaypoint = 4f;
    [Tooltip("Cuántos waypoints adelante puede detectar (tolera waypoints omitidos)")]
    public int waypointsAdelante = 3;
    [Tooltip("Si es true, el progreso nunca baja")]
    public bool soloAvanzar = true;
    public bool mostrarGizmos = true;

    public float Progreso { get; private set; }

    float[] acumulado;
    float total;
    float visible;
    int indice = 0;          // último waypoint alcanzado
    float maximo = 0f;

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
        int ultimo = correctRoute.childCount - 1;

        // 1) ¿Alcancé algún waypoint de los siguientes?
        int limite = Mathf.Min(ultimo, indice + waypointsAdelante);
        for (int k = limite; k > indice; k--)
        {
            if (Vector3.Distance(p, correctRoute.GetChild(k).position) <= radioWaypoint)
            {
                indice = k;
                break;
            }
        }

        if (indice >= ultimo) return soloAvanzar ? (maximo = 1f) : 1f;

        // 2) Interpolar dentro del segmento indice -> indice+1
        Vector3 a = correctRoute.GetChild(indice).position;
        Vector3 b = correctRoute.GetChild(indice + 1).position;
        Vector3 ab = b - a;
        float len = ab.magnitude;
        float t = len > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;

        float resultado = Mathf.Clamp01((acumulado[indice] + t * len) / total);

        if (soloAvanzar)
        {
            maximo = Mathf.Max(maximo, resultado);
            return maximo;
        }
        return resultado;
    }

    // Llámalo desde tu EndScreen al ganar para asegurar 100%
    public void Completar()
    {
        indice = correctRoute.childCount - 1;
        maximo = 1f;
    }

    void OnDrawGizmos()
    {
        if (!mostrarGizmos || correctRoute == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < correctRoute.childCount; i++)
        {
            Vector3 pos = correctRoute.GetChild(i).position;
            Gizmos.DrawWireSphere(pos, radioWaypoint);
            if (i > 0) Gizmos.DrawLine(correctRoute.GetChild(i - 1).position, pos);
        }
    }
}