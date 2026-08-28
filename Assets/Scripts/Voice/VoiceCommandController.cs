using System;
using System.Collections.Generic;
using UnityEngine;
using HapticResearch.Levels;
using HapticResearch.Experiment;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace HapticResearch.Voice
{
    // Controllo vocale "di regia" del livello, a PAROLE CHIAVE in italiano.
    //
    // Serve l'accessibilita': il giocatore non vedente (o l'operatore) puo' avviare il
    // livello, riavviarlo per un nuovo partecipante e ri-annunciare la forma bersaglio
    // parlando, senza dover trovare/cliccare il pannello a schermo.
    //
    // Path INPUT-AGNOSTICO: la voce NON e' un percorso separato, chiama esattamente gli
    // stessi metodi del pannello operatore (ShapeRecognitionManager.StartLevel /
    // RepeatAnnouncement). Quindi si comporta identico con guanti veri, mani demo o mouse.
    //
    // La RISPOSTA del giocatore resta puramente tattile (afferra + tieni): la voce fa
    // solo regia, cosi' non altera cosa misura l'esperimento.
    //
    // Motore: riconoscimento vocale NATIVO di Windows (UnityEngine.Windows.Speech,
    // KeywordRecognizer). Gratis, offline, zero dipendenze. Funziona SOLO su Windows:
    // tutto il codice del recognizer e' isolato dietro #if UNITY_STANDALONE_WIN, cosi'
    // sul Mac compila ma resta inattivo (come SteamVR / middleware WEART). Sulla macchina
    // esperimento serve il pacchetto vocale ITALIANO installato in Windows.
    public class VoiceCommandController : MonoBehaviour
    {
        // Enum locale (non espone il tipo Windows-only ConfidenceLevel nell'Inspector).
        public enum VoiceConfidence { High, Medium, Low }

        [Header("Riferimenti")]
        [Tooltip("Se null, cerca uno ShapeRecognitionManager in scena all'avvio.")]
        [SerializeField] private ShapeRecognitionManager manager;

        [Header("Attivazione")]
        [Tooltip("Il microfono parte in ascolto all'avvio della scena.")]
        [SerializeField] private bool startEnabled = true;

        [Tooltip("Tasto per mutare/riattivare l'ascolto (comodo per l'operatore). None per disabilitarlo.")]
        [SerializeField] private KeyCode muteKey = KeyCode.M;

        [Header("Riconoscimento")]
        [Tooltip("Confidenza minima: High = accetta solo pronunce nette, Low = piu' permissivo.")]
        [SerializeField] private VoiceConfidence minimumConfidence = VoiceConfidence.Medium;

        [Header("Vocabolario (le frasi base italiane sono gia' incluse; qui aggiungi sinonimi)")]
        [Tooltip("Frasi extra che AVVIANO/riavviano il livello (nuovo partecipante).")]
        [SerializeField] private string[] extraStartPhrases;

        [Tooltip("Frasi extra che RI-ANNUNCIANO la forma bersaglio corrente.")]
        [SerializeField] private string[] extraRepeatPhrases;

        [Header("Feedback")]
        [Tooltip("Suono opzionale 'comando ricevuto' (conferma non visiva, accessibilita').")]
        [SerializeField] private AudioClip commandAckClip;

        [Tooltip("Mostra a schermo un piccolo indicatore stato microfono (per l'operatore).")]
        [SerializeField] private bool showStatus = true;

        [Header("Logging")]
        [Tooltip("Id livello per il SessionLogger (deve combaciare con lo ShapeRecognitionManager).")]
        [SerializeField] private string levelId = "level1_shape_recognition";

        // Frasi base sempre attive (italiano). Le "extra" dell'Inspector si sommano a queste.
        // "Avvia" e "Nuovo partecipante" chiamano lo stesso metodo (StartLevel gestisce
        // avvio, riavvio e nuovo partecipante da qualunque stato).
        private static readonly string[] BaseStartPhrases =
        {
            "avvia", "avvia livello", "inizia", "inizia livello", "parti", "via",
            "nuovo partecipante", "prossimo partecipante", "riavvia", "riavvia livello", "ricomincia"
        };
        private static readonly string[] BaseRepeatPhrases =
        {
            "ripeti", "ripeti annuncio", "ripeti la forma", "quale forma", "che forma", "di nuovo"
        };

        private AudioSource ackSource;
        private SessionLogger sessionLogger;
        private bool voiceEnabled;
        private string lastRecognized = "-";

        // Stile indicatore (creato lazy in OnGUI).
        private bool styleReady;
        private GUIStyle statusStyle;
        private Texture2D statusBg;

        void Awake()
        {
            if (manager == null) manager = FindFirstObjectByType<ShapeRecognitionManager>();
            sessionLogger = SessionLogger.Instance;

            // AudioSource 2D dedicato: la conferma "comando ricevuto" si sente sempre.
            ackSource = gameObject.AddComponent<AudioSource>();
            ackSource.spatialBlend = 0f;
            ackSource.playOnAwake = false;
            ackSource.loop = false;

            voiceEnabled = startEnabled;
        }

        void Start()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            BuildVocabularyAndStart();
#else
            Debug.Log("[VoiceCommand] Riconoscimento vocale disponibile solo su Windows: inattivo su questa piattaforma.");
#endif
        }

        void Update()
        {
            if (muteKey != KeyCode.None && Input.GetKeyDown(muteKey))
                SetVoiceEnabled(!voiceEnabled);
        }

        // Muta / riattiva l'ascolto. Pubblico: richiamabile anche da UI o altri script.
        public void SetVoiceEnabled(bool enabled)
        {
            voiceEnabled = enabled;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer == null) return;
            if (enabled && !recognizer.IsRunning) recognizer.Start();
            else if (!enabled && recognizer.IsRunning) recognizer.Stop();
#endif
        }

        public bool VoiceEnabled => voiceEnabled;

        // --- Riconoscimento (SOLO Windows) --------------------------------------------

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer recognizer;
        // Dizionario frase -> azione, case-insensitive (la pronuncia riconosciuta e' una delle chiavi).
        private readonly Dictionary<string, Action> actions =
            new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        private void BuildVocabularyAndStart()
        {
            if (manager == null)
            {
                Debug.LogWarning("[VoiceCommand] Nessun ShapeRecognitionManager in scena: comandi vocali disattivati.");
                return;
            }

            actions.Clear();
            RegisterPhrases(BaseStartPhrases, () => manager.StartLevel());
            RegisterPhrases(extraStartPhrases, () => manager.StartLevel());
            RegisterPhrases(BaseRepeatPhrases, () => manager.RepeatAnnouncement());
            RegisterPhrases(extraRepeatPhrases, () => manager.RepeatAnnouncement());

            if (actions.Count == 0) return;

            var keywords = new string[actions.Count];
            actions.Keys.CopyTo(keywords, 0);

            try
            {
                recognizer = new KeywordRecognizer(keywords, ToConfidence(minimumConfidence));
                recognizer.OnPhraseRecognized += OnPhraseRecognized;
                if (voiceEnabled) recognizer.Start();
            }
            catch (Exception e)
            {
                // Cause tipiche: microfono assente/negato, o pacchetto vocale ITALIANO non
                // installato in Windows (Impostazioni -> Lingua -> Italiano -> Voce).
                Debug.LogWarning("[VoiceCommand] Impossibile avviare il riconoscimento vocale. " +
                    "Verifica il microfono e il pacchetto vocale ITALIANO in Windows. Dettaglio: " + e.Message);
                recognizer = null;
            }
        }

        private void RegisterPhrases(string[] phrases, Action action)
        {
            if (phrases == null) return;
            foreach (var p in phrases)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                actions[p.Trim()] = action; // eventuali doppioni si sovrascrivono, nessun crash
            }
        }

        private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            if (!voiceEnabled) return;
            if (!actions.TryGetValue(args.text, out var action)) return;

            lastRecognized = args.text;
            if (commandAckClip != null) ackSource.PlayOneShot(commandAckClip);
            sessionLogger?.Log(levelId, "voice_command",
                "{\"phrase\":\"" + args.text + "\",\"confidence\":\"" + args.confidence + "\"}");
            action.Invoke();
        }

        private static ConfidenceLevel ToConfidence(VoiceConfidence c)
        {
            switch (c)
            {
                case VoiceConfidence.High: return ConfidenceLevel.High;
                case VoiceConfidence.Low: return ConfidenceLevel.Low;
                default: return ConfidenceLevel.Medium;
            }
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

        // --- Indicatore a schermo (operatore) -----------------------------------------

        void OnGUI()
        {
            if (!showStatus) return;
            EnsureStyle();

            var rect = new Rect(Screen.width - 340f, Screen.height - 34f, 328f, 26f);
            GUI.Label(rect, "  " + StatusText(), statusStyle);
        }

        private string StatusText()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (recognizer == null) return "Voce IT: non disponibile (microfono / pacchetto voce?)";
            return voiceEnabled
                ? "Voce IT attiva  -  ultimo: " + lastRecognized + "   (M = muta)"
                : "Voce IT muta   (M = riattiva)";
#else
            return "Voce IT: attiva solo su Windows";
#endif
        }

        private void EnsureStyle()
        {
            if (styleReady) return;

            statusBg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            statusBg.SetPixel(0, 0, new Color32(2, 40, 78, 220)); // blu UniBS
            statusBg.Apply();

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white, background = statusBg },
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            styleReady = true;
        }
    }
}
