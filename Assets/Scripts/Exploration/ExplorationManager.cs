using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using WeArt.Core;
using HapticResearch.Audio;
using HapticResearch.Experiment;
using HapticResearch.Labyrinth;
using HapticResearch.Levels;
using HapticResearch.UI;
using HapticResearch.Voice;

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

        [Header("Canale termico")]
        [Tooltip("Se vuoto viene aggiunto a questo GameObject.")]
        [SerializeField] private ThermalObjectCue thermalCue;

        [Header("Oggetti in scena")]
        [Tooltip("Se vuota si riempie da sola con tutti i SceneObjectBinding della scena.")]
        [SerializeField] private List<SceneObjectBinding> bindings = new List<SceneObjectBinding>();

        [Header("Rilevamento (piano del tavolo)")]
        [Tooltip("Distanza dito-oggetto sotto la quale si considera un contatto. Stesso ordine di grandezza del touchDistance dei segmenti di dito.")]
        [SerializeField] private float enterRadius = 0.03f;

        [Tooltip("Piu' larga di enterRadius: il contatto si perde solo uscendo davvero. Senza questa isteresi il dito sul bordo entrerebbe e uscirebbe decine di volte al secondo.")]
        [SerializeField] private float exitRadius = 0.045f;

        [Tooltip("Sopra questa altezza la mano e' sollevata dal tavolo e non tocca niente.")]
        [SerializeField] private float maxTipHeight = 0.97f;

        [Tooltip("Quanto il dito deve restare SULL'OGGETTO prima che la voce lo nomini. Non immobilita': seguire il bordo non azzera il conteggio.")]
        [SerializeField] private float nameDwellSeconds = 1.0f;

        [Tooltip("Un oggetto gia' nominato non si ripete prima di questo tempo.")]
        [SerializeField] private float nameCooldownSeconds = 15f;

        [Header("Suoni")]
        [Tooltip("Colpetto di contatto: si riusa quello del Level 2 (Assets/Audio/Level2/wall_bump).")]
        [SerializeField] private AudioClip contactClip;

        [Tooltip("Campanella di scoperta: si riusa quella del Level 2 (checkpoint_chime).")]
        [SerializeField] private AudioClip discoveryClip;

        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Richieste facoltative")]
        [SerializeField] private ExplorationSuggestions suggestions;
        [Tooltip("Tono della richiesta: Assets/Audio/Level3/suggestion_tone.")]
        [SerializeField] private AudioClip suggestionToneClip;

        [Header("Controlli operatore")]
        [SerializeField] private KeyCode startKey = KeyCode.Return;
        [SerializeField] private KeyCode repeatKey = KeyCode.R;

        [Tooltip("Chiude il livello. La sandbox non finisce da sola: la chiude il partecipante a voce o l'operatore da qui.")]
        [SerializeField] private KeyCode finishKey = KeyCode.End;

        [SerializeField] private bool autoStart = false;

        [Header("Comandi vocali del partecipante")]
        [Tooltip("Se vuoto lo cerca in scena.")]
        [SerializeField] private VoiceCommandController voiceCommands;

        [SerializeField] private string[] whatIsThisPhrases = { "cos'e' questo", "che cos'e'", "che cosa e'", "cos'e'" };
        [SerializeField] private string[] whatIsMissingPhrases = { "cosa manca", "che cosa manca", "quanto manca" };
        [SerializeField] private string[] finishPhrases = { "ho finito", "basta cosi'", "ho fatto" };

        [Header("Traccia del dito")]
        [Tooltip("Registra la posizione del dito nel CSV: in una sandbox senza accuratezza da misurare, la mappa di come si esplora il tavolo E' il risultato.")]
        [SerializeField] private bool logProbeTrace = true;

        [SerializeField, Range(1f, 30f)] private float probeHz = 10f;

        [Header("Logging")]
        [SerializeField] private SessionLogger sessionLogger;

        private State state = State.Idle;
        private float levelStartTime = -1f, levelEndTime = -1f;

        private readonly HashSet<string> discovered = new HashSet<string>();
        private AudioSource sfxSource;

        private FingerProbeSource probes;
        private SceneObjectBinding touched;
        private float touchedSince;
        private bool namedThisVisit;
        private readonly Dictionary<string, float> lastNamed = new Dictionary<string, float>();
        private float probeAccumulator;

        public TableSceneAsset Scene => scene;
        public HapticProfile Profile => profile;
        public int DiscoveredCount => discovered.Count;
        public int TotalDiscoverable => scene != null ? scene.DiscoverableCount : 0;

        // Etichetta dell'oggetto sotto il dito, per l'HUD. Riempita dal Task 8.
        public string TouchedLabel { get; protected set; }

        public SceneObjectBinding TouchedBinding => touched;

        // Il dito e' entrato su un oggetto diverso / la voce lo ha nominato.
        public event Action<SceneObjectBinding> OnTouchChanged;
        public event Action<SceneObjectBinding> OnObjectNamed;

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

            probes = new FingerProbeSource();
            // La spec dice "una mano sola, lato deciso in HapticProfile": senza questo
            // filtro il rilevamento guarda le punte di ENTRAMBE le mani e vince il minimo
            // globale, quindi una mano appoggiata sulla tovaglietta terrebbe il contatto
            // incollato li' e il canale termico armato a vita.
            probes.SideFilter =
                (profile.ActuatesLeft ? HandSideFlags.Left : HandSideFlags.None) |
                (profile.ActuatesRight ? HandSideFlags.Right : HandSideFlags.None);

            if (thermalCue == null) thermalCue = GetComponent<ThermalObjectCue>();
            if (thermalCue == null) thermalCue = gameObject.AddComponent<ThermalObjectCue>();
            thermalCue.Configure(profile);
            thermalCue.OnArmed += HandleThermalArmed;
            thermalCue.OnDelivered += HandleThermalDelivered;
            thermalCue.OnReleased += HandleThermalReleased;
            thermalCue.OnSuppressed += HandleThermalSuppressed;

            if (suggestions == null) suggestions = GetComponent<ExplorationSuggestions>();
            if (suggestions == null) suggestions = gameObject.AddComponent<ExplorationSuggestions>();
            suggestions.Configure(this, scene, suggestionToneClip);

            if (bindings.Count == 0)
                bindings.AddRange(FindObjectsByType<SceneObjectBinding>(FindObjectsSortMode.None));
            bindings.RemoveAll(b => b == null);

            if (voiceCommands == null)
                voiceCommands = FindFirstObjectByType<VoiceCommandController>(FindObjectsInactive.Include);
            if (voiceCommands != null)
            {
                voiceCommands.RegisterCommand(whatIsThisPhrases, SayWhatIsTouched);
                voiceCommands.RegisterCommand(whatIsMissingPhrases, SayWhatIsMissing);
                voiceCommands.RegisterCommand(finishPhrases, FinishByVoice);
            }
        }

        protected virtual void Start()
        {
            if (autoStart) StartLevel();
        }

        public override void StartLevel()
        {
            // Riporta il canale termico a neutro se il gesto "nuovo partecipante" (Invio) lo
            // chiama mentre un oggetto era ancora armato. Nessuno in sala potrebbe notare che
            // il guanto e' ancora caldo: il livello a schermo e' ripartito pulito, ma l'attuatore
            // no. Questa riga garantisce che ogni partecipante riceva un guanto freddo.
            thermalCue?.ResetChannel();

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
            probes.Invalidate();
            touched = null;
            namedThisVisit = false;
            lastNamed.Clear();
            suggestions.ResetAll();
            TouchedLabel = null;
            probeAccumulator = 0f;
            state = State.Exploring;
            levelStartTime = Time.time;
            levelEndTime = -1f;

            Voice("level3_intro");
            // Il profilo finisce nel log per nome E per valori: il Level 3 eredita la
            // taratura termica fatta nel Level 2, e a distanza di mesi deve restare
            // scritto che era la stessa, non una rifatta o quella di default.
            Log("level_start",
                $"{{\"scene\":\"{scene.SceneId}\",\"objects\":{bound},\"discoverable\":{TotalDiscoverable}," +
                $"\"hand\":\"{profile.ActuatedHandName()}\",\"profile\":\"{ProfileName()}\"," +
                $"\"warm\":{F(profile.WarmValue)},\"cold\":{F(profile.ColdValue)}," +
                $"\"neutral\":{F(profile.NeutralValue)}}}");
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
            suggestions.DropActiveOnFinish();   // prima di level_end: l'invito ancora vivo ha un esito
            Voice("level3_end_hint");
            Log("level_end",
                $"{{\"discovered\":{discovered.Count},\"total\":{TotalDiscoverable}," +
                $"\"seconds\":{F(ElapsedSeconds)},\"reason\":\"{reason}\"}}");
        }

        // Riempito dal Task 9: alla chiusura il canale termico torna neutro.
        private void OnFinished()
        {
            thermalCue?.ResetChannel();
        }

        private void HandleThermalArmed(string id, ThermalRole role)
        {
            Log("thermal_armed", $"{{\"id\":\"{id}\",\"role\":\"{role.ToString().ToLowerInvariant()}\"}}");
        }

        // "e' calda" arriva SOLO quando la temperatura e' stata davvero erogata: se il
        // Peltier non ce l'ha fatta, il partecipante non deve sentirsi affermare una
        // sensazione che non sta provando.
        private void HandleThermalDelivered(string id, ThermalRole role)
        {
            // Si ACCODA, non interrompe: il canale si arma all'ingresso e la battuta
            // arriva 3 s dopo, cioe' quasi sempre mentre l'introduzione sta ancora
            // parlando. Troncarla la' significa tagliare l'ultima frase, "quando vuoi
            // smettere, di': ho finito", che e' l'unico modo che il partecipante ha di
            // chiudere il livello.
            VoiceQueued(role == ThermalRole.Warm ? "level3_warm" : "level3_cool");
            Log("thermal_delivered", $"{{\"id\":\"{id}\",\"role\":\"{role.ToString().ToLowerInvariant()}\"}}");
        }

        private void HandleThermalReleased(string id, float held, bool delivered)
        {
            Log("thermal_released",
                $"{{\"id\":\"{id}\",\"seconds\":{F(held)},\"delivered\":{(delivered ? "true" : "false")}}}");
        }

        // Il caso "hai toccato il cucchiaino un secondo dopo la tazza e non hai sentito
        // niente". Senza questa riga, in analisi resta un partecipante che sembra non aver
        // percepito il freddo, e non si sapra' mai che il freddo non gli e' stato mandato.
        private void HandleThermalSuppressed(string id, string reason)
        {
            Log("thermal_suppressed", $"{{\"id\":\"{id}\",\"reason\":\"{reason}\"}}");
        }

        protected virtual void Update()
        {
            if (Input.GetKeyDown(startKey) && state != State.Exploring) StartLevel();
            if (Input.GetKeyDown(repeatKey)) RepeatAnnouncement();
            if (Input.GetKeyDown(finishKey)) Finish("operatore");

            if (state == State.Exploring)
                UpdateTouch(Time.deltaTime);
        }

        protected bool MarkDiscovered(string id)
        {
            if (!discovered.Add(id)) return false;
            PlaySfx(discoveryClip);
            Log("discovery",
                $"{{\"id\":\"{id}\",\"order\":{discovered.Count},\"seconds\":{F(ElapsedSeconds)}}}");
            if (discovered.Count >= TotalDiscoverable) VoiceQueued("level3_all_found");
            suggestions.NotifyDiscovery();
            return true;
        }

        public bool IsDiscovered(string id) => discovered.Contains(id);

        // Un profilo creato a runtime (nessun asset assegnato) non ha nome: nel log deve
        // vedersi che erano i valori di default, non un asset tarato.
        private string ProfileName() =>
            profile == null || string.IsNullOrEmpty(profile.name) ? "default" : profile.name;

        protected void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlaySuggestionTone(AudioClip clip) => PlaySfx(clip);

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

        private void UpdateTouch(float dt)
        {
            probes.Refresh();
            var hit = ResolveTouched();

            thermalCue.SetTarget(hit != null ? hit.Id : null,
                                 hit != null ? hit.Role : ThermalRole.Neutral);
            thermalCue.Tick(dt);
            suggestions.Tick(dt);

            // La traccia si scrive QUI, prima di qualunque uscita anticipata: gira a ogni
            // frame del livello, anche quando il dito non tocca niente. Quei tratti sono
            // meta' della mappa (dove la mano ha cercato senza trovare) e in una sandbox
            // senza accuratezza da misurare la mappa E' il risultato.
            LogProbeTrace(dt, hit);

            if (hit != touched)
            {
                if (touched != null)
                    Log("object_exit",
                        $"{{\"id\":\"{touched.Id}\",\"seconds\":{F(Time.time - touchedSince)}}}");

                touched = hit;
                touchedSince = Time.time;
                namedThisVisit = false;
                TouchedLabel = hit != null ? hit.Label : null;

                if (hit != null)
                {
                    PlaySfx(contactClip);
                    Log("object_enter", $"{{\"id\":\"{hit.Id}\"}}");
                }
                OnTouchChanged?.Invoke(hit);
            }

            if (touched == null) return;
            if (Time.time - touchedSince < nameDwellSeconds) return;

            // La richiesta si soddisfa alla SOSTA, non alla nominazione. Nel percorso di
            // sotto ci sono il cooldown dei nomi e il controllo sulla narrazione: un
            // oggetto nominato da meno di nameCooldownSeconds non ci arriverebbe mai, ed e'
            // il caso PIU' probabile, perche' le richieste partono dopo lo stallo o ogni
            // tre scoperte, cioe' quando gli oggetti sono gia' stati toccati. Il
            // partecipante faceva esattamente quel che gli era stato chiesto e non
            // succedeva niente. Lo scheduler ignora da se' le chiamate ripetute.
            suggestions.NotifyTouched(touched);

            if (namedThisVisit) return;

            // Il nome NON si accoda: se la voce sta parlando si riprova al frame dopo, e
            // se intanto il dito se n'e' andato non lo si dice affatto. Una voce che
            // insegue il dito con due secondi di ritardo, nominando cose che non si stanno
            // piu' toccando, per chi non vede e' peggio del silenzio.
            var nm = NarrationManager.Instance;
            if (nm != null && nm.IsSpeaking) return;

            if (lastNamed.TryGetValue(touched.Id, out float last) &&
                Time.time - last < nameCooldownSeconds)
            {
                namedThisVisit = true;   // gia' nominato da poco: si resta zitti fino alla prossima visita
                return;
            }

            namedThisVisit = true;
            lastNamed[touched.Id] = Time.time;
            Voice(touched.Entry.VoiceKey);
            Log("object_named", $"{{\"id\":\"{touched.Id}\"}}");

            if (touched.Entry.Discoverable) MarkDiscovered(touched.Id);
            OnObjectNamed?.Invoke(touched);
        }

        // Campiona la posizione della punta a frequenza fissa. `hit` e' l'oggetto sotto il
        // dito in QUESTO frame (`touched` sarebbe ancora quello del frame precedente):
        // quando e' null la riga resta, con id vuoto, ed e' esattamente il dato che dice
        // dove il partecipante ha cercato a vuoto.
        private void LogProbeTrace(float dt, SceneObjectBinding hit)
        {
            if (!logProbeTrace || probeHz <= 0f) return;
            probeAccumulator += dt;
            float period = 1f / probeHz;
            if (probeAccumulator < period) return;
            // Sottrai il periodo invece di azzerare: così il residuo rimane nell'accumulatore
            // e la frequenza media rimane nominale a qualunque frame rate. Con azzeramento,
            // la frequenza effettiva variava (4% in meno a 144 fps rispetto a 60 fps).
            probeAccumulator -= period;
            if (!probes.TryLowestTip(out var tip)) return;
            Log("probe",
                $"{{\"x\":{F(tip.x, "0.000")},\"z\":{F(tip.z, "0.000")}," +
                $"\"id\":\"{(hit != null ? hit.Id : "")}\"}}");
        }

        // L'oggetto piu' vicino alla punta dell'indice, con isteresi fra entrata e uscita.
        // Nel labirinto la stessa domanda ha una risposta aritmetica (MazeMap.Locate); qui
        // la geometria e' disegnata a mano e si misura la distanza dai collider. Con sette
        // oggetti il ciclo costa meno di una query di physics, e funziona identico con le
        // mani demo e con i tracker.
        private SceneObjectBinding ResolveTouched()
        {
            SceneObjectBinding best = null;
            float bestDistance = float.PositiveInfinity;
            float currentDistance = float.PositiveInfinity;   // distanza dall'oggetto gia' toccato

            foreach (var b in bindings)
            {
                if (b == null || b.Collider == null || b.Entry == null) continue;
                foreach (var tip in probes.Tips)
                {
                    if (tip.y > maxTipHeight) continue;   // mano sollevata dal tavolo
                    float d = Vector3.Distance(b.Collider.ClosestPoint(tip), tip);
                    if (b == touched && d < currentDistance) currentDistance = d;
                    if (d >= bestDistance) continue;
                    bestDistance = d;
                    best = b;
                }
            }

            // L'isteresi si applica all'oggetto che si sta GIA' toccando, anche quando non
            // e' il piu' vicino: tazza e piattino sono concentrici per progetto, e sul bordo
            // il minimo alterna fra i due. Confrontando solo il vincitore, la soglia
            // ricadeva ogni volta su enterRadius e partivano object_exit/object_enter a
            // raffica, con il colpetto sonoro a ogni giro - che il partecipante sente.
            if (touched != null && currentDistance <= exitRadius) return touched;

            if (best == null) return null;
            return bestDistance <= enterRadius ? best : null;
        }

        // I controller vocali segnalano la frase DOPO che l'azione ha deciso: cosi' i
        // sottotitoli dell'operatore dicono anche che effetto ha avuto.
        private void SayWhatIsTouched()
        {
            if (state != State.Exploring) return;

            if (touched == null || touched.Entry == null)
            {
                Voice("level3_nothing_here");
                VoiceSubtitles.ReportHeard("cos'e' questo", "alta", true, "niente sotto il dito");
                Log("voice_query", "{\"query\":\"what_is_this\",\"id\":\"\"}");
                return;
            }

            Voice(touched.Entry.VoiceKey);
            lastNamed[touched.Id] = Time.time;
            if (touched.Entry.Discoverable) MarkDiscovered(touched.Id);
            suggestions.NotifyTouched(touched);   // chiedere "cos'e' questo" vale come sosta
            VoiceSubtitles.ReportHeard("cos'e' questo", "alta", true, touched.Label);
            Log("voice_query", $"{{\"query\":\"what_is_this\",\"id\":\"{touched.Id}\"}}");
        }

        // Dice QUANTI ne mancano, mai QUALI: la scoperta resta al partecipante.
        private void SayWhatIsMissing()
        {
            if (state != State.Exploring) return;

            int missing = Mathf.Max(0, TotalDiscoverable - discovered.Count);
            string key = $"level3_missing_{Mathf.Min(missing, 6)}";
            Voice(key);
            VoiceSubtitles.ReportHeard("cosa manca", "alta", true, $"ne mancano {missing}");
            Log("voice_query", $"{{\"query\":\"what_is_missing\",\"missing\":{missing}}}");
        }

        private void FinishByVoice()
        {
            if (state != State.Exploring) return;
            VoiceSubtitles.ReportHeard("ho finito", "alta", true, "chiude il livello");
            Finish("voce");
        }
    }
}
