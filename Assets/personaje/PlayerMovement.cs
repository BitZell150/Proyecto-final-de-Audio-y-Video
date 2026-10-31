using UnityEngine;
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
    [Range(0f, 1f)] public float volumenPasos = 1f;
    [Tooltip("Velocidad horizontal mínima (m/s) para que suenen los pasos (evita sonar contra una pared).")]
    public float velocidadMinimaPasos = 0.5f;
    [Tooltip("Segundos que tarda en apagarse / encenderse el sonido al detenerse / empezar a caminar.")]
    public float fundidoPasos = 0.08f;

    Vector3 velocity;
    bool isGrounded;
    AudioSource fuentePasos;

    void Awake()
    {
        if (sonidoPasos == null) return;

        fuentePasos = gameObject.AddComponent<AudioSource>();
        fuentePasos.clip = sonidoPasos;
        fuentePasos.loop = true;
        fuentePasos.playOnAwake = false;
        fuentePasos.spatialBlend = 0f; // sonido 2D, siempre se oye igual
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
        ActualizarPasos(isGrounded && inputMove.sqrMagnitude > 0.01f && velocidadReal > velocidadMinimaPasos);
    }

    void ActualizarPasos(bool caminando)
    {
        if (fuentePasos == null) return;

        float objetivo = caminando ? volumenPasos : 0f;
        float paso = volumenPasos * Time.deltaTime / Mathf.Max(0.01f, fundidoPasos);
        fuentePasos.volume = Mathf.MoveTowards(fuentePasos.volume, objetivo, paso);

        if (caminando)
        {
            if (!fuentePasos.isPlaying) fuentePasos.Play();
        }
        else if (fuentePasos.isPlaying && fuentePasos.volume <= 0.001f)
        {
            fuentePasos.Stop();
        }
    }
}