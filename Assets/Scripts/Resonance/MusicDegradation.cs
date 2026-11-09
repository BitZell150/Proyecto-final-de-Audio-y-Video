using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Resonance
{
    [Serializable]
    public class MixerParam
    {
        [Tooltip("Nombre del parámetro expuesto en el AudioMixer (clic derecho > Expose).")]
        public string exposedName;
        [Tooltip("Valor con la música 'limpia' (wrongness = 0).")]
        public float cleanValue;
        [Tooltip("Valor con la música totalmente degradada (wrongness = 1).")]
        public float wrongValue;
        [Tooltip("Interpola en escala logarítmica (recomendado para frecuencias; requiere valores > 0).")]
        public bool logarithmic;
        [Tooltip("Cómo evoluciona este parámetro según la 'wrongness' (0..1).")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// Convierte un valor 0..1 ("wrongness") en varios parámetros del AudioMixer.
    /// Suavizado exponencial: responde en el mismo frame (sin la inercia inicial de SmoothDamp).
    /// </summary>
    [DefaultExecutionOrder(200)] // después del director: lee su Target del mismo frame
    public class MusicDegradation : MonoBehaviour
    {
        [SerializeField] AudioMixer mixer;
        [SerializeField] MixerParam[] parameters;
        [Tooltip("Constante de tiempo (s) al empeorar. Un valor mayor produce una transición más gradual.")]
        [SerializeField] float attackSeconds = 0.3f;
        [Tooltip("Constante de tiempo (s) al recuperarse.")]
        [SerializeField] float releaseSeconds = 0.7f;
        [Tooltip("Variación lenta y sutil sobre la degradación. Se aplica solo cuando hay alejamiento de la ruta.")]
        [SerializeField, Range(0f, 0.2f)] float dynamicVariation = 0.06f;
        [SerializeField] float variationSpeed = 0.18f;

        /// <summary>0 = música limpia, 1 = totalmente degradada.</summary>
        public float Target { get; set; }
        public float Current => current;

        public bool TryGetMusicVolume(out float value)
        {
            value = musicVolume;
            return hasMusicVolume;
        }

        float current;
        float musicVolume;
        bool hasMusicVolume;

        void Reset()
        {
            parameters = new[]
            {
                new MixerParam { exposedName = "MusicLowpass",    cleanValue = 22000f,  wrongValue = 700f,   logarithmic = true },
                new MixerParam { exposedName = "MusicVolume",     cleanValue = 0f,      wrongValue = -9f },
                new MixerParam { exposedName = "MusicDistortion", cleanValue = 0f,      wrongValue = 0.5f },
                new MixerParam { exposedName = "MusicReverbWet",  cleanValue = -10000f, wrongValue = -1500f }
            };
        }

        void Start() => Apply(0f);

        void Update()
        {
            float tau = Target > current ? attackSeconds : releaseSeconds;
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, tau));
            float target = Mathf.Clamp01(Target);
            if (target > 0f && dynamicVariation > 0f)
            {
                float variation = (Mathf.PerlinNoise(Time.time * variationSpeed, 0.37f) * 2f - 1f)
                    * dynamicVariation * Mathf.Clamp01(target * 1.5f);
                target = Mathf.Clamp01(target + variation);
            }

            current = Mathf.Lerp(current, target, k);
            Apply(current);
        }

        void Apply(float wrongness)
        {
            if (mixer == null || parameters == null) return;

            hasMusicVolume = false;
            foreach (var p in parameters)
            {
                if (string.IsNullOrEmpty(p.exposedName)) continue;

                float k = p.curve.Evaluate(wrongness);
                float v;
                if (p.logarithmic && p.cleanValue > 0f && p.wrongValue > 0f)
                    v = Mathf.Exp(Mathf.Lerp(Mathf.Log(p.cleanValue), Mathf.Log(p.wrongValue), k));
                else
                    v = Mathf.Lerp(p.cleanValue, p.wrongValue, k);

                mixer.SetFloat(p.exposedName, v);
                if (p.exposedName == "MusicVolume")
                {
                    musicVolume = v;
                    hasMusicVolume = true;
                }
            }
        }

        void OnValidate()
        {
            attackSeconds = Mathf.Max(0.001f, attackSeconds);
            releaseSeconds = Mathf.Max(0.001f, releaseSeconds);
            variationSpeed = Mathf.Max(0f, variationSpeed);
        }
    }
}