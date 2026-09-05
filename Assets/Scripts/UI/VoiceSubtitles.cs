using UnityEngine;
using UnityEngine.SceneManagement;
using HapticResearch.Audio;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace HapticResearch.UI
{
    // Sottotitoli a schermo per l'OPERATORE VEDENTE: due righe fisse.
    //   SENTO -> l'ultima frase riconosciuta dal microfono, con confidenza ed esito
    //            (eseguita, oppure ignorata e perche') e lo stato del riconoscimento
    //   DICO  -> il testo della battuta vocale che il gioco sta pronunciando
    //
    // Serve a chi conduce la sessione: vede se il comando e' arrivato e cosa sta sentendo
    // il partecipante senza guardare la Console. Il partecipante non vedente non ne ha
    // bisogno: tutto quello che c'e' qui e' gia' audio.
    //
    // Limite del motore: il riconoscimento e' a PAROLE CHIAVE (KeywordRecognizer di
    // Windows), quindi "sente" solo le frasi del vocabolario dei controller vocali. Una
    // frase fuori vocabolario non produce alcun evento e qui non compare.
    //
    // Si auto-installa a ogni scena (come SceneFader): niente da aggiungere nelle scene.
    // Per ritoccare i parametri da Inspector basta mettere il componente su un GameObject
    // qualsiasi della scena: l'auto-install lo trova e non crea doppioni. La posizione
    // (Placement) e' per scena: in Level1 in basso al centro, nel menu in alto a sinistra
    // perche' in basso ci sono i crediti (lo imposta MainMenuSceneController).
    public class VoiceSubtitles : MonoBehaviour
    {
        public static VoiceSubtitles Instance { get; private set; }

        public enum Placement { BottomCenter, TopLeft, TopRight, TopCenter }

        [Header("Visibilita'")]
        [Tooltip("Tasto per mostrare/nascondere i sottotitoli. None per disabilitarlo.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F2;

        [Tooltip("Sottotitoli visibili all'avvio.")]
        [SerializeField] private bool startVisible = true;

        [Header("Posizione")]
        [Tooltip("Dove sta il riquadro. I controller di scena possono cambiarlo a runtime (SetPlacement).")]
        [SerializeField] private Placement placement = Placement.BottomCenter;

        [Tooltip("Distanza dal bordo inferiore (solo BottomCenter): lascia spazio al bottone 'Vai al livello 2' e all'indicatore microfono.")]
        [SerializeField] private float bottomMargin = 84f;

        [Tooltip("Distanza dai bordi per le posizioni in alto.")]
        [SerializeField] private float cornerMargin = 14f;

        [Header("Aspetto")]
        [SerializeField] private int fontSize = 18;

        [Tooltip("Larghezza del riquadro come frazione dello schermo quando sta in basso al centro.")]
        [SerializeField, Range(0.3f, 0.9f)] private float widthFraction = 0.55f;

        [Tooltip("Larghezza come frazione dello schermo quando sta in un angolo (piu' stretto: non deve coprire il logo centrale del menu).")]
        [SerializeField, Range(0.2f, 0.9f)] private float cornerWidthFraction = 0.4f;

        [SerializeField] private float minWidth = 480f;
        [SerializeField] private float maxWidth = 1000f;

        [Header("Tempi")]
        [Tooltip("Secondi per cui l'ultima frase riconosciuta resta in evidenza.")]
        [SerializeField] private float heardHoldSeconds = 6f;

        [Tooltip("Secondi per cui l'ultima battuta resta leggibile dopo che la voce ha finito.")]
        [SerializeField] private float saidHoldSeconds = 4f;

        // Ultima frase riconosciuta: STATICA, cosi' sopravvive al cambio scena (es. "uno"
        // detto nel menu si legge ancora un attimo mentre parte il livello).
        private static string heardPhrase;
        private static string heardConfidence;
        private static bool heardExecuted;
        private static string heardNote;
        private static float heardAt = float.NegativeInfinity;

        // Battuta detta FUORI dal NarrationManager (clip di riserva da Inspector): testo e
        // scadenza. Anche questa statica, per lo stesso motivo.
        private static string saidOverrideText;
        private static float saidOverrideUntil = float.NegativeInfinity;

        private bool visible;
        private string lastSaidKey;
        private string lastSaidText;
        private bool speakingNow;
        private float saidEndedAt = float.NegativeInfinity;

        private bool styleReady;
        private GUIStyle lineStyle;
        private Texture2D bg;

        private const string TagColor = "#00A3E0"; // azzurro UniBS
        private const string DimColor = "#9AA4AE";
        private const string HoldColor = "#C8CED4";
        private const string OkColor = "#7CE38B";
        private const string WarnColor = "#FFB454";

        // --- Auto-install -------------------------------------------------------------

        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Senza domain reload gli static restano tra un Play e l'altro: l'iscrizione
            // va fatta una volta sola, ma lo stato va azzerato a ogni avvio.
            if (!subscribed)
            {
                subscribed = true;
                SceneManager.sceneLoaded += (_, _) => EnsureInstance();
            }
            ResetStaticState();
            EnsureInstance(); // anche per la primissima scena (sceneLoaded e' gia' passato)
        }

        private static void ResetStaticState()
        {
            heardPhrase = null;
            heardConfidence = null;
            heardExecuted = false;
            heardNote = null;
            heardAt = float.NegativeInfinity;
            saidOverrideText = null;
            saidOverrideUntil = float.NegativeInfinity;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            lastSpeechError = null;
#endif
        }

        private static void EnsureInstance()
        {
            if (Instance != null) return;
            // Se la scena ne ha gia' uno (anche disabilitato apposta), si rispetta quello.
            var existing = FindFirstObjectByType<VoiceSubtitles>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return;
            }
            new GameObject("VoiceSubtitles").AddComponent<VoiceSubtitles>();
        }

        // --- API per i controller -------------------------------------------------------

        // Chiamata dai controller vocali quando il recognizer riconosce una frase.
        // executed = il comando ha avuto effetto; note = perche' e' stato ignorato.
        public static void ReportHeard(string phrase, string confidence, bool executed, string note = null)
        {
            heardPhrase = phrase;
            heardConfidence = confidence;
            heardExecuted = executed;
            heardNote = note;
            heardAt = Time.unscaledTime;
        }

        // Battuta pronunciata senza passare dal NarrationManager (es. clip di riserva da
        // Inspector quando manca la traccia pre-generata): resta in riga DICO per 'seconds'.
        public static void ReportSaid(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            saidOverrideText = text;
            saidOverrideUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);
        }

        // Posizione del riquadro, scelta dal controller della scena (es. menu: TopLeft).
        public void SetPlacement(Placement value) => placement = value;

        // --- Ciclo di vita ------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Doppione: sparisce solo il componente, l'host resta intatto (potrebbe
                // ospitare altri script del livello).
                Debug.LogWarning($"[VoiceSubtitles] Doppione su '{gameObject.name}': rimosso, resta quello su '{Instance.gameObject.name}'.");
                Destroy(this);
                return;
            }
            Instance = this;
            visible = startVisible;
        }

        void OnEnable()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            PhraseRecognitionSystem.OnError += OnSpeechError;
#endif
        }

        void OnDisable()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            PhraseRecognitionSystem.OnError -= OnSpeechError;
#endif
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static string lastSpeechError;
        private static void OnSpeechError(SpeechError error) => lastSpeechError = error.ToString();
#endif

        void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) visible = !visible;

            // Segue la narrazione per polling: il NarrationManager e' per-scena, cosi' non
            // c'e' nessun evento da iscrivere/disiscrivere a ogni cambio scena.
            var nm = NarrationManager.Instance;
            string key = nm != null ? nm.CurrentKey : null;
            bool speaking = false;

            if (!string.IsNullOrEmpty(key))
            {
                if (key != lastSaidKey)
                {
                    lastSaidKey = key;
                    lastSaidText = VoiceLines.TextOf(key) ?? $"[{key}]";
                }
                speaking = true;
            }
            else if (Time.unscaledTime < saidOverrideUntil)
            {
                lastSaidKey = null;
                lastSaidText = saidOverrideText;
                speaking = true;
            }

            if (speaking) speakingNow = true;
            else if (speakingNow)
            {
                speakingNow = false;
                saidEndedAt = Time.unscaledTime;
            }
        }

        // --- Disegno ------------------------------------------------------------------

        void OnGUI()
        {
            if (!visible) return;
            EnsureStyle();

            bool corner = placement != Placement.BottomCenter;
            float fraction = corner ? cornerWidthFraction : widthFraction;
            float width = Mathf.Clamp(Screen.width * fraction, minWidth, Mathf.Min(maxWidth, Screen.width - 2f * cornerMargin));

            var content = new GUIContent(HeardLine() + "\n" + SaidLine());
            float height = lineStyle.CalcHeight(content, width);

            float x, y;
            switch (placement)
            {
                case Placement.TopLeft:
                    x = cornerMargin; y = cornerMargin; break;
                case Placement.TopRight:
                    x = Screen.width - width - cornerMargin; y = cornerMargin; break;
                case Placement.TopCenter:
                    x = (Screen.width - width) * 0.5f; y = cornerMargin; break;
                default:
                    x = (Screen.width - width) * 0.5f; y = Screen.height - bottomMargin - height; break;
            }
            GUI.Label(new Rect(x, y, width, height), content, lineStyle);
        }

        private string HeardLine()
        {
            string head = $"{Tag("SENTO")} <color={DimColor}>[{MicStatus()}]</color> ";
            float age = Time.unscaledTime - heardAt;
            if (heardPhrase == null || age > heardHoldSeconds)
                return head + $"<color={DimColor}>nessun comando recente</color>";

            string outcome = heardExecuted
                ? $"<color={OkColor}>eseguito</color>"
                : $"<color={WarnColor}>ignorato{(string.IsNullOrEmpty(heardNote) ? "" : ": " + heardNote)}</color>";
            return head + $"«{heardPhrase}»  ({ConfidenceIt(heardConfidence)})  {outcome}";
        }

        private string SaidLine()
        {
            string head = Tag("DICO") + " ";
            if (speakingNow) return head + lastSaidText;

            float age = Time.unscaledTime - saidEndedAt;
            if (lastSaidText != null && age <= saidHoldSeconds)
                return head + $"<color={HoldColor}>{lastSaidText}</color>";
            return head + $"<color={DimColor}>—</color>";
        }

        private static string Tag(string name) => $"<color={TagColor}>{name}</color>";

        private static string MicStatus()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            switch (PhraseRecognitionSystem.Status)
            {
                case SpeechSystemStatus.Running: return "in ascolto";
                case SpeechSystemStatus.Failed: return "errore" + (lastSpeechError != null ? " " + lastSpeechError : "");
                default: return "microfono fermo";
            }
#else
            return "voce solo su Windows";
#endif
        }

        private static string ConfidenceIt(string confidence)
        {
            switch (confidence)
            {
                case "High": return "alta";
                case "Medium": return "media";
                case "Low": return "bassa";
                case "Rejected": return "rifiutata";
                default: return string.IsNullOrEmpty(confidence) ? "n/d" : confidence;
            }
        }

        private void EnsureStyle()
        {
            if (styleReady) return;

            bg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.78f));
            bg.Apply();

            lineStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white, background = bg },
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 8, 8)
            };
            styleReady = true;
        }
    }
}
