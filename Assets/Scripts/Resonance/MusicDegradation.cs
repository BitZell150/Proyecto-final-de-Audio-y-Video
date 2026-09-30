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
    /// Convierte un único valor 0..1 ("wrongness") en varios parámetros del AudioMixer.
    /// Se degrada despacio (attack) y se recupera más rápido (release).
    /// </summary>
    public class MusicDegradation : MonoBehaviour
    {
        [SerializeField] AudioMixer mixer;
        [SerializeField] MixerParam[] parameters;
        [SerializeField] float attackTime = 1.5f;
        [SerializeField] float releaseTime = 0.6f;

        /// <summary>0 = música limpia, 1 = totalmente degradada.</summary>
        public float Target { get; set; }
        public float Current => current;

        float current, velocity;

        void Reset()
        {
            // Valores iniciales de ejemplo. Ajusta los nombres a los que expongas en tu mixer.
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
            float smooth = Target > current ? attackTime : releaseTime;
            current = Mathf.SmoothDamp(current, Mathf.Clamp01(Target), ref velocity, Mathf.Max(0.01f, smooth));
            Apply(current);
        }

        void Apply(float wrongness)
        {
            if (mixer == null || parameters == null) return;

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
            }
        }
    }
}
