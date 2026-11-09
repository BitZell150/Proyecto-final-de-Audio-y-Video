using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public Animator animator;
    public float speed = 3f;
    public float gravity = -9.81f;
    public float jumpHeight = 0.5f;

    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Controles")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference interactAction;

    [Header("Pasos")]
    public AudioClip sonidoPasos;
    public AudioClip[] variacionesPasos;
    public AudioMixerGroup grupoSFX;
    [Range(0f, 1f)] public float volumenPasos = 1f;
    public bool pasosIndividuales;
    [Min(0.1f)] public float intervaloPasos = 0.42f;
    [Range(0f, 1f)] public float variacionPitchPasos = 0.06f;
    public float distanciaMinimaPasos = 1f;
    public float distanciaMaximaPasos = 18f;
    [Tooltip("Velocidad horizontal mínima (m/s) para que suenen los pasos (evita sonar contra una pared).")]
    public float velocidadMinimaPasos = 0.5f;
    [Tooltip("Segundos que tarda en apagarse / encenderse el sonido al detenerse / empezar a caminar.")]
    public float fundidoPasos = 0.35f;

    Vector3 velocity;
    bool isGrounded;
    AudioSource fuentePasos;
    float temporizadorPaso;

    public bool IsGrounded => isGrounded;
    public bool IsMoving { get; private set; }
    public float HorizontalSpeed { get; private set; }

    void Awake()
    {
        if (sonidoPasos == null && (variacionesPasos == null || variacionesPasos.Length == 0)) return;

        fuentePasos = gameObject.AddComponent<AudioSource>();
        fuentePasos.clip = pasosIndividuales ? null : sonidoPasos != null ? sonidoPasos : variacionesPasos[0];
        fuentePasos.loop = !pasosIndividuales;
        fuentePasos.playOnAwake = false;
        fuentePasos.outputAudioMixerGroup = grupoSFX;
        fuentePasos.spatialBlend = 1f;
        fuentePasos.rolloffMode = AudioRolloffMode.Linear;
        fuentePasos.minDistance = distanciaMinimaPasos;
        fuentePasos.maxDistance = distanciaMaximaPasos;
        fuentePasos.dopplerLevel = 0f;
        fuentePasos.volume = 0f;
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        interactAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
        interactAction.action.Disable();

        // Pausa o pantalla final: el jugador deja de actuar, así que los pasos también.
        if (fuentePasos != null)
        {
            fuentePasos.Stop();
            fuentePasos.volume = 0f;
            temporizadorPaso = 0f;
        }
    }

    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector2 inputMove = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * inputMove.x + transform.forward * inputMove.y;

        Vector3 posicionAntes = transform.position;

        controller.Move(move * speed * Time.deltaTime);

        // Enviar la magnitud de la entrada (0 a 1) al Blend Tree del Animator
        if (animator != null)
        {
            animator.SetFloat("Speed", inputMove.magnitude);
        }

        if (jumpAction.action.WasPressedThisFrame() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // Activar la animación de salto
            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Los pasos suenan solo si el personaje avanza de verdad (no en el aire, quieto ni contra una pared).
        Vector3 desplazamiento = transform.position - posicionAntes;
        desplazamiento.y = 0f;
        float velocidadReal = Time.deltaTime > 0f ? desplazamiento.magnitude / Time.deltaTime : 0f;
        HorizontalSpeed = velocidadReal;
        IsMoving = velocidadReal > velocidadMinimaPasos;
        ActualizarPasos(isGrounded && inputMove.sqrMagnitude > 0.01f && IsMoving, velocidadReal);
    }

    void ActualizarPasos(bool caminando, float velocidadReal)
    {
        if (fuentePasos == null) return;

        if (pasosIndividuales)
        {
            fuentePasos.volume = volumenPasos;
            if (!caminando)
            {
                temporizadorPaso = 0f;
                return;
            }

            temporizadorPaso -= Time.deltaTime;
            if (temporizadorPaso <= 0f)
            {
                ReproducirPaso();
                temporizadorPaso = Mathf.Lerp(intervaloPasos, intervaloPasos * 0.65f,
                    Mathf.InverseLerp(velocidadMinimaPasos, Mathf.Max(speed, velocidadMinimaPasos), velocidadReal));
            }
            return;
        }

        float objetivo = caminando ? volumenPasos : 0f;
        float paso = volumenPasos * Time.deltaTime / Mathf.Max(0.01f, fundidoPasos);
        fuentePasos.volume = Mathf.MoveTowards(fuentePasos.volume, objetivo, paso);
        fuentePasos.pitch = caminando
            ? Mathf.Lerp(0.85f, 1.2f, Mathf.InverseLerp(velocidadMinimaPasos, Mathf.Max(speed, velocidadMinimaPasos), velocidadReal))
            : 1f;

        if (caminando)
        {
            if (!fuentePasos.isPlaying) fuentePasos.Play();
        }
        else if (fuentePasos.isPlaying && fuentePasos.volume <= 0.001f)
        {
            fuentePasos.Stop();
        }
    }

    void ReproducirPaso()
    {
        AudioClip clip = variacionesPasos != null && variacionesPasos.Length > 0
            ? variacionesPasos[Random.Range(0, variacionesPasos.Length)]
            : sonidoPasos;
        if (clip == null) return;

        fuentePasos.pitch = Random.Range(1f - variacionPitchPasos, 1f + variacionPitchPasos);
        fuentePasos.PlayOneShot(clip);
    }
}