using UnityEngine;
using HapticResearch.Audio;
using HapticResearch.UI;

namespace HapticResearch.Levels
{
    // Menu iniziale PARLATO prima di Level 1 (accessibile senza vista).
    //
    // Non introduce percorsi di input nuovi: da' solo il benvenuto e spiega i comandi che
    // esistono gia' (Invio o "avvia" per iniziare, gestiti da ShapeRecognitionManager e
    // VoiceCommandController). Quando il livello parte, il benvenuto viene interrotto
    // dalle istruzioni del livello (NarrationManager.Speak interrompe) e il menu si
    // rimette in ascolto solo a livello fermo.
    //
    // Da mettere su un GameObject qualsiasi della scena (es. "MainMenu"): lo crea anche
    // il tool editor "HapticResearch/Level 1/Configura forme e menu".
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Riferimenti")]
        [Tooltip("Se null, cerca il LevelController della scena all'avvio.")]
        [SerializeField] private LevelController manager;

        [Header("Benvenuto")]
        [Tooltip("Chiave della traccia vocale in Resources/Voice (testi in Resources/Voice/voice_lines.json).")]
        [SerializeField] private string welcomeKey = "menu_welcome";

        [Tooltip("Secondi di attesa prima del benvenuto (lascia partire calibrazione/avvio scena).")]
        [SerializeField] private float welcomeDelay = 1.5f;

        [Tooltip("Tasto per riascoltare il benvenuto a livello fermo. In gioco R e' gia' il ri-annuncio della forma: nessun conflitto, qui vale solo nel menu.")]
        [SerializeField] private KeyCode repeatMenuKey = KeyCode.R;

        [Tooltip("Clip di riserva se la traccia vocale pre-generata non esiste.")]
        [SerializeField] private AudioClip welcomeFallbackClip;

        private AudioSource fallbackSource;
        private float timer;
        private bool welcomePlayed;

        void Awake()
        {
            if (manager == null) manager = LevelController.Find();

            // Sorgente 2D dedicata al solo fallback: udibile sempre.
            fallbackSource = gameObject.AddComponent<AudioSource>();
            fallbackSource.spatialBlend = 0f;
            fallbackSource.playOnAwake = false;
            fallbackSource.loop = false;
        }

        void Update()
        {
            // Il menu vive solo PRIMA del livello (o dopo un completamento, in attesa del
            // partecipante successivo): mentre si gioca resta muto e non intercetta tasti.
            bool inMenu = manager == null || (!manager.IsRunning && !manager.IsComplete);
            if (!inMenu) return;

            if (!welcomePlayed)
            {
                timer += Time.deltaTime;
                if (timer >= welcomeDelay)
                {
                    welcomePlayed = true;
                    SpeakWelcome();
                }
                return;
            }

            if (Input.GetKeyDown(repeatMenuKey)) SpeakWelcome();
        }

        private void SpeakWelcome()
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(welcomeKey))
            {
                nm.Speak(welcomeKey);
                return;
            }
            if (welcomeFallbackClip != null)
            {
                VoiceSubtitles.ReportSaid(VoiceLines.TextOf(welcomeKey) ?? $"[{welcomeKey}]", welcomeFallbackClip.length);
                fallbackSource.PlayOneShot(welcomeFallbackClip);
            }
            else Debug.LogWarning($"[MainMenu] Nessuna traccia '{welcomeKey}' e nessun clip di riserva: menu muto. Genera le voci con Tools/generate_voice_macos.py.");
        }
    }
}
