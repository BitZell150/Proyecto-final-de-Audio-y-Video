using Resonance;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(400)]
public class ImmersiveAudioController : MonoBehaviour
{
    [Header("Mixer")]
    [SerializeField] AudioMixer mixer;
    [SerializeField] AudioMixerGroup grupoSFX;

    [Header("Respiracion")]
    [SerializeField] AudioClip clipRespiracion;
    [SerializeField] bool respiracionProcedural = true;
    [SerializeField, Range(0f, 1f)] float volumenRespiracion = 0.08f;
    [SerializeField] float duracionFadeRespiracion = 1.2f;
    [SerializeField] float amplitudRespiracionCamara = 0.012f;
    [SerializeField] float frecuenciaRespiracionCamara = 0.5f;

    [Header("Navegacion")]
    [SerializeField, Range(0f, 1f)] float volumenNavegacion = 0.3f;
    [SerializeField] float duracionDucking = 0.45f;
    [SerializeField] float duckingDb = -5f;

    [Header("Ambientes 3D")]
    [SerializeField] AudioClip[] ambientes;
    [SerializeField] Transform[] puntosAmbiente;
    [SerializeField] float distanciaMinimaAmbiente = 2f;
    [SerializeField] float distanciaMaximaAmbiente = 24f;

    [Header("Reverb")]
    [SerializeField] AudioReverbZone[] zonasReverb;

    AudioSource respiracion;
    AudioSource navegacion;
    PlayerMovement movimiento;
    Camera camara;
    Vector3 posicionBaseCamara;
    ResonanceMusicDirector director;
    MusicDegradation degradation;
    float duckTimer;
    float lastDuckingAmount;
    bool lastDuckingUsedMixerValue;
    ResonanceMusicDirector.MusicState lastState;
    bool hasState;

    void Awake()
    {
        director = FindAnyObjectByType<ResonanceMusicDirector>();
        degradation = FindAnyObjectByType<MusicDegradation>();
        movimiento = GetComponent<PlayerMovement>();
        camara = GetComponentInChildren<Camera>();
        if (camara != null) posicionBaseCamara = camara.transform.localPosition;

        respiracion = CreateSource("Breathing", false, 0.15f, 8f);
        navegacion = CreateSource("Navigation", true, 1f, 12f);

        if (clipRespiracion != null)
            respiracion.clip = clipRespiracion;
        else if (respiracionProcedural)
            respiracion.clip = CreateBreathingClip();

        respiracion.loop = respiracion.clip != null;
        respiracion.volume = 0f;
        if (respiracion.clip != null) respiracion.Play();

        ConfigureAmbientSources();
        ConfigureReverbZones();
    }

    void Update()
    {
        if (movimiento == null) movimiento = GetComponent<PlayerMovement>();
        if (director == null) director = FindAnyObjectByType<ResonanceMusicDirector>();
        if (degradation == null) degradation = FindAnyObjectByType<MusicDegradation>();

        float wrongness = degradation != null ? degradation.Current : 0f;
        bool detenido = movimiento == null || (!movimiento.IsMoving && movimiento.IsGrounded);
        float statePressure = director != null ? StateBreathing(director.State) : 0f;
        float breathingTarget = detenido ? Mathf.Clamp01(0.35f + wrongness * 0.35f + statePressure) : 0f;
        float fadeSpeed = volumenRespiracion / Mathf.Max(0.01f, duracionFadeRespiracion);
        respiracion.volume = Mathf.MoveTowards(respiracion.volume, breathingTarget * volumenRespiracion, Time.deltaTime * fadeSpeed);
        respiracion.pitch = Mathf.Lerp(0.96f, 1.04f, breathingTarget);
        float breathingTime = respiracion != null && respiracion.clip != null ? respiracion.time : Time.time;
        ActualizarMovimientoCamara(breathingTarget, breathingTime);

        if (director != null && (!hasState || lastState != director.State))
        {
            if (hasState) PlayNavigationTone(director.State);
            lastState = director.State;
            hasState = true;
        }

        duckTimer = Mathf.Max(0f, duckTimer - Time.deltaTime);
        ApplyDucking();
    }

    void OnDisable()
    {
        if (respiracion != null) respiracion.Stop();
        if (navegacion != null) navegacion.Stop();
    }

    AudioSource CreateSource(string name, bool spatial, float minDistance, float maxDistance)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.name = name;
        source.playOnAwake = false;
        source.outputAudioMixerGroup = grupoSFX;
        source.spatialBlend = spatial ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        return source;
    }

    void PlayNavigationTone(ResonanceMusicDirector.MusicState state)
    {
        float frequency = state switch
        {
            ResonanceMusicDirector.MusicState.Completed => 760f,
            ResonanceMusicDirector.MusicState.TimeUp => 110f,
            _ => 0f
        };

        if (frequency <= 0f) return;

        navegacion.volume = volumenNavegacion;
        navegacion.PlayOneShot(CreateTone(frequency, state == ResonanceMusicDirector.MusicState.TimeUp ? 0.45f : 0.18f));
        duckTimer = duracionDucking;
    }

    void ActualizarMovimientoCamara(float breathingTarget, float breathingTime)
    {
        if (camara == null) return;

        float weight = Mathf.SmoothStep(0f, 1f, breathingTarget);
        float phase = breathingTime * frecuenciaRespiracionCamara * Mathf.PI * 2f;
        Vector3 offset = new Vector3(0f, Mathf.Sin(phase), Mathf.Cos(phase) * 0.25f)
            * (amplitudRespiracionCamara * weight);
        camara.transform.localPosition = Vector3.Lerp(camara.transform.localPosition, posicionBaseCamara + offset, Time.deltaTime * 12f);
    }

    void ApplyDucking()
    {
        if (mixer == null) return;

        float baseVolume = 0f;
        bool hasBaseVolume = degradation != null && degradation.TryGetMusicVolume(out baseVolume);
        if (!hasBaseVolume && !mixer.GetFloat("MusicVolume", out baseVolume)) return;
        if (!hasBaseVolume && lastDuckingUsedMixerValue)
            baseVolume -= duckingDb * lastDuckingAmount;

        float amount = Mathf.Clamp01(duckTimer / Mathf.Max(0.01f, duracionDucking));
        mixer.SetFloat("MusicVolume", baseVolume + duckingDb * amount);
        lastDuckingAmount = amount;
        lastDuckingUsedMixerValue = !hasBaseVolume;
    }

    void ConfigureAmbientSources()
    {
        if (ambientes == null || ambientes.Length == 0)
            ambientes = new[] { CreateAmbientClip() };
        if (puntosAmbiente == null || puntosAmbiente.Length == 0)
        {
            if (director == null) return;
            puntosAmbiente = new[] { director.transform };
        }

        for (int i = 0; i < puntosAmbiente.Length; i++)
        {
            Transform point = puntosAmbiente[i];
            if (point == null) continue;

            var source = point.gameObject.AddComponent<AudioSource>();
            source.clip = ambientes[i % ambientes.Length];
            source.loop = true;
            source.playOnAwake = false;
            source.outputAudioMixerGroup = grupoSFX;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = distanciaMinimaAmbiente;
            source.maxDistance = distanciaMaximaAmbiente;
            source.dopplerLevel = 0f;
            source.Play();
        }
    }

    void ConfigureReverbZones()
    {
        if (zonasReverb == null) return;
        foreach (AudioReverbZone zone in zonasReverb)
            if (zone != null) zone.enabled = true;
    }

    static float StateBreathing(ResonanceMusicDirector.MusicState state)
    {
        return state switch
        {
            ResonanceMusicDirector.MusicState.OffRoute => 0.32f,
            ResonanceMusicDirector.MusicState.Retreating => 0.2f,
            ResonanceMusicDirector.MusicState.TimeUp => 0.55f,
            _ => 0.04f
        };
    }

    static AudioClip CreateTone(float frequency, float duration)
    {
        const int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Min(1f, t * 35f) * Mathf.Min(1f, (duration - t) * 18f);
            data[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * 0.35f;
        }

        var clip = AudioClip.Create("NavigationTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateBreathingClip()
    {
        const int sampleRate = 44100;
        const float duration = 2f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        var data = new float[samples];
        var random = new System.Random(17);

        for (int i = 0; i < samples; i++)
        {
            float phase = (i / (float)sampleRate) % 2f;
            float envelope = phase < 0.85f ? Mathf.Sin(phase / 0.85f * Mathf.PI) : 0f;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float low = Mathf.Sin(i * 0.012f) * 0.12f;
            data[i] = (noise * 0.22f + low) * envelope;
        }

        var clip = AudioClip.Create("ProceduralBreathing", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateAmbientClip()
    {
        const int sampleRate = 22050;
        const float duration = 8f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float hum = Mathf.Sin(t * 2f * Mathf.PI * 58f) * 0.035f;
            float harmonic = Mathf.Sin(t * 2f * Mathf.PI * 116f) * 0.012f;
            float swell = 0.65f + Mathf.Sin(t * 2f * Mathf.PI * 0.11f) * 0.35f;
            data[i] = (hum + harmonic) * swell;
        }

        var clip = AudioClip.Create("ProceduralMazeAmbience", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}