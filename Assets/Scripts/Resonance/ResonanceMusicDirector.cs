using System.Collections.Generic;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Cerebro del sistema. Decide en cada frame la VELOCIDAD de reproducción (negativa = atrás)
    /// y el nivel de degradación de la música.
    ///
    /// EN RUTA      : la canción avanza a 1x dentro de una ventana alrededor de la posición "esperada"
    ///                (progreso en la ruta -> segundo de la canción). Si el jugador se adelanta, la canción
    ///                acelera un poco; si se detiene, la canción espera (pausa suave).
    /// FUERA DE RUTA: la canción sigue sonando hacia delante (degradada) y se dejan "migas de pan" con el
    ///                segundo de la canción en cada punto. Si el jugador camina de vuelta por sus migas,
    ///                la canción retrocede siguiendo esas marcas hasta el punto donde se bifurcó.
    /// </summary>
    public class ResonanceMusicDirector : MonoBehaviour
    {
        public enum RouteState { OnRoute, OffRoute }

        struct Crumb
        {
            public Vector3 pos;
            public float songTime;
            public float dist; // distancia acumulada desde el punto de bifurcación
            public Crumb(Vector3 pos, float songTime, float dist)
            {
                this.pos = pos; this.songTime = songTime; this.dist = dist;
            }
        }

        [Header("Referencias")]
        [SerializeField] Transform player;
        [SerializeField] MazeRoute route;
        [SerializeField] ReversibleSongPlayer song;
        [SerializeField] MusicDegradation degradation;
        [SerializeField] bool autoStart = true;

        [Header("Mapeo ruta -> canción (segundos)")]
        [Tooltip("Segundo de la canción al inicio de la ruta.")]
        [SerializeField] float songStartTime = 0f;
        [Tooltip("Segundo de la canción al final de la ruta.")]
        [SerializeField] float songEndTime = 150f;

        [Header("Detección de ruta (metros)")]
        [SerializeField] float onRouteRadius = 2f;
        [Tooltip("Mayor que onRouteRadius para tener histéresis y evitar parpadeo.")]
        [SerializeField] float offRouteRadius = 3f;
        [Tooltip("Solo se buscan tramos de ruta cerca del último progreso conocido.")]
        [SerializeField] float searchWindow = 15f;

        [Header("En ruta: ventana de sincronía (segundos de canción)")]
        [SerializeField] float lagTolerance = 1.5f;
        [SerializeField] float leadTolerance = 1.5f;
        [SerializeField] float maxForwardRate = 1.5f;
        [Tooltip("Si la canción va más adelantada que esto (p. ej. el jugador retrocedió por la ruta correcta), se rebobina.")]
        [SerializeField] float rewindThreshold = 3f;

        [Header("Backtracking (fuera de ruta)")]
        [SerializeField] float crumbSpacing = 0.75f;
        [Tooltip("Distancia máxima a la estela para considerar que el jugador 'la sigue'.")]
        [SerializeField] float trailSnapRadius = 1.5f;
        [Tooltip("Segmentos cerca de la punta que aún cuentan como 'tierra nueva'.")]
        [SerializeField] float tipMargin = 1.5f;
        [Tooltip("Ganancia del seguimiento: rate = error(s) * ganancia.")]
        [SerializeField] float followGain = 3f;
        [SerializeField] float maxReverseRate = 2.5f;
        [SerializeField] int searchSegments = 80;

        [Header("Degradación")]
        [SerializeField] float degradeStartDistance = 2f;
        [SerializeField] float degradeFullDistance = 15f;

        [Header("Debug")]
        [SerializeField] bool showDebug = true;

        public RouteState State { get; private set; } = RouteState.OnRoute;

        readonly List<Crumb> trail = new List<Crumb>();
        int trailIndex;
        bool running;

        float lastProgress;
        Vector3 lastOnRoutePos;
        float lastOnRouteSongTime;

        float desiredRate = 1f;
        float degradationTarget;

        void Start()
        {
            if (autoStart) Begin();
        }

        public void Begin()
        {
            route.Rebuild();
            if (song.Length > 0f) songEndTime = Mathf.Min(songEndTime, song.Length - 1f);

            song.Begin(songStartTime);

            lastProgress = 0f;
            lastOnRoutePos = Flat(player.position);
            lastOnRouteSongTime = songStartTime;
            trail.Clear();
            State = RouteState.OnRoute;
            running = true;
        }

        void Update()
        {
            if (!running) return;

            Vector3 p = Flat(player.position);

            bool found = route.Project(p, lastProgress - searchWindow, lastProgress + searchWindow, out var s);
            float radius = State == RouteState.OnRoute ? offRouteRadius : onRouteRadius;
            bool onRoute = found && s.lateral <= radius;

            if (onRoute)
            {
                if (State == RouteState.OffRoute)
                {
                    trail.Clear();
                    State = RouteState.OnRoute;
                }
                TickOnRoute(p, s);
            }
            else
            {
                if (State == RouteState.OnRoute) BeginTrail();
                TickOffRoute(p);
            }

            song.SetDesiredRate(desiredRate);
            if (degradation != null) degradation.Target = degradationTarget;
        }

        // ---------------------------------------------------------------- EN RUTA

        void TickOnRoute(Vector3 p, MazeRoute.Sample s)
        {
            lastProgress = s.progress;
            lastOnRoutePos = p;
            lastOnRouteSongTime = song.CurrentTime;

            float target = MapProgressToSongTime(s.progress);
            desiredRate = WindowRate(target - song.CurrentTime);
            degradationTarget = 0f;
        }

        float MapProgressToSongTime(float progress)
        {
            float k = route.progressToSong.Evaluate(route.Normalized(progress));
            return Mathf.Lerp(songStartTime, songEndTime, k);
        }

        // error = objetivo - actual. Positivo: la canción va atrasada.
        float WindowRate(float err)
        {
            if (err > lagTolerance)
                return Mathf.Lerp(1f, maxForwardRate, Mathf.InverseLerp(lagTolerance, lagTolerance + 4f, err));

            if (err >= -leadTolerance) return 1f;                 // dentro de la ventana: reproducción natural
            if (err >= -rewindThreshold) return 0f;              // demasiado adelantada: espera
            return -Mathf.Lerp(1f, maxReverseRate,               // muy adelantada: rebobina
                Mathf.InverseLerp(rewindThreshold, rewindThreshold + 4f, -err));
        }

        // ---------------------------------------------------------------- FUERA DE RUTA

        void BeginTrail()
        {
            trail.Clear();
            trail.Add(new Crumb(lastOnRoutePos, lastOnRouteSongTime, 0f));
            trailIndex = 0;
            State = RouteState.OffRoute;
        }

        void TickOffRoute(Vector3 p)
        {
            FindOnTrail(p, out float cursor, out float distToTrail);

            int tip = trail.Count - 1;
            bool onTrail = distToTrail <= trailSnapRadius;
            if (onTrail) trailIndex = Mathf.Clamp(Mathf.RoundToInt(cursor), 0, tip);

            float pathDist;

            if (onTrail && cursor < tip - tipMargin)
            {
                // RETROCEDIENDO por una estela ya recorrida: la canción sigue su sello temporal.
                float target = TrailSongTime(cursor);
                desiredRate = FollowRate(target - song.CurrentTime);
                pathDist = TrailDistance(cursor);
            }
            else
            {
                // TIERRA NUEVA (o junto a la punta): la canción continúa hacia delante.
                if (!onTrail && trailIndex < tip)
                {
                    // El jugador se desvió de la estela a medio camino: descartamos lo que queda por delante.
                    trail.RemoveRange(trailIndex + 1, tip - trailIndex);
                    tip = trailIndex;
                }

                Crumb last = trail[tip];
                float d = Vector3.Distance(p, last.pos);
                if (d >= crumbSpacing)
                {
                    last = new Crumb(p, song.CurrentTime, last.dist + d);
                    trail.Add(last);
                    trailIndex = trail.Count - 1;
                    d = 0f;
                }

                desiredRate = 1f;
                pathDist = last.dist + d;
            }

            degradationTarget = Mathf.Clamp01(Mathf.InverseLerp(degradeStartDistance, degradeFullDistance, pathDist));
        }

        float FollowRate(float err)
        {
            if (Mathf.Abs(err) < 0.04f) return 0f;
            return Mathf.Clamp(err * followGain, -maxReverseRate, maxForwardRate);
        }

        // Proyecta al jugador sobre la estela (en una ventana de segmentos alrededor del último índice).
        void FindOnTrail(Vector3 p, out float cursor, out float dist)
        {
            int n = trail.Count;
            if (n == 1)
            {
                cursor = 0f;
                dist = Vector3.Distance(p, trail[0].pos);
                return;
            }

            int from = Mathf.Max(0, trailIndex - searchSegments);
            int to = Mathf.Min(n - 2, trailIndex + searchSegments);
            float best = float.MaxValue;
            cursor = trailIndex;

            for (int i = from; i <= to; i++)
            {
                Vector3 a = trail[i].pos, ab = trail[i + 1].pos - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
                float d = Vector3.Distance(p, a + ab * t);
                if (d < best)
                {
                    best = d;
                    cursor = i + t;
                }
            }
            dist = best;
        }

        float TrailSongTime(float c)
        {
            GetSegment(c, out int i, out int j, out float t);
            return Mathf.Lerp(trail[i].songTime, trail[j].songTime, t);
        }

        float TrailDistance(float c)
        {
            GetSegment(c, out int i, out int j, out float t);
            return Mathf.Lerp(trail[i].dist, trail[j].dist, t);
        }

        void GetSegment(float c, out int i, out int j, out float t)
        {
            i = Mathf.Clamp(Mathf.FloorToInt(c), 0, trail.Count - 1);
            j = Mathf.Min(i + 1, trail.Count - 1);
            t = c - i;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ---------------------------------------------------------------- DEBUG

        void OnGUI()
        {
            if (!showDebug || !running) return;
            GUI.Label(new Rect(10, 10, 700, 24),
                $"{State}  |  song {song.CurrentTime:F1}s  |  rate {song.CurrentRate:F2}  |  wrongness {degradationTarget:F2}  |  crumbs {trail.Count}");
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < trail.Count; i++)
            {
                Gizmos.DrawSphere(trail[i].pos + Vector3.up * 0.2f, 0.12f);
                if (i > 0) Gizmos.DrawLine(trail[i - 1].pos + Vector3.up * 0.2f, trail[i].pos + Vector3.up * 0.2f);
            }
        }
    }
}
