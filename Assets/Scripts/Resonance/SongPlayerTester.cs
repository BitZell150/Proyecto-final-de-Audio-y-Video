using UnityEngine;
using UnityEngine.InputSystem;

namespace Resonance
{
    /// <summary>
    /// PASO 0: valida que el rewind funciona, usando el Input System NUEVO (sin tocar Project Settings).
    /// Enter = iniciar | X = adelante | Z = atrás | Shift = x2 | sin tecla = pausa suave
    /// </summary>
    public class SongPlayerTester : MonoBehaviour
    {
        [SerializeField] ReversibleSongPlayer song;
        [SerializeField] float startTime = 30f;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame && !song.IsRunning) song.Begin(startTime);
            if (!song.IsRunning) return;

            float mult = kb.leftShiftKey.isPressed ? 2f : 1f;
            float rate = 0f;
            if (kb.xKey.isPressed) rate = 1f * mult;
            else if (kb.zKey.isPressed) rate = -1f * mult;

            song.SetDesiredRate(rate);
        }

        void OnGUI()
        {
            string txt = song.IsRunning
                ? $"t = {song.CurrentTime:F2}s / {song.Length:F1}s   rate = {song.CurrentRate:F2}"
                : "Pulsa ENTER para iniciar";
            GUI.Label(new Rect(10, 10, 500, 24), txt);
        }
    }
}