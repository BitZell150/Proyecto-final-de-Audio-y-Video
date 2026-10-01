using System;
using UnityEngine;

namespace Resonance
{
    /// <summary>
    /// Reproduce UNA canción completa hacia delante o hacia atrás con un único AudioSource.
    /// La dirección y velocidad se controlan con AudioSource.pitch (positivo = adelante, negativo = atrás).
    /// Con velocidad 0 el AudioSource se PAUSA: la posición queda exactamente donde estaba.
    ///
    /// Requisitos del AudioClip (Import Settings):
    ///   Load Type = Decompress On Load, Preload Audio Data = ON, NO usar Streaming.
    ///   Compression Format = Vorbis (o PCM si hay memoria de sobra). Force To Mono = OFF.
    /// </summary>
    [DefaultExecutionOrder(300)] // último: lee la velocidad ya decidida en este frame
    [RequireComponent(typeof(AudioSource))]
    public class ReversibleSongPlayer : MonoBehaviour
    {
        [Header("Clip")]
        [SerializeField] AudioClip clip;
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;

        [Header("Velocidad de reproducción")]
        [Tooltip("Unity limita AudioSource.pitch al rango [-3, 3].")]
        [SerializeField] float maxAbsRate = 3f;
        [Tooltip("Suavizado de la velocidad. Al cruzar por 0 produce un efecto 'tape stop'.")]
        [SerializeField] float rateSmoothSeconds = 0.05f;
        [Tooltip("Por debajo de este |rate| el volumen se atenúa (evita zumbidos/clics al pausar o invertir).")]
        [SerializeField] float fadeBelowRate = 0.15f;

        [Header("Bordes")]
        [Tooltip("Margen respecto al inicio/fin donde se frena la reproducción (evita el salto del loop).")]
        [SerializeField] float edgeMarginSeconds = 0.25f;

        /// <summary>Se dispara una vez cuando la canción llega al final.</summary>
        public event Action SongEnded;

        AudioSource source;
        float desiredRate;
        float currentRate;
        float rateVelocity;
        bool running;
        bool paused;
        bool endReported;

        public float Length => clip != null ? clip.length : 0f;
        public float CurrentTime => (running && clip != null) ? (float)source.timeSamples / clip.frequency : 0f;
        public float CurrentRate => currentRate;
        public bool IsRunning => running;
        public bool IsPaused => paused;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            // loop = true evita el caso en que Unity no reproduce con pitch negativo y loop desactivado.
            // El salto del loop lo evitamos nosotros con edgeMarginSeconds.
            source.loop = true;
            source.spatialBlend = 0f; // música 2D
            source.clip = clip;
        }

        /// <summary>Arranca la canción en el segundo indicado (fade-in automático).</summary>
        public void Begin(float startTimeSeconds)
        {
            if (clip == null)
            {
                Debug.LogError("[ReversibleSongPlayer] Falta asignar el AudioClip.");
                return;
            }

            source.clip = clip;
            source.pitch = 1f;
            source.volume = 0f;
            source.timeSamples = ToSample(ClampTime(startTimeSeconds));
            source.Play();

            currentRate = 0f;
            rateVelocity = 0f;
            desiredRate = 1f;
            endReported = false;
            paused = false;
            running = true;
        }

        /// <summary>Velocidad deseada: 1 = normal, -1 = marcha atrás, 0 = pausa (posición exacta).</summary>
        public void SetDesiredRate(float rate)
        {
            desiredRate = Mathf.Clamp(rate, -maxAbsRate, maxAbsRate);
        }

        /// <summary>Salto duro de posición. Puede producir un clic: úsalo solo para inicializar.</summary>
        public void Seek(float timeSeconds)
        {
            if (!running) return;
            source.timeSamples = ToSample(ClampTime(timeSeconds));
        }

        void Update()
        {
            if (!running) return;

            float t = CurrentTime;
            bool atEnd = t >= Length - edgeMarginSeconds;
            bool atStart = t <= edgeMarginSeconds;

            float target = desiredRate;
            if (atEnd && target > 0f) target = 0f;
            if (atStart && target < 0f) target = 0f;

            currentRate = Mathf.SmoothDamp(currentRate, target, ref rateVelocity, rateSmoothSeconds);

            // Si la inercia del suavizado empuja hacia un borde, frenamos en seco.
            if ((atEnd && currentRate > 0f) || (atStart && currentRate < 0f))
            {
                currentRate = 0f;
                rateVelocity = 0f;
            }

            if (atEnd && !endReported)
            {
                endReported = true;
                SongEnded?.Invoke();
            }
            else if (endReported && t < Length - edgeMarginSeconds - 0.5f)
            {
                endReported = false;
            }

            // Velocidad 0: pausa real, así la posición se conserva exactamente.
            if (Mathf.Abs(target) < 0.001f && Mathf.Abs(currentRate) < 0.02f)
            {
                currentRate = 0f;
                rateVelocity = 0f;
                if (!paused)
                {
                    source.volume = 0f;
                    source.Pause();
                    paused = true;
                }
                return;
            }

            if (paused)
            {
                source.UnPause();
                paused = false;
            }

            ApplyPitch(currentRate);
            source.volume = Mathf.Clamp01(Mathf.Abs(currentRate) / Mathf.Max(0.0001f, fadeBelowRate)) * masterVolume;
        }

        void ApplyPitch(float rate)
        {
            // Evitamos pitch == 0 exacto: usamos un mínimo de 0.01 conservando el signo.
            if (rate >= 0f) source.pitch = Mathf.Max(rate, 0.01f);
            else source.pitch = Mathf.Min(rate, -0.01f);
        }

        float ClampTime(float t) => Mathf.Clamp(t, edgeMarginSeconds, Mathf.Max(edgeMarginSeconds, Length - edgeMarginSeconds));
        int ToSample(float t) => Mathf.Clamp((int)(t * clip.frequency), 0, clip.samples - 1);
    }
}