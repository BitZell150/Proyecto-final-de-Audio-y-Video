using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// La velocidad de la música depende del CAMINO, no del reloj:
    ///     rate = (segundos de canción por metro de ruta) x (velocidad del jugador)
    /// Jugador quieto = música en pausa. Jugador lento = música lenta. Retroceso real = reverse.
    ///
    ///  ARMING    : la canción NO suena hasta que el jugador llega al primer waypoint.
    ///  ON ROUTE  : avanzar -> adelante | retroceder sobre la ruta -> reverse (proporcional a la velocidad).
    ///  OFF ROUTE : siempre hacia adelante (nunca reverse), también proporcional a la velocidad.
    ///              La distorsión depende de la distancia a la ruta.
    /// Al volver a la ruta se "reancla" la canción donde esté: sin rebobinado.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ResonanceMusicDirector : MonoBehaviour
    {
        public enum RouteState { Arming, OnRoute, OffRoute }

        [Header("Referencias")]
        [SerializeField] Transform player;
        [SerializeField] MazeRoute route;
        [SerializeField] ReversibleSongPlayer song;
        [SerializeField] MusicDegradation degradation;
        [SerializeField] bool autoStart = true;

        [Header("Inicio: la canción empieza al llegar aquí")]
        [Tooltip("Arrastra aquí el PRIMER waypoint. Si lo dejas vacío se usa el primer punto de MazeRoute.")]
        [SerializeField] Transform firstWaypoint;
        [Tooltip("Distancia (m) al primer waypoint que dispara la canción.")]
        [SerializeField] float startTriggerRadius = 1.5f;

        [Header("Mapeo ruta -> canción (segundos)")]
        [SerializeField] float songStartTime = 0f;
        [Tooltip("Segundo de la canción en la meta. Se ignora si Reference Speed > 0.")]
        [SerializeField] float songEndTime = 150f;
        [Tooltip("Velocidad (m/s) a la que quieres que la música suene a ritmo normal (1x). " +
                 "Pon la 'speed' de tu PlayerMovement. Si es > 0, calcula songEndTime automáticamente. 0 = desactivado.")]
        [SerializeField] float referenceSpeed = 0f;

        [Header("Estado de ruta (metros)")]
        [SerializeField] float routeEnterRadius = 1.5f;
        [SerializeField] float routeLeaveRadius = 2.5f;
        [SerializeField] float searchWindow = 15f;

        [Header("Distorsión (metros desde la ruta)")]
        [SerializeField] float safeRadius = 1f;
        [SerializeField] float fullWrongRadius = 4f;

        [Header("Velocidad dependiente del camino")]
        [Tooltip("Suavizado (s) de las velocidades medidas. Menor = reacciona antes.")]
        [SerializeField] float speedSmoothing = 0.08f;
        [Tooltip("Velocidad (m/s) hacia atrás sobre la ruta a partir de la cual se considera retroceso. Mantenla baja si tu personaje es lento.")]
        [SerializeField] float retreatSpeed = 0.1f;
        [SerializeField] float maxForwardRate = 2f;
        [SerializeField] float maxReverseRate = 2.5f;

        [Header("Corrección de posición (solo en ruta)")]
        [Tooltip("Error (s) tolerado entre la canción y la posición esperada antes de corregir.")]
        [SerializeField] float syncDeadzone = 0.25f;
        [SerializeField] float syncGain = 2f;
        [SerializeField] float maxCorrectionRate = 1f;

        [Header("Debug")]
        [SerializeField] bool showDebug = true;

        public RouteState State { get; private set; } = RouteState.Arming;

        bool running;
        float lastProgress;
        float progressVelocity;   // m/s a lo largo de la ruta (negativo = retrocede)
        float playerSpeed;        // m/s en el plano
        Vector3 lastPlayerPos;
        float songOffset;
        float songPerMeter;       // segundos de canción por metro (media de la ruta)
        float desiredRate;
        float lastLateral, lastErr;

        void Start()
        {
            if (autoStart) Begin();
        }

        public void Begin()
        {
            route.Rebuild();

            float available = song.Length > 0f ? song.Length - 1f : songEndTime;
            if (referenceSpeed > 0f)
            {
                float wanted = songStartTime + route.Length / referenceSpeed;
                if (wanted > available)
                    Debug.LogWarning($"[Resonance] La ruta ({route.Length:F0} m) a {referenceSpeed} m/s necesita {wanted - songStartTime:F0} s de música, " +
                                     $"pero solo hay {available - songStartTime:F0} s. La música sonará más lenta que 1x a esa velocidad.");
                songEndTime = Mathf.Min(wanted, available);
            }
            else
            {
                songEndTime = Mathf.Min(songEndTime, available);
            }

            songPerMeter = (songEndTime - songStartTime) / Mathf.Max(0.01f, route.Length);

            lastPlayerPos = Flat(player.position);
            playerSpeed = 0f;
            progressVelocity = 0f;
            songOffset = 0f;
            desiredRate = 0f;
            State = RouteState.Arming;
            running = true;
        }

        void Update()
        {
            if (!running) return;

            float dt = Time.deltaTime;
            Vector3 p = Flat(player.position);
            UpdatePlayerSpeed(p, dt);

            if (State == RouteState.Arming)
            {
                TickArming(p);
                return;
            }

            bool found = route.Project(p, lastProgress - searchWindow, lastProgress + searchWindow, out var s);
            float lateral = found ? s.lateral : float.MaxValue;
            lastLateral = lateral;

            // Distorsión inmediata, basada solo en la distancia a la ruta.
            if (degradation != null)
                degradation.Target = Mathf.InverseLerp(safeRadius, fullWrongRadius, lateral);

            float radius = State == RouteState.OnRoute ? routeLeaveRadius : routeEnterRadius;
            bool onRoute = found && lateral <= radius;

            if (onRoute)
            {
                if (State == RouteState.OffRoute)
                {
                    // Regreso desde ruta incorrecta: reancla donde suena la canción (sin rebobinar).
                    songOffset = song.CurrentTime - BaseMap(s.progress);
                    lastProgress = s.progress;
                    progressVelocity = 0f;
                    State = RouteState.OnRoute;
                }
                TickOnRoute(s, dt);
            }
            else
            {
                State = RouteState.OffRoute;
                progressVelocity = 0f;
                // Fuera de ruta: solo hacia adelante, y proporcional a lo que se mueve el jugador.
                desiredRate = Mathf.Clamp(playerSpeed * songPerMeter, 0f, maxForwardRate);
            }

            song.SetDesiredRate(desiredRate);
        }

        // ------------------------------------------------------------ INICIO

        void TickArming(Vector3 p)
        {
            if (degradation != null) degradation.Target = 0f;

            Vector3 start = firstWaypoint != null ? Flat(firstWaypoint.position) : route.StartPoint;
            lastLateral = Vector3.Distance(p, start);
            if (lastLateral > startTriggerRadius) return;

            // Llegada al primer waypoint: aquí empieza la canción y el recorrido real.
            float prog = route.Project(p, 0f, searchWindow, out var s) ? s.progress : 0f;

            song.Begin(songStartTime);
            songOffset = songStartTime - BaseMap(prog);
            lastProgress = prog;
            progressVelocity = 0f;
            State = RouteState.OnRoute;
        }

        // ------------------------------------------------------------ EN RUTA

        void TickOnRoute(MazeRoute.Sample s, float dt)
        {
            if (dt > 0f)
            {
                float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, speedSmoothing));
                progressVelocity = Mathf.Lerp(progressVelocity, (s.progress - lastProgress) / dt, k);
            }
            lastProgress = s.progress;

            float expected = ExpectedSongTime(s.progress);
            float err = expected - song.CurrentTime;   // + : la canción va atrasada
            lastErr = err;

            // Pendiente local de la ruta: segundos de canción por metro en este punto.
            float localSlope = ExpectedSongTime(s.progress + 0.5f) - ExpectedSongTime(s.progress - 0.5f);

            // Velocidad = lo que avanza el jugador por el camino (+ corrección suave de posición).
            float rate = localSlope * progressVelocity + Correction(err);

            // Solo hay reverse si el jugador retrocede de verdad por la ruta.
            if (progressVelocity >= -retreatSpeed) rate = Mathf.Max(0f, rate);

            desiredRate = Mathf.Clamp(rate, -maxReverseRate, maxForwardRate);
        }

        float Correction(float err)
        {
            float a = Mathf.Abs(err) - syncDeadzone;
            if (a <= 0f) return 0f;
            return Mathf.Sign(err) * Mathf.Min(a * syncGain, maxCorrectionRate);
        }

        float BaseMap(float progress)
        {
            float k = route.progressToSong.Evaluate(route.Normalized(progress));
            return Mathf.Lerp(songStartTime, songEndTime, k);
        }

        float ExpectedSongTime(float progress)
        {
            float maxT = song.Length > 0f ? song.Length - 1f : songEndTime;
            return Mathf.Clamp(BaseMap(progress) + songOffset, songStartTime, maxT);
        }

        void UpdatePlayerSpeed(Vector3 p, float dt)
        {
            if (dt <= 0f) return;
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, speedSmoothing));
            playerSpeed = Mathf.Lerp(playerSpeed, Vector3.Distance(p, lastPlayerPos) / dt, k);
            lastPlayerPos = p;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void OnGUI()
        {
            if (!showDebug || !running) return;

            string txt = State == RouteState.Arming
                ? $"Arming | esperando el primer waypoint ({lastLateral:F1} m)"
                : $"{State} | song {song.CurrentTime:F1}s | rate {song.CurrentRate:F2} | err {lastErr:F2}s | " +
                  $"pathVel {progressVelocity:F2}m/s | lateral {lastLateral:F1}m | s/m {songPerMeter:F2} | " +
                  $"wrongness {(degradation != null ? degradation.Current : 0f):F2}";
            GUI.Label(new Rect(10, 10, 1000, 24), txt);
        }

        void OnDrawGizmosSelected()
        {
            Transform t = firstWaypoint;
            if (t == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(t.position, startTriggerRadius);
        }
    }
}