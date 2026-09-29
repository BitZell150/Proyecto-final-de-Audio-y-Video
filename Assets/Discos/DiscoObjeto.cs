using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableItem : MonoBehaviour
{
    [Header("Configuración de Rotación")]
    [Tooltip("Velocidad de rotación en los ejes X, Y, Z")]
    public Vector3 rotationSpeed = new Vector3(0f, 50f, 0f);

    [Header("Secuencia")]
    [Tooltip("Arrastra aquí el siguiente objeto que debe aparecer. Déjalo vacío si es el último.")]
    public GameObject nextItemToSpawn;

    void Update()
    {
        // Rotación constante sobre su propio eje en tiempo local
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }

    public void PickUp()
    {
        // 1. Activar el siguiente objeto de la secuencia (si existe)
        if (nextItemToSpawn != null)
        {
            nextItemToSpawn.SetActive(true);
        }

        // 2. Desactivar el objeto actual
        gameObject.SetActive(false);
    }
}