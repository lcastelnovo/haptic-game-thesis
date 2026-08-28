using System.Collections.Generic;
using UnityEngine;

namespace HapticResearch.Audio
{
    // Riproduttore centrale delle battute vocali PRE-GENERATE (voce ElevenLabs).
    //
    // Le tracce sono file audio dentro Resources/Voice/ caricati PER NOME (chiave), non per
    // riferimento Inspector: cosi' aggiungere/rigenerare una voce = mettere il file in
    // Assets/Resources/Voice/<chiave>.mp3, senza ricablare nulla. La chiave nel gioco combacia
    // con quella in Tools/voice_lines.json usata dallo script di generazione.
    //
    // Offline al 100%: in partita si sentono clip locali, nessuna chiamata di rete e nessuna
    // API key nel gioco (la key serve solo alla generazione, sul PC di sviluppo).
    //
    // Una sola battuta per volta: Speak() interrompe e parla subito, SpeakQueued() accoda.
    // Cosi' "istruzioni -> primo annuncio" o "giusto -> prossimo annuncio" non si sovrappongono.
    public class NarrationManager : MonoBehaviour
    {
        public static NarrationManager Instance { get; private set; }

        [Tooltip("Volume della voce.")]
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        [Tooltip("Sottocartella dentro Resources da cui caricare le tracce (Resources/<cartella>/<chiave>).")]
        [SerializeField] private string resourceFolder = "Voice";

        private AudioSource source;
        // Cache per chiave (memorizza anche i null: evita Resources.Load ripetute per clip mancanti).
        private readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private readonly Queue<AudioClip> queue = new Queue<AudioClip>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Sorgente dedicata 2D: la voce si sente sempre, indipendente dalla posizione.
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.loop = false;
            source.volume = volume;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // Scodamento: appena finisce la battuta corrente, parte la prossima in coda.
            if (queue.Count > 0 && source != null && !source.isPlaying)
            {
                source.clip = queue.Dequeue();
                source.Play();
            }
        }

        private AudioClip Load(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (cache.TryGetValue(key, out var clip)) return clip;
            clip = Resources.Load<AudioClip>($"{resourceFolder}/{key}");
            cache[key] = clip; // cache anche se null
            return clip;
        }

        // C'e' una traccia pre-generata per questa chiave?
        public bool Has(string key) => Load(key) != null;

        // Interrompe qualunque cosa e parla subito (es. ri-annuncio richiesto dall'operatore).
        public void Speak(string key)
        {
            var clip = Load(key);
            if (clip == null || source == null) return;
            queue.Clear();
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }

        // Accoda: parte quando la battuta corrente (e quelle gia' in coda) sono finite.
        public void SpeakQueued(string key)
        {
            var clip = Load(key);
            if (clip == null || source == null) return;

            if (!source.isPlaying && queue.Count == 0)
            {
                source.clip = clip;
                source.volume = volume;
                source.Play();
            }
            else
            {
                queue.Enqueue(clip);
            }
        }

        public void StopSpeaking()
        {
            if (source != null) source.Stop();
            queue.Clear();
        }

        public bool IsSpeaking => (source != null && source.isPlaying) || queue.Count > 0;
    }
}
