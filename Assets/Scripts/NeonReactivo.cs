using UnityEngine;

public class NeonReactivo : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public Transform correctRoute;

    [Header("Ajustes")]
    public float distanciaMaxima = 6f;
    public Color colorError = new Color(1f, 0.05f, 0.05f);
    public float suavizado = 3f;

    [Header("Parpadeo Stranger Things")]
    [Range(0f, 1f)] public float umbralParpadeo = 0.15f; // desde cuánto error empieza a titilar
    public float intensidadRojo = 3f;

    class Parpadeador
    {
        float timer;
        float valor = 1f;

        public float Actualizar(float error, float umbral, float dt)
        {
            if (error < umbral) { valor = 1f; timer = 0f; return 1f; }

            timer -= dt;
            if (timer <= 0f)
            {
                float r = Random.value;
                float probApagado = error * 0.6f;

                if (r < probApagado)                 // casi apagado
                {
                    valor = Random.Range(0f, 0.08f);
                    timer = Random.Range(0.03f, 0.12f);
                }
                else if (r < probApagado + 0.2f)     // a media luz
                {
                    valor = Random.Range(0.3f, 0.6f);
                    timer = Random.Range(0.03f, 0.1f);
                }
                else                                 // encendido (pausa entre ráfagas)
                {
                    valor = 1f;
                    timer = Random.Range(0.1f, Mathf.Lerp(1.2f, 0.2f, error));
                }
            }
            return valor;
        }
    }

    Renderer[] renders;
    Light[] luces;
    Parpadeador[] pRenders, pLuces;
    Color[] lucesColorBase;
    float[] lucesIntensidadBase;
    MaterialPropertyBlock mpb;
    Color colorBueno;
    float error;

    void Start()
    {
        renders = GetComponentsInChildren<Renderer>();
        luces = GetComponentsInChildren<Light>();
        mpb = new MaterialPropertyBlock();

        // Copia del material con emisión activada
        Material mat = new Material(renders[0].sharedMaterial);
        mat.EnableKeyword("_EMISSION");
        foreach (var r in renders) r.sharedMaterial = mat;

        colorBueno = mat.GetColor("_EmissionColor");
        if (colorBueno.maxColorComponent < 0.01f)
            colorBueno = mat.GetColor("_BaseColor");

        pRenders = new Parpadeador[renders.Length];
        for (int i = 0; i < pRenders.Length; i++) pRenders[i] = new Parpadeador();

        pLuces = new Parpadeador[luces.Length];
        lucesColorBase = new Color[luces.Length];
        lucesIntensidadBase = new float[luces.Length];
        for (int i = 0; i < luces.Length; i++)
        {
            pLuces[i] = new Parpadeador();
            lucesColorBase[i] = luces[i].color;
            lucesIntensidadBase[i] = luces[i].intensity;
        }
    }

    void Update()
    {
        float d = DistanciaARuta(player.position);
        float objetivo = Mathf.Clamp01(d / distanciaMaxima);
        error = Mathf.Lerp(error, objetivo, Time.deltaTime * suavizado);

        // El color cambia suave, el parpadeo es brusco
        Color c = Color.Lerp(colorBueno, colorError * intensidadRojo, error);

        for (int i = 0; i < renders.Length; i++)
        {
            float v = pRenders[i].Actualizar(error, umbralParpadeo, Time.deltaTime);
            mpb.SetColor("_EmissionColor", c * v);
            renders[i].SetPropertyBlock(mpb);
        }

        for (int i = 0; i < luces.Length; i++)
        {
            float v = pLuces[i].Actualizar(error, umbralParpadeo, Time.deltaTime);
            luces[i].color = Color.Lerp(lucesColorBase[i], colorError, error);
            luces[i].intensity = lucesIntensidadBase[i] * v;
        }
    }

    float DistanciaARuta(Vector3 p)
    {
        float min = float.MaxValue;
        for (int i = 0; i < correctRoute.childCount - 1; i++)
        {
            Vector3 a = correctRoute.GetChild(i).position;
            Vector3 b = correctRoute.GetChild(i + 1).position;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            float dist = Vector3.Distance(p, a + ab * t);
            if (dist < min) min = dist;
        }
        return min;
    }
}