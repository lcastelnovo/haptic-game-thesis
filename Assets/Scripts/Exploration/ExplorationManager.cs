using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using HapticResearch.Audio;
using HapticResearch.Experiment;
using HapticResearch.Labyrinth;
using HapticResearch.Levels;
using HapticResearch.UI;

namespace HapticResearch.Exploration
{
    // Level 3: esplorazione libera di una colazione appoggiata sul tavolo.
    //
    // E' una SANDBOX: non si puo' sbagliare, non c'e' punteggio, non c'e' tempo limite e
    // la scoperta di tutti gli oggetti annuncia ma non chiude. Il livello finisce solo
    // quando il partecipante o l'operatore lo dicono.
    //
    // Il dato non e' l'accuratezza ma il percorso: in che ordine si tocca, dove si torna,
    // cosa si salta. Per questo il grosso del lavoro qui e' registrare, non giudicare.
    public class ExplorationManager : LevelController
    {
        private enum State { Idle, Exploring, Finished }

        [Header("Identita' (HUD operatore)")]
        [SerializeField] private int levelNumber = 3;
        [SerializeField] private string levelTitle = "Colazione";
        [SerializeField] private string levelId = "level3_breakfast";

        [Header("Dati")]
        [Tooltip("La colazione: oggetti, parametri tattili, suggerimenti.")]
        [SerializeField] private TableSceneAsset scene;

        [Tooltip("Taratura del partecipante. Se vuoto si usano i valori di default del profilo.")]
        [SerializeField] private HapticProfile profile;

        [Header("Oggetti in scena")]
        [Tooltip("Se vuota si riempie da sola con tutti i SceneObjectBinding della scena.")]
        [SerializeField] private List<SceneObjectBinding> bindings = new List<SceneObjectBinding>();

        [Header("Suoni")]
        [Tooltip("Colpetto di contatto: si riusa quello del Level 2 (Assets/Audio/Level2/wall_bump).")]
        [SerializeField] private AudioClip contactClip;

        [Tooltip("Campanella di scoperta: si riusa quella del Level 2 (checkpoint_chime).")]
        [SerializeField] private AudioClip discoveryClip;

        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Controlli operatore")]
        [SerializeField] private KeyCode startKey = KeyCode.Return;
        [SerializeField] private KeyCode repeatKey = KeyCode.R;

        [Tooltip("Chiude il livello. La sandbox non finisce da sola: la chiude il partecipante a voce o l'operatore da qui.")]
        [SerializeField] private KeyCode finishKey = KeyCode.End;

        [SerializeField] private bool autoStart = false;

        [Header("Logging")]
        [SerializeField] private SessionLogger sessionLogger;

        private State state = State.Idle;
        private float levelStartTime = -1f, levelEndTime = -1f;

        private readonly HashSet<string> discovered = new HashSet<string>();
        private AudioSource sfxSource;

        public TableSceneAsset Scene => scene;
        public HapticProfile Profile => profile;
        public int DiscoveredCount => discovered.Count;
        public int TotalDiscoverable => scene != null ? scene.DiscoverableCount : 0;

        // Etichetta dell'oggetto sotto il dito, per l'HUD. Riempita dal Task 8.
        public string TouchedLabel { get; protected set; }

        public override string LevelId => levelId;
        public override int LevelNumber => levelNumber;
        public override string LevelTitle => levelTitle;
        public override bool IsRunning => state == State.Exploring;
        public override bool IsComplete => state == State.Finished;

        public override string StatusLine
        {
            get
            {
                switch (state)
                {
                    case State.Idle: return "in attesa di avvio";
                    case State.Finished: return $"finita - {discovered.Count}/{TotalDiscoverable} scoperti";
                    default:
                        string touching = string.IsNullOrEmpty(TouchedLabel) ? "niente" : TouchedLabel;
                        return $"esplorazione libera - {discovered.Count}/{TotalDiscoverable} scoperti - sta toccando: {touching}";
                }
            }
        }

        public override float ElapsedSeconds
        {
            get
            {
                if (levelStartTime < 0f) return 0f;
                float end = levelEndTime > 0f ? levelEndTime : Time.time;
                return end - levelStartTime;
            }
        }

        protected virtual void Awake()
        {
            if (NarrationManager.Instance == null &&
                FindObjectsByType<NarrationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
                gameObject.AddComponent<NarrationManager>();

            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            if (profile == null) profile = ScriptableObject.CreateInstance<HapticProfile>();

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 0f;   // 2D: si sente sempre
            sfxSource.playOnAwake = false;

            if (bindings.Count == 0)
                bindings.AddRange(FindObjectsByType<SceneObjectBinding>(FindObjectsSortMode.None));
            bindings.RemoveAll(b => b == null);
        }

        protected virtual void Start()
        {
            if (autoStart) StartLevel();
        }

        public override void StartLevel()
        {
            if (scene == null)
            {
                Debug.LogError("[Level3] Manca la TableSceneAsset: impossibile avviare.");
                return;
            }
            if (!scene.Validate(out string error))
            {
                Debug.LogError($"[Level3] La scena '{scene.SceneId}' non e' valida: {error}");
                return;
            }

            int bound = 0;
            foreach (var b in bindings) if (b.Bind(scene)) bound++;
            if (bound == 0)
            {
                Debug.LogError("[Level3] Nessun oggetto legato all'asset: il tavolo sarebbe vuoto.");
                return;
            }

            discovered.Clear();
            TouchedLabel = null;
            state = State.Exploring;
            levelStartTime = Time.time;
            levelEndTime = -1f;

            Voice("level3_intro");
            Log("level_start",
                $"{{\"scene\":\"{scene.SceneId}\",\"objects\":{bound},\"discoverable\":{TotalDiscoverable}," +
                $"\"hand\":\"{profile.ActuatedHandName()}\"}}");
        }

        public override void RepeatAnnouncement()
        {
            switch (state)
            {
                case State.Idle: Voice("level3_intro"); break;
                case State.Exploring: Voice(discovered.Count >= TotalDiscoverable ? "level3_all_found" : "level3_intro"); break;
                case State.Finished: Voice("level3_end_hint"); break;
            }
        }

        // Chiusura esplicita: nessuno "vince", si decide di smettere.
        public void Finish(string reason)
        {
            if (state != State.Exploring) return;
            state = State.Finished;
            levelEndTime = Time.time;
            OnFinished();
            Voice("level3_end_hint");
            Log("level_end",
                $"{{\"discovered\":{discovered.Count},\"total\":{TotalDiscoverable}," +
                $"\"seconds\":{F(ElapsedSeconds)},\"reason\":\"{reason}\"}}");
        }

        // Riempito dal Task 9: alla chiusura il canale termico torna neutro.
        private void OnFinished() { }

        protected virtual void Update()
        {
            if (Input.GetKeyDown(startKey) && state != State.Exploring) StartLevel();
            if (Input.GetKeyDown(repeatKey)) RepeatAnnouncement();
            if (Input.GetKeyDown(finishKey)) Finish("operatore");
        }

        protected bool MarkDiscovered(string id)
        {
            if (!discovered.Add(id)) return false;
            PlaySfx(discoveryClip);
            Log("discovery",
                $"{{\"id\":\"{id}\",\"order\":{discovered.Count},\"seconds\":{F(ElapsedSeconds)}}}");
            if (discovered.Count >= TotalDiscoverable) VoiceQueued("level3_all_found");
            return true;
        }

        public bool IsDiscovered(string id) => discovered.Contains(id);

        protected void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // Battuta pre-generata per chiave. Se manca, lo dice invece di tacere: una voce
        // assente in un gioco per non vedenti e' un pezzo di interfaccia assente.
        public void Voice(string key)
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(key)) { nm.Speak(key); return; }
            Debug.LogWarning($"[Level3] Traccia vocale '{key}' mancante: genera le voci con Tools/generate_voice_macos.py.");
            VoiceSubtitles.ReportSaid(VoiceLines.TextOf(key) ?? $"[{key}]", 1.5f);
        }

        // Battute che NON devono interrompere quella in corso: arrivano mentre il nome
        // dell'oggetto e' ancora in bocca al narratore, e troncarlo lascerebbe il
        // partecipante senza sapere che cosa sta toccando.
        public void VoiceQueued(string key)
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(key)) { nm.SpeakQueued(key); return; }
            Debug.LogWarning($"[Level3] Traccia vocale '{key}' mancante: genera le voci con Tools/generate_voice_macos.py.");
            VoiceSubtitles.ReportSaid(VoiceLines.TextOf(key) ?? $"[{key}]", 1.5f);
        }

        protected static string F(float v, string format = "0.00") =>
            v.ToString(format, CultureInfo.InvariantCulture);

        public void Log(string eventType, string json)
        {
            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            sessionLogger?.Log(levelId, eventType, json);
        }
    }
}
