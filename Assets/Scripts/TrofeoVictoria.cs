using UnityEngine;

/// <summary>
/// Trigger del trofeo: al entrar el jugador se muestra la pantalla de victoria.
/// Va en el objeto "Trophy" junto a un Collider con Is Trigger activado.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrofeoVictoria : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        if (EndScreen.Instance != null) EndScreen.Instance.MostrarVictoria();
        else Debug.LogWarning("[TrofeoVictoria] No hay EndScreen en la escena.");
    }
}
