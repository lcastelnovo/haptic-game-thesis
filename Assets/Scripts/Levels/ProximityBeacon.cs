using UnityEngine;
using HapticResearch.Audio;

namespace HapticResearch.Levels
{
    // Faro sonoro: un beep che batte piu' veloce e piu' acuto man mano che ci si
    // avvicina al bersaglio. Due dimensioni (ritmo e altezza) invece di una sola,
    // perche' con una sola il gradiente e' troppo fine da seguire senza vedere.
    //
    // Estratto da LabyrinthManager: serve due volte, per cercare l'ingresso e per
    // riportare in pista chi e' uscito dal labirinto.
    public class ProximityBeacon
    {
        private readonly AudioSource source;

        public AudioClip Clip;
        public float FarDistance = 1.2f;
        public float SlowInterval = 1.2f;
        public float FastInterval = 0.15f;
        public float FarPitch = 0.8f;
        public float NearPitch = 1.5f;
        public float Volume = 1f;

        // Il faro tace mentre la voce parla: due segnali sonori sovrapposti non si
        // seguono, e le istruzioni sono piu' importanti.
        public bool SilentWhileSpeaking = true;

        private float nextBeepTime;

        public ProximityBeacon(AudioSource source) { this.source = source; }

        public void Restart(float delay = 0f) => nextBeepTime = Time.time + delay;

        public void Stop() => nextBeepTime = float.PositiveInfinity;

        // distance in metri; infinito se non c'e' nessuna punta attiva.
        public void Tick(float distance)
        {
            if (Clip == null || source == null || Time.time < nextBeepTime) return;

            var nm = NarrationManager.Instance;
            if (SilentWhileSpeaking && nm != null && nm.IsSpeaking)
            {
                nextBeepTime = Time.time + 0.25f;
                return;
            }

            float t = float.IsInfinity(distance) ? 1f : Mathf.Clamp01(distance / Mathf.Max(0.01f, FarDistance));
            source.pitch = Mathf.Lerp(NearPitch, FarPitch, t);
            source.PlayOneShot(Clip, Volume);
            nextBeepTime = Time.time + Mathf.Lerp(FastInterval, SlowInterval, t);
        }
    }
}
