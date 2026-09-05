using System;
using UnityEngine;
using HapticResearch.Audio;
using HapticResearch.UI;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace HapticResearch.Levels
{
    // Passaggio al livello successivo a fine partita (Level 1 -> Labirinto).
    //
    // Quando ShapeRecognitionManager segnala il completamento:
    //   - la voce suggerisce le opzioni (traccia "level1_next_hint", accodata dopo
    //     il "livello completato");
    //   - il partecipante puo' dire "avanti" (o simili) o premere N per andare avanti;
    //   - l'operatore vedente ha anche un bottone a schermo;
    //   - Invio resta il rigioco per un nuovo partecipante (gestito dal manager).
    // Prima del cambio scena: conferma parlata + dissolvenza (SceneFader).
    public class LevelFlowController : MonoBehaviour
    {
        [Header("Riferimenti")]
        [Tooltip("Se null, cerca uno ShapeRecognitionManager in scena all'avvio.")]
        [SerializeField] private ShapeRecognitionManager manager;

        [Header("Livello successivo")]
        [Tooltip("Nome della scena del livello successivo (deve stare nella Scene List).")]
        [SerializeField] private string nextSceneName = "Labyrinth";

        [Tooltip("Tasto che porta al livello successivo (attivo solo a livello completato).")]
        [SerializeField] private KeyCode nextLevelKey = KeyCode.N;

        [Tooltip("Frasi vocali per andare avanti (attive solo a livello completato).")]
        [SerializeField] private string[] nextPhrases = { "avanti", "prossimo livello", "vai avanti", "livello successivo" };

        [Header("Tracce vocali (Resources/Voice)")]
        [Tooltip("Suggerimento letto una volta a livello completato.")]
        [SerializeField] private string hintKey = "level1_next_hint";

        [Tooltip("Conferma letta prima di caricare il livello successivo.")]
        [SerializeField] private string confirmKey = "menu_start_level2";

        private bool hintSpoken;
        private bool loading;

        // Stile bottone operatore (creato lazy in OnGUI).
        private bool styleReady;
        private GUIStyle buttonStyle;

        void Awake()
        {
            if (manager == null)
            {
                var found = FindObjectsByType<ShapeRecognitionManager>(FindObjectsSortMode.None);
                if (found.Length > 0) manager = found[0];
            }
        }

        void Start()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            StartVoice();
#endif
        }

        void Update()
        {
            if (manager == null || loading) return;

            if (!manager.IsComplete)
            {
                hintSpoken = false; // se si rigioca, il suggerimento tornera' a fine partita
                return;
            }

            if (!hintSpoken)
            {
                hintSpoken = true;
                var nm = NarrationManager.Instance;
                if (nm != null && nm.Has(hintKey)) nm.SpeakQueued(hintKey); // dopo "livello completato"
            }

            if (Input.GetKeyDown(nextLevelKey)) GoToNextLevel();
        }

        // Pubblico: usato da tasto, voce e bottone a schermo. Attivo SOLO a livello
        // completato: durante la partita "avanti" detto per sbaglio non fa nulla.
        public void GoToNextLevel() => TryGoToNextLevel(out _);

        // Come sopra, ma dice anche PERCHE' non e' partito (per i sottotitoli): ritorna
        // true solo se il caricamento e' iniziato davvero.
        public bool TryGoToNextLevel(out string reason)
        {
            reason = null;
            if (loading) { reason = "caricamento in corso"; return false; }
            if (manager == null || !manager.IsComplete) { reason = "livello non ancora completato"; return false; }
            var nm = NarrationManager.Instance;

            if (string.IsNullOrEmpty(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                Debug.LogWarning($"[LevelFlow] Scena '{nextSceneName}' non caricabile (manca dalla Scene List?).");
                if (nm != null && nm.Has("menu_not_available")) nm.Speak("menu_not_available");
                reason = "scena non disponibile";
                return false;
            }

            loading = true;

            // Conferma parlata e caricamento in parallelo, come nel menu principale:
            // il labirinto si carica sotto la schermata "Caricamento..." e si entra
            // quando la voce ha finito (timeout di sicurezza).
            if (nm != null && nm.Has(confirmKey)) nm.Speak(confirmKey);
            float deadline = Time.time + 5f;
            SceneFader.LoadSceneWithFade(nextSceneName,
                () => nm == null || !nm.IsSpeaking || Time.time >= deadline);
            return true;
        }

        // --- Riconoscimento vocale (SOLO Windows) -------------------------------------

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer recognizer;

        private void StartVoice()
        {
            if (nextPhrases == null || nextPhrases.Length == 0) return;
            try
            {
                recognizer = new KeywordRecognizer(nextPhrases, ConfidenceLevel.Medium);
                recognizer.OnPhraseRecognized += OnPhraseRecognized;
                recognizer.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LevelFlow] Voce 'avanti' non attiva (microfono/pacchetto vocale?). " +
                    "Restano tasto N e bottone. Dettaglio: " + e.Message);
                recognizer = null;
            }
        }

        private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            // L'esito va nei sottotitoli DOPO la decisione: cosi' "avanti" con il
            // labirinto non in build risulta ignorato, non eseguito.
            bool started = TryGoToNextLevel(out var reason);
            VoiceSubtitles.ReportHeard(args.text, args.confidence.ToString(), started, reason);
        }

        void OnDestroy()
        {
            if (recognizer == null) return;
            recognizer.OnPhraseRecognized -= OnPhraseRecognized;
            if (recognizer.IsRunning) recognizer.Stop();
            recognizer.Dispose();
            recognizer = null;
        }
#endif

        // --- Bottone a schermo per l'operatore ----------------------------------------

        void OnGUI()
        {
            if (manager == null || !manager.IsComplete || loading) return;
            EnsureStyle();

            var rect = new Rect(Screen.width * 0.5f - 160f, Screen.height - 70f, 320f, 44f);
            if (GUI.Button(rect, "Vai al livello 2  (N)", buttonStyle))
                GoToNextLevel();
        }

        private void EnsureStyle()
        {
            if (styleReady) return;
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            styleReady = true;
        }
    }
}
