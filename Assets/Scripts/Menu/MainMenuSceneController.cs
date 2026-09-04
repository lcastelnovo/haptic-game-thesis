using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HapticResearch.Audio;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace HapticResearch.Menu
{
    // Controller della SCENA menu principale (MainMenu.unity), pensato per giocatori non
    // vedenti: tutto passa da voce e audio, nessun elemento da guardare o cliccare.
    //
    // All'avvio annuncia le opzioni (traccia "menu_main"); il livello si sceglie:
    //   - a VOCE: "uno" / "forme", "due" / "labirinto"... (frasi configurabili per livello)
    //   - da TASTIERA: tasto numerico per livello (fallback operatore / senza microfono)
    // "ripeti" (o R) riannuncia le opzioni. Alla scelta: conferma parlata
    // ("menu_start_<id>") e caricamento della scena del livello. La calibrazione WEART
    // parte SOLO nella scena del livello: per questo il menu resta senza rig aptico.
    //
    // Riconoscimento vocale: nativo Windows (KeywordRecognizer), stesso pattern di
    // VoiceCommandController - su Mac compila ma resta muto, funzionano i tasti.
    public class MainMenuSceneController : MonoBehaviour
    {
        // Un livello selezionabile dal menu. Tutto configurabile da Inspector.
        [Serializable]
        public class MenuLevelEntry
        {
            [Tooltip("Id logico: usato per la traccia di conferma 'menu_start_<id>' e nei log.")]
            [SerializeField] private string id = "level1";

            [Tooltip("Nome della scena da caricare (deve stare nella Scene List della build).")]
            [SerializeField] private string sceneName = "";

            [Tooltip("Tasto che seleziona questo livello (fallback senza microfono).")]
            [SerializeField] private KeyCode key = KeyCode.Alpha1;

            [Tooltip("Frasi vocali che selezionano questo livello (es. 'uno', 'forme').")]
            [SerializeField] private string[] phrases;

            public string Id => id;
            public string SceneName => sceneName;
            public KeyCode Key => key;
            public string[] Phrases => phrases;
        }

        [Header("Livelli (in ordine di annuncio)")]
        [SerializeField] private List<MenuLevelEntry> levels = new List<MenuLevelEntry>();

        [Header("Annuncio opzioni")]
        [Tooltip("Chiave della traccia con l'elenco delle opzioni (Resources/Voice).")]
        [SerializeField] private string menuKey = "menu_main";

        [Tooltip("Secondi di attesa prima dell'annuncio all'avvio della scena.")]
        [SerializeField] private float welcomeDelay = 1f;

        [Tooltip("Tasto per riascoltare le opzioni.")]
        [SerializeField] private KeyCode repeatKey = KeyCode.R;

        [Header("Riconoscimento vocale")]
        [Tooltip("Frasi che riannunciano le opzioni (oltre al tasto).")]
        [SerializeField] private string[] repeatPhrases = { "ripeti", "ripeti le opzioni", "menu", "quali livelli" };

        [Tooltip("Tasto per mutare/riattivare il microfono (operatore).")]
        [SerializeField] private KeyCode muteKey = KeyCode.M;

        // --- runtime ---
        private bool welcomeSpoken;
        private float timer;
        private bool loading; // scelta gia' fatta: ignora altri input mentre si carica

        void Start()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            BuildVocabularyAndStart();
#else
            Debug.Log("[MainMenu] Voce disponibile solo su Windows: seleziona coi tasti numerici.");
#endif
        }

        void Update()
        {
            if (loading) return;

            if (!welcomeSpoken)
            {
                timer += Time.deltaTime;
                if (timer >= welcomeDelay)
                {
                    welcomeSpoken = true;
                    AnnounceOptions();
                }
                return;
            }

            if (Input.GetKeyDown(repeatKey)) AnnounceOptions();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (muteKey != KeyCode.None && Input.GetKeyDown(muteKey)) ToggleVoice();
#endif

            foreach (var level in levels)
                if (level != null && Input.GetKeyDown(level.Key))
                {
                    SelectLevel(level);
                    return;
                }
        }

        // Pubblico: richiamato anche dal bottone "Ripeti annuncio" della UI operatore.
        public void AnnounceOptions()
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(menuKey)) nm.Speak(menuKey);
            else Debug.LogWarning($"[MainMenu] Traccia '{menuKey}' mancante: genera le voci con Tools/generate_voice_macos.py.");
        }

        // Selezione per indice della lista livelli: comodo per i bottoni UI (onClick con
        // parametro int), stessa identica logica della voce e dei tasti.
        public void SelectLevelByIndex(int index)
        {
            if (index < 0 || index >= levels.Count) return;
            SelectLevel(levels[index]);
        }

        // Conferma parlata, poi carica la scena. Pubblico: usabile anche da UI operatore.
        public void SelectLevel(MenuLevelEntry level)
        {
            if (loading || level == null) return;

            // Scena presente nella build? Se no, avvisa e resta nel menu (es. Level 2 non
            // ancora portato su questa macchina).
            if (string.IsNullOrEmpty(level.SceneName) || !Application.CanStreamedLevelBeLoaded(level.SceneName))
            {
                Debug.LogWarning($"[MainMenu] Scena '{level.SceneName}' non caricabile (manca dalla Scene List?).");
                var nm = NarrationManager.Instance;
                if (nm != null && nm.Has("menu_not_available")) nm.Speak("menu_not_available");
                return;
            }

            loading = true;
            StartCoroutine(ConfirmAndLoad(level));
        }

        private IEnumerator ConfirmAndLoad(MenuLevelEntry level)
        {
            var nm = NarrationManager.Instance;
            string confirmKey = $"menu_start_{level.Id}";
            if (nm != null && nm.Has(confirmKey))
            {
                nm.Speak(confirmKey);
                // Lascia finire la conferma prima del cambio scena (timeout di sicurezza).
                float deadline = Time.time + 5f;
                while (nm.IsSpeaking && Time.time < deadline) yield return null;
            }
            SceneManager.LoadScene(level.SceneName);
        }

        // --- Riconoscimento vocale (SOLO Windows) -------------------------------------

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer recognizer;
        private bool voiceEnabled = true;
        private readonly Dictionary<string, Action> actions =
            new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        private void BuildVocabularyAndStart()
        {
            actions.Clear();
            foreach (var level in levels)
            {
                if (level == null || level.Phrases == null) continue;
                var captured = level; // evita la capture della variabile di ciclo
                foreach (var p in level.Phrases)
                    if (!string.IsNullOrWhiteSpace(p)) actions[p.Trim()] = () => SelectLevel(captured);
            }
            foreach (var p in repeatPhrases)
                if (!string.IsNullOrWhiteSpace(p)) actions[p.Trim()] = AnnounceOptions;

            if (actions.Count == 0) return;

            var keywords = new string[actions.Count];
            actions.Keys.CopyTo(keywords, 0);
            try
            {
                recognizer = new KeywordRecognizer(keywords, ConfidenceLevel.Medium);
                recognizer.OnPhraseRecognized += OnPhraseRecognized;
                recognizer.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MainMenu] Riconoscimento vocale non avviato (microfono o pacchetto " +
                    "vocale ITALIANO di Windows?). Restano i tasti numerici. Dettaglio: " + e.Message);
                recognizer = null;
            }
        }

        private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            if (!voiceEnabled || loading) return;
            if (actions.TryGetValue(args.text, out var action)) action.Invoke();
        }

        private void ToggleVoice()
        {
            voiceEnabled = !voiceEnabled;
            if (recognizer == null) return;
            if (voiceEnabled && !recognizer.IsRunning) recognizer.Start();
            else if (!voiceEnabled && recognizer.IsRunning) recognizer.Stop();
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
    }
}
