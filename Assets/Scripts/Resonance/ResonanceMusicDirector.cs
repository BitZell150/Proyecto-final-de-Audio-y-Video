using UnityEngine;
using UnityEngine.Events;

namespace Resonance
{
    /// <summary>
    /// LA CANCIÓN ES UN RELOJ MUSICAL: empieza al llegar al primer waypoint y su final es el tiempo límite.
    ///
    ///  - Avanzar (ruta o fuera de ella): la canción avanza a 1x. Detenido: pausa exacta.
    ///  - Cada waypoint guarda el segundo de la canción en el que el jugador pasó por él.
    ///  - Retroceder POR LA RUTA CORRECTA: la canción retrocede, desde donde está, siguiendo esos segundos guardados
    ///    (interpolados entre waypoints). Nunca hay reverse fuera de ruta.
    ///  - Distorsión: depende solo de la distancia a la ruta (sin cambios).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ResonanceMusicDirector : MonoBehaviour
    {
        public enum MusicState { Arming, Advancing, Stopped, Retreating, OffRoute, Completed, TimeUp }

        [Header("Referencias")]
        [SerializeField] Transform player;
        [SerializeField] MazeRoute route;
        [SerializeField] ReversibleSongPlayer song;
        [SerializeField] MusicDegradation degradation;
        [SerializeField] bool autoStart = true;

        [Header("Inicio: la canción empieza al llegar al primer waypoint")]
        [Tooltip("Arrastra aquí el PRIMER waypoint. Si lo dejas vacío se usa el primer punto de MazeRoute.")]
        [SerializeField] Transform firstWaypoint;
        [Tooltip("Distancia (m) al primer waypoint que dispara la canción.")]
        [SerializeField] float startTriggerRadius = 1.5f;
        [Tooltip("Segundo de la canción con el que empieza el recorrido.")]
        [SerializeField] float songStartTime = 0f;

        [Header("Dentro / fuera de la ruta (metros a la línea de waypoints)")]
        [Tooltip("Se considera que el jugador ENTRA en la ruta por debajo de esta distancia.")]
        [SerializeField] float insideRadius = 2.25f;
        [Tooltip("Se considera que SALE de la ruta por encima de esta distancia (>= insideRadius).")]
        [SerializeField] float exitRadius = 3.25f;
        [SerializeField] float searchWindow = 15f;

        [Header("Distorsión (sin cambios)")]
        [SerializeField] float safeRadius = 1f;
        [SerializeField] float fullWrongRadius = 4f;

        [Header("Movimiento")]
        [Tooltip("Suavizado (s) de la velocidad del jugador.")]
        [SerializeField] float speedSmoothing = 0.08f;
        [Tooltip("Por debajo de esta velocidad (m/s) se considera que el jugador está detenido.")]
        [SerializeField] float minMoveSpeed = 0.05f;

        [Header("Waypoints y backtracking")]
        [Tooltip("Un waypoint cuenta como alcanzado a esta distancia (m) antes de llegar a él.")]
        [SerializeField] float waypointTolerance = 0.75f;
        [Tooltip("Metros por detrás del punto más avanzado para empezar a considerar un retroceso.")]
        [SerializeField] float retreatEnterDistance = 1.5f;
        [Tooltip("Segundos que debe mantenerse ese retroceso para confirmarlo (filtra falsos retrocesos).")]
        [SerializeField] float retreatConfirmSeconds = 0.12f;
        [Tooltip("Metros que debe avanzar desde el punto más retrocedido para volver a modo avance.")]
        [SerializeField] float retreatExitDistance = 1f;
        [SerializeField] float reverseGain = 6f;
        [SerializeField] float maxReverseRate = 2.5f;

        [Header("Eventos")]
        [SerializeField] UnityEvent onMazeCompleted;
        [SerializeField] UnityEvent onTimeUp;

        [Header("Debug")]
        [SerializeField] bool showDebug = true;

        public MusicState State { get; private set; } = MusicState.Arming;

        bool running, onRoute, retreating, timeUpPending;
        float lastProgress;
        float retreatTimer, retreatLowest;
        float playerSpeed;
        Vector3 lastPlayerPos;
        float lastLateral;

        // Línea temporal guardada: segundo de la canción por waypoint + la "punta" (punto más avanzado).
        float[] rec;
        int reachedIndex;
        float frontierProgress, frontierTime;

        void Start()
        {
            if (autoStart) Begin();
        }

        void OnDestroy()
        {
            if (song != null) song.SongEnded -= OnSongEnded;
        }

        public void Begin()
        {
            route.Rebuild();
            if (route.WaypointCount < 2)
            {
                Debug.LogError("[Resonance] La ruta necesita al menos 2 waypoints.");
                return;
            }

            rec = new float[route.WaypointCount];
            song.SongEnded -= OnSongEnded;
            song.SongEnded += OnSongEnded;

            lastPlayerPos = Flat(player.position);
            playerSpeed = 0f;
            timeUpPending = false;
            onRoute = false;
            retreating = false;
            State = MusicState.Arming;
            running = true;
        }

        void OnSongEnded()
        {
            if (State != MusicState.Completed) timeUpPending = true;
        }

        void Update()
        {
            if (!running) return;

            if (State == MusicState.Completed || State == MusicState.TimeUp) return;

            if (timeUpPending)
            {
                State = MusicState.TimeUp;
                song.SetDesiredRate(0f);
                if (degradation != null) degradation.Target = 0f;
                onTimeUp?.Invoke();
                return;
            }

            float dt = Time.deltaTime;
            Vector3 p = Flat(player.position);
            UpdatePlayerSpeed(p, dt);
            bool moving = playerSpeed > minMoveSpeed;

            if (State == MusicState.Arming)
            {
                TickArming(p);
                return;
            }

            // --- Distancia a la ruta: estado dentro/fuera + distorsión inmediata (misma lógica que antes) ---
            bool found = route.Project(p, lastProgress - searchWindow, lastProgress + searchWindow, out var s);
            float lateral = found ? s.lateral : float.MaxValue;
            lastLateral = lateral;

            if (degradation != null)
                degradation.Target = Mathf.InverseLerp(safeRadius, fullWrongRadius, lateral);

            bool nowOnRoute = found && lateral <= (onRoute ? exitRadius : insideRadius);

            if (nowOnRoute)
            {
                if (!onRoute) ReenterRoute(s.progress);
                onRoute = true;
                TickOnRoute(s.progress, moving, dt);
            }
            else
            {
                // FUERA DE RUTA: la canción sigue hacia delante (si el jugador se mueve). Nunca reverse.
                onRoute = false;
                retreating = false;
                retreatTimer = 0f;
                State = MusicState.OffRoute;
                song.SetDesiredRate(moving ? 1f : 0f);
            }
        }

        // ------------------------------------------------------------ INICIO

        void TickArming(Vector3 p)
        {
            if (degradation != null) degradation.Target = 0f;

            Vector3 start = firstWaypoint != null ? Flat(firstWaypoint.position) : route.StartPoint;
            lastLateral = Vector3.Distance(p, start);
            if (lastLateral > startTriggerRadius) return;

            // Llegada al primer waypoint: empieza la canción y la línea temporal guardada.
            float prog = route.Project(p, 0f, searchWindow, out var s) ? s.progress : 0f;
            song.Begin(songStartTime);

            rec[0] = songStartTime;
            reachedIndex = 0;
            frontierProgress = prog;
            frontierTime = songStartTime;
            lastProgress = prog;
            onRoute = true;
            retreating = false;
            retreatTimer = 0f;
            State = MusicState.Advancing;
        }

        // ------------------------------------------------------------ EN RUTA

        void TickOnRoute(float p, bool moving, float dt)
        {
            lastProgress = p;

            if (!retreating)
            {
                if (p > frontierProgress)
                {
                    AdvanceHead(p);
                    if (State == MusicState.Completed) return;
                }

                // ¿Retroceso real? Debe estar bastante por detrás de la punta y mantenerse un instante.
                if (frontierProgress - p >= retreatEnterDistance) retreatTimer += dt;
                else retreatTimer = 0f;

                if (retreatTimer >= retreatConfirmSeconds)
                {
                    retreating = true;
                    retreatLowest = p;
                }
            }
            else
            {
                retreatLowest = Mathf.Min(retreatLowest, p);

                // El jugador vuelve a avanzar: lo guardado por delante deja de ser válido.
                if (p >= retreatLowest + retreatExitDistance)
                {
                    retreating = false;
                    retreatTimer = 0f;
                    SetHead(p);
                }
            }

            if (retreating)
            {
                // Retroceder desde la posición ACTUAL hacia el segundo guardado de este punto de la ruta.
                float target = RecordedTimeAt(p);
                float err = target - song.CurrentTime;   // negativo: la canción debe retroceder
                float rate = moving ? Mathf.Clamp(err * reverseGain, -maxReverseRate, 0f) : 0f;
                song.SetDesiredRate(rate);
                State = MusicState.Retreating;
            }
            else
            {
                song.SetDesiredRate(moving ? 1f : 0f);
                State = moving ? MusicState.Advancing : MusicState.Stopped;
            }
        }

        // Regreso desde una ruta incorrecta: la canción continúa donde está; la punta se coloca aquí.
        void ReenterRoute(float progress)
        {
            retreating = false;
            retreatTimer = 0f;
            lastProgress = progress;
            SetHead(progress);
        }

        // ------------------------------------------------------------ LÍNEA TEMPORAL GUARDADA

        bool Reached(int i, float p) => p >= route.CumulativeDistance(i) - waypointTolerance;

        // La punta avanza: se guarda el segundo de la canción en cada waypoint recién alcanzado.
        void AdvanceHead(float p)
        {
            frontierProgress = p;
            frontierTime = song.CurrentTime;

            int last = route.WaypointCount - 1;
            while (reachedIndex < last && Reached(reachedIndex + 1, p))
            {
                reachedIndex++;
                rec[reachedIndex] = song.CurrentTime;
            }

            if (reachedIndex == last)
            {
                State = MusicState.Completed;
                if (degradation != null) degradation.Target = 0f;
                song.SetDesiredRate(1f);   // la canción termina de sonar
                onMazeCompleted?.Invoke();
            }
        }

        // Coloca la punta en p con el segundo actual y descarta los waypoints guardados por delante.
        void SetHead(float p)
        {
            frontierProgress = p;
            frontierTime = song.CurrentTime;
            while (reachedIndex > 0 && !Reached(reachedIndex, p)) reachedIndex--;
        }

        // Segundo de canción asociado a un punto de la ruta (interpolando entre waypoints guardados).
        float RecordedTimeAt(float p)
        {
            p = Mathf.Clamp(p, 0f, frontierProgress);

            int k = 0;
            while (k < reachedIndex && route.CumulativeDistance(k + 1) <= p) k++;

            float p0 = route.CumulativeDistance(k), t0 = rec[k];
            float p1, t1;
            if (k < reachedIndex) { p1 = route.CumulativeDistance(k + 1); t1 = rec[k + 1]; }
            else { p1 = frontierProgress; t1 = frontierTime; }

            if (p1 - p0 < 0.001f) return t0;
            return Mathf.Lerp(t0, t1, Mathf.Clamp01((p - p0) / (p1 - p0)));
        }

        void UpdatePlayerSpeed(Vector3 p, float dt)
        {
            if (dt <= 0f) return;
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, speedSmoothing));
            playerSpeed = Mathf.Lerp(playerSpeed, Vector3.Distance(p, lastPlayerPos) / dt, k);
            lastPlayerPos = p;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ------------------------------------------------------------ DEBUG

        void OnGUI()
        {
            if (!showDebug || !running) return;

            if (State == MusicState.Arming)
            {
                GUI.Label(new Rect(10, 10, 900, 24), $"Arming | esperando el primer waypoint ({lastLateral:F1} m)");
                return;
            }

            float remaining = Mathf.Max(0f, song.Length - song.CurrentTime);
            GUI.Label(new Rect(10, 10, 1100, 24),
                $"{State} | song {song.CurrentTime:F1}s (quedan {remaining:F0}s) | rate {song.CurrentRate:F2} | " +
                $"lateral {lastLateral:F1}m | wrongness {(degradation != null ? degradation.Current : 0f):F2} | " +
                $"progreso {lastProgress:F1}m / punta {frontierProgress:F1}m");

            string recs = "";
            for (int i = Mathf.Max(0, reachedIndex - 7); i <= reachedIndex; i++) recs += $"WP{i}={rec[i]:F1}s  ";
            GUI.Label(new Rect(10, 30, 1100, 24), "Guardado: " + recs);
        }

        void OnDrawGizmosSelected()
        {
            if (firstWaypoint == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firstWaypoint.position, startTriggerRadius);
        }
    }
}