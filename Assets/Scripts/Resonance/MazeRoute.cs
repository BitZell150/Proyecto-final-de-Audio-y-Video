using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// La ruta correcta del laberinto como polilínea (plano XZ).
    /// Asigna waypoints en orden; si los dejas vacíos se usan los hijos de este objeto.
    /// Devuelve el "progreso" del jugador como distancia recorrida a lo largo de la ruta.
    /// </summary>
    public class MazeRoute : MonoBehaviour
    {
        public struct Sample
        {
            public float progress;   // metros a lo largo de la ruta
            public float lateral;    // distancia del jugador a la ruta
            public Vector3 point;    // punto proyectado sobre la ruta
        }

        [SerializeField] Transform[] waypoints;
        [Tooltip("Progreso normalizado (0..1) -> posición normalizada en el tramo de canción (0..1). Permite anclar hitos musicales.")]
        public AnimationCurve progressToSong = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        Vector3[] pts;
        float[] cum;

        public float Length { get; private set; }

        /// <summary>Primer punto de la ruta (plano XZ). Válido tras Rebuild().</summary>
        public Vector3 StartPoint => (pts != null && pts.Length > 0) ? pts[0] : transform.position;

        public int WaypointCount => pts == null ? 0 : pts.Length;

        /// <summary>Distancia (m) acumulada desde el primer waypoint hasta el waypoint i.</summary>
        public float CumulativeDistance(int i) => cum[Mathf.Clamp(i, 0, cum.Length - 1)];

        void Awake() => Rebuild();

        public void Rebuild()
        {
            Transform[] src = waypoints;
            if (src == null || src.Length < 2)
            {
                src = new Transform[transform.childCount];
                for (int i = 0; i < src.Length; i++) src[i] = transform.GetChild(i);
            }

            if (src.Length < 2)
            {
                Debug.LogError("[MazeRoute] Se necesitan al menos 2 waypoints.");
                pts = new Vector3[0];
                cum = new float[0];
                Length = 0f;
                return;
            }

            pts = new Vector3[src.Length];
            cum = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Vector3 p = src[i].position; p.y = 0f;
                pts[i] = p;
                cum[i] = i == 0 ? 0f : cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            }
            Length = cum[cum.Length - 1];
        }

        public float Normalized(float progress) => Length > 0f ? Mathf.Clamp01(progress / Length) : 0f;

        /// <summary>
        /// Proyecta p sobre la ruta, considerando SOLO tramos dentro de [minProgress, maxProgress].
        /// La ventana evita que un pasillo contiguo (al otro lado de una pared) se confunda con la ruta.
        /// </summary>
        public bool Project(Vector3 p, float minProgress, float maxProgress, out Sample best)
        {
            best = default;
            if (pts == null || pts.Length < 2) return false;

            p.y = 0f;
            float bestLat = float.MaxValue;
            bool found = false;

            for (int i = 0; i < pts.Length - 1; i++)
            {
                if (cum[i + 1] < minProgress || cum[i] > maxProgress) continue;

                Vector3 a = pts[i], ab = pts[i + 1] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
                Vector3 q = a + ab * t;
                float lat = Vector3.Distance(p, q);

                if (lat < bestLat)
                {
                    bestLat = lat;
                    best.lateral = lat;
                    best.point = q;
                    best.progress = cum[i] + Mathf.Sqrt(len2) * t;
                    found = true;
                }
            }
            return found;
        }

        void OnDrawGizmos()
        {
            Transform[] src = waypoints;
            if (src == null || src.Length < 2)
            {
                src = new Transform[transform.childCount];
                for (int i = 0; i < src.Length; i++) src[i] = transform.GetChild(i);
            }
            Gizmos.color = Color.green;
            for (int i = 0; i < src.Length - 1; i++)
            {
                if (src[i] == null || src[i + 1] == null) continue;
                Gizmos.DrawLine(src[i].position, src[i + 1].position);
                Gizmos.DrawSphere(src[i].position, 0.15f);
            }
        }
    }
}