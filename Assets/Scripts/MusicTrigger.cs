using UnityEngine;

public class MusicTrigger : MonoBehaviour
{
    public AudioSource music;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            music.Play();
        }
    }
}
