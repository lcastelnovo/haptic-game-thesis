using System.Collections.Generic;
using UnityEngine;
using HapticResearch.Experiment;
using HapticResearch.Haptics;
using HapticResearch.Audio;
using HapticResearch.UI;

namespace HapticResearch.Levels
{
    // Level 1 - Riconoscimento Forme.
    //
    // Flusso: un operatore vedente avvia il livello (startKey). Il sistema annuncia in
    // audio una forma scelta a caso; il giocatore non vedente esplora a tatto, AFFERRA la
    // forma che crede giusta e la TIENE per holdDuration secondi continui per confermare.
    //   - Giusta  -> suono positivo, round successivo.
    //   - Sbagliata -> suono negativo, stesso bersaglio (va rilasciata prima di riprovare).
    // Vinto quando tutte le forme sono state riconosciute (una a testa, ordine casuale).
    //
    // La forma "tenuta" è letta da HandGrabController/GloveGrabController.CurrentGrabbable,
    // così funziona sia col mouse (desktop) sia col guanto (VR), senza toccare quei file.
    public class ShapeRecognitionManager : LevelController
    {
        private enum State { Idle, AwaitingSelection, LevelComplete }

        [Header("Forme (configurabili da Inspector)")]
        [SerializeField] private List<ShapeDefinition> shapes = new List<ShapeDefinition>();

        [Header("Layout sul tavolo")]
        [Tooltip("Centro del tavolo dove disporre le forme. Se null usa questo transform. Tienilo a scala 1.")]
        [SerializeField] private Transform tableCenter;

        [Tooltip("Altezza Y del piano del tavolo (le forme nascono poco sopra e si appoggiano).")]
        [SerializeField] private float tableY = 0.86f;

        [Tooltip("Distanza tra le forme lungo la fila (m).")]
        [SerializeField] private float spacing = 0.18f;

        [Tooltip("Numero massimo di forme per fila prima di andare a capo.")]
        [SerializeField] private int perRow = 4;

        [Tooltip("Offset verticale di spawn: le forme cadono e si appoggiano sul tavolo.")]
        [SerializeField] private float spawnHeightOffset = 0.03f;

        [Header("Selezione (grab + hold)")]
        [Tooltip("Secondi di hold continuo necessari per confermare la scelta.")]
        [SerializeField] private float holdDuration = 5f;

        [Tooltip("Pausa dopo una conferma prima che un nuovo hold venga conteggiato.")]
        [SerializeField] private float selectionCooldown = 0.5f;

        [Header("Avvio / controlli operatore")]
        [Tooltip("Tasto con cui l'operatore vedente avvia il livello.")]
        [SerializeField] private KeyCode startKey = KeyCode.Return;

        [Tooltip("Tasto per ri-annunciare la forma bersaglio corrente.")]
        [SerializeField] private KeyCode repeatKey = KeyCode.R;

        [Tooltip("Avvia automaticamente allo Start (comodo in test).")]
        [SerializeField] private bool autoStart = false;

        [Header("Audio 2D (assegnare i clip)")]
        [SerializeField] private AudioClip levelStartClip;
        [SerializeField] private AudioClip correctClip;
        [SerializeField] private AudioClip wrongClip;
        [SerializeField] private AudioClip levelCompleteClip;

        [Tooltip("Tono riprodotto durante l'hold (feedback non visivo del conto). Usa pure i tone_*hz_5s.")]
        [SerializeField] private AudioClip holdLoopClip;

        [Tooltip("ON: il pitch del tono sale col progresso dell'hold. OFF: tono a frequenza fissa.")]
        [SerializeField] private bool rampHoldPitch = true;

        [Tooltip("Volume di voce/annunci e SFX.")]
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;

        [Header("Logging")]
        [Tooltip("Se null prova ad usare SessionLogger.Instance.")]
        [SerializeField] private SessionLogger sessionLogger;
        [SerializeField] private string levelId = "level1_shape_recognition";

        [Header("Identita' (HUD operatore)")]
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private string levelTitle = "Riconoscimento forme";

        // --- runtime ---
        private State state = State.Idle;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly Dictionary<IGrabbable, RecognizableShape> grabbableToShape = new();
        private readonly Dictionary<RecognizableShape, ShapeDefinition> shapeToDef = new();

        // Forme effettivamente preparate da SetupShapes: i round si costruiscono su queste,
        // così una def mal configurata (senza Scene Instance né Prefab) non diventa mai un
        // bersaglio impossibile da trovare sul tavolo.
        private readonly List<ShapeDefinition> activeShapes = new List<ShapeDefinition>();

        private HandGrabController[] handGrabControllers;
        private GloveGrabController[] gloveGrabControllers;
        private WeArtGraspBridge graspBridge; // presa delle mani WEART (VR / guanto reale)

        private AudioSource voiceSource; // annunci + SFX (one-shot)
        private AudioSource holdSource;  // loop durante l'hold

        private readonly List<int> roundOrder = new List<int>();
        private int roundIndex = -1;
        private ShapeDefinition currentTarget;

        private RecognizableShape heldShape;     // forma attualmente in conteggio
        private RecognizableShape ignoredShape;  // forma sbagliata da rilasciare prima di riprovare
        private string candidateSource;          // sorgente della presa candidata nell'ultimo scan
        private string heldSource;               // sorgente della presa in conteggio (per log/diagnosi)
        private float holdTimer;
        private float cooldownTimer;
        private int currentRoundErrors;
        private float roundStartTime;
        private float levelStartTime = -1f; // Time.time del via (-1 = mai partito)
        private float levelEndTime = -1f;   // Time.time del completamento (-1 = in corso)

        // --- stato pubblico (LevelController: HUD, voce, flusso) ---
        public override string LevelId => levelId;
        public override int LevelNumber => levelNumber;
        public override string LevelTitle => levelTitle;
        public override bool IsRunning => state == State.AwaitingSelection;
        public override bool IsComplete => state == State.LevelComplete;
        public string CurrentTargetId => currentTarget != null ? currentTarget.Id : "-";
        public int CurrentRound => Mathf.Clamp(roundIndex + 1, 0, roundOrder.Count);
        public int TotalRounds => roundOrder.Count;

        public override string StatusLine
        {
            get
            {
                if (IsComplete) return "completato";
                if (IsRunning) return $"round {CurrentRound}/{TotalRounds} · trova: {CurrentTargetId}";
                return "in attesa di avvio";
            }
        }

        public override float ElapsedSeconds
        {
            get
            {
                if (levelStartTime < 0f) return 0f;
                float end = levelEndTime >= 0f ? levelEndTime : Time.time;
                return end - levelStartTime;
            }
        }

        void Awake()
        {
            if (tableCenter == null) tableCenter = transform;

            // Sorgenti di grab (mouse + guanto) su tutte le mani, anche se inattive (hand switching).
            handGrabControllers = FindObjectsByType<HandGrabController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            gloveGrabControllers = FindObjectsByType<GloveGrabController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Bridge per la presa delle mani WEART (VR / guanto reale). Usa quello in scena
            // se presente, altrimenti lo crea: cosi' il livello funziona senza setup manuale.
            graspBridge = WeArtGraspBridge.Instance;
            if (graspBridge == null)
            {
                var found = FindObjectsByType<WeArtGraspBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                graspBridge = found.Length > 0 ? found[0] : gameObject.AddComponent<WeArtGraspBridge>();
            }

            // Narrazione vocale (istruzioni/annunci/feedback pre-generati con ElevenLabs).
            // Come per il bridge: usa quella in scena se presente, altrimenti la crea, cosi' il
            // livello parla senza setup manuale. Se le tracce non ci sono ancora, i metodi Voice()
            // ricadono sui clip assegnati da Inspector (comportamento storico, nessuna regressione).
            if (NarrationManager.Instance == null)
            {
                var foundNarr = FindObjectsByType<NarrationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (foundNarr.Length == 0) gameObject.AddComponent<NarrationManager>();
            }

            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;

            SetupAudioSources();
        }

        void Start()
        {
            SetupShapes();
            if (autoStart) StartLevel();
        }

        private void SetupAudioSources()
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.spatialBlend = 0f; // 2D: udibile sempre, indipendente dalla posizione
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            voiceSource.volume = voiceVolume;

            holdSource = gameObject.AddComponent<AudioSource>();
            holdSource.spatialBlend = 0f;
            holdSource.playOnAwake = false;
            holdSource.loop = true;
            holdSource.volume = voiceVolume;
            holdSource.clip = holdLoopClip;
        }

        // --- Spawn forme -------------------------------------------------------

        // Prepara le forme: usa quella GIÀ piazzata in scena (def.SceneInstance) se assegnata,
        // altrimenti istanzia il Prefab disponendolo sul tavolo.
        private void SetupShapes()
        {
            ClearSpawned();
            activeShapes.Clear();

            int count = shapes.Count;
            int spawnIndex = 0; // indice di layout per le sole forme istanziate
            for (int i = 0; i < count; i++)
            {
                var def = shapes[i];
                if (def == null) continue;

                GameObject go;
                bool instantiated = false;

                if (def.SceneInstance != null)
                {
                    // forma già piazzata: la usiamo dov'è, senza spostarla
                    go = def.SceneInstance;
                }
                else if (def.Prefab != null)
                {
                    go = Instantiate(def.Prefab);
                    go.transform.SetPositionAndRotation(ComputeSlotPosition(spawnIndex, count), def.Prefab.transform.rotation);
                    go.transform.SetParent(tableCenter, true); // worldPositionStays: niente eredità di scala
                    go.name = $"Shape_{def.Id}";
                    instantiated = true;
                    spawnIndex++;
                }
                else
                {
                    Debug.LogWarning($"[ShapeRecognition] Forma '{def.Id}' senza Scene Instance né Prefab: saltata.");
                    continue;
                }

                var rec = go.GetComponent<RecognizableShape>();
                if (rec == null) rec = go.AddComponent<RecognizableShape>();
                rec.SetShapeId(def.Id);
                rec.ResetVisual(); // al (ri)avvio riporta la forma al colore originale

                var grab = go.GetComponent<GrabbableObject>();
                if (grab != null)
                {
                    grab.SetHomeParent(instantiated ? tableCenter : go.transform.parent);
                    grabbableToShape[grab] = rec;
                }
                else
                {
                    Debug.LogWarning($"[ShapeRecognition] '{go.name}' non ha GrabbableObject sul root: non sarà afferrabile come unità.");
                }

                shapeToDef[rec] = def;
                activeShapes.Add(def);
                if (instantiated) spawned.Add(go); // solo le istanziate vanno distrutte al cleanup
            }
        }

        // Dispone le forme in una/più file centrate su tableCenter, sul piano del tavolo.
        private Vector3 ComputeSlotPosition(int index, int total)
        {
            int cols = Mathf.Clamp(perRow, 1, Mathf.Max(1, total));
            int row = index / cols;
            int col = index % cols;
            int colsInThisRow = Mathf.Min(cols, total - row * cols);
            int rowsTotal = Mathf.CeilToInt(total / (float)cols);

            float xStart = -(colsInThisRow - 1) * 0.5f * spacing;
            float zStart = -(rowsTotal - 1) * 0.5f * spacing;

            Vector3 center = tableCenter.position;
            float x = center.x + xStart + col * spacing;
            float z = center.z + zStart + row * spacing;
            float y = tableY + spawnHeightOffset;
            return new Vector3(x, y, z);
        }

        // --- Loop principale ---------------------------------------------------

        void Update()
        {
            if (state == State.Idle || state == State.LevelComplete)
            {
                // Da Idle: primo avvio. Da LevelComplete: ri-avvio per il partecipante successivo.
                if (Input.GetKeyDown(startKey)) StartLevel();
                return;
            }

            if (state != State.AwaitingSelection) return;

            if (Input.GetKeyDown(repeatKey)) RepeatAnnouncement();

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                return;
            }

            UpdateHold();
        }

        public override void StartLevel()
        {
            // Richiamabile in qualsiasi stato: l'operatore può avviare o riavviare/abortire.
            if (activeShapes.Count == 0)
            {
                Debug.LogWarning("[ShapeRecognition] Nessuna forma valida configurata: impossibile avviare.");
                return;
            }

            // Nuova partita: riporta le forme al colore base (blu) e rilascia eventuali prese.
            foreach (var rec in FindObjectsByType<RecognizableShape>(FindObjectsSortMode.None))
                rec.ResetVisual();
            if (graspBridge != null) graspBridge.Clear();

            BuildRoundOrder();
            roundIndex = -1;
            ignoredShape = null;
            levelStartTime = Time.time;
            levelEndTime = -1f;
            // Istruzioni parlate iniziali; il primo annuncio (in NextRound) viene ACCODATO cosi'
            // parte solo quando le istruzioni sono finite, senza sovrapporsi.
            Voice("instructions_intro", levelStartClip, false);
            Log("level_start", $"{{\"shapes\":{activeShapes.Count}}}");
            NextRound();
        }

        // Ordine casuale senza ripetizione (Fisher-Yates).
        private void BuildRoundOrder()
        {
            roundOrder.Clear();
            for (int i = 0; i < activeShapes.Count; i++) roundOrder.Add(i);
            for (int i = roundOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (roundOrder[i], roundOrder[j]) = (roundOrder[j], roundOrder[i]);
            }
        }

        private void NextRound()
        {
            roundIndex++;
            if (roundIndex >= roundOrder.Count)
            {
                LevelComplete();
                return;
            }

            currentTarget = activeShapes[roundOrder[roundIndex]];
            currentRoundErrors = 0;
            heldShape = null;
            holdTimer = 0f;
            cooldownTimer = selectionCooldown;
            roundStartTime = Time.time;
            state = State.AwaitingSelection;
            StopHoldAudio();

            AnnounceTarget(true); // accodato: segue istruzioni iniziali o feedback "giusto"
            Log("round_start", $"{{\"round\":{roundIndex + 1},\"target\":\"{currentTarget.Id}\"}}");
        }

        // Annuncia la forma bersaglio. queue=true accoda (dopo istruzioni/feedback), false
        // interrompe e annuncia subito. Usa la voce ElevenLabs "find_<id>" se generata,
        // altrimenti ricade sull'AnnounceClip da Inspector.
        private void AnnounceTarget(bool queue)
        {
            if (currentTarget == null) return;
            Voice($"find_{currentTarget.Id}", currentTarget.AnnounceClip, queue);
        }

        // L'operatore può ri-annunciare il bersaglio (repeatKey, bottone UI o comando vocale).
        public override void RepeatAnnouncement()
        {
            if (state == State.AwaitingSelection) AnnounceTarget(false); // subito, interrompe
        }

        private void UpdateHold()
        {
            ScanHeldShapes(out RecognizableShape candidate, out bool ignoredStillHeld);

            // La forma sbagliata è stata rilasciata da tutte le mani: si può riprovare.
            if (ignoredShape != null && !ignoredStillHeld) ignoredShape = null;

            if (candidate == null)
            {
                // niente di valido in mano (o solo la forma da rilasciare ancora stretta)
                ResetHold();
                return;
            }

            if (candidate != heldShape)
            {
                // nuova presa: riparte il timer di hold
                heldShape = candidate;
                heldSource = candidateSource;
                holdTimer = 0f;
                StartHoldAudio();
            }

            holdTimer += Time.deltaTime;
            UpdateHoldAudio(holdTimer / holdDuration);

            if (holdTimer >= holdDuration)
                Confirm(heldShape);
        }

        private void ResetHold()
        {
            if (heldShape != null || holdTimer > 0f)
            {
                heldShape = null;
                heldSource = null;
                holdTimer = 0f;
                StopHoldAudio();
            }
        }

        // Scansiona tutte le mani (mouse + guanto). Restituisce la prima forma tenuta che NON è
        // ignoredShape (candidate) e se ignoredShape è ancora tenuta in una qualsiasi mano.
        // Così una presa sbagliata in una mano non maschera quella giusta nell'altra (bimanuale).
        private void ScanHeldShapes(out RecognizableShape candidate, out bool ignoredStillHeld)
        {
            candidate = null;
            ignoredStillHeld = false;
            candidateSource = null;

            // Presa desktop (mouse) e guanto simulato, via i nostri controller.
            for (int i = 0; i < handGrabControllers.Length; i++)
                InspectHeld(handGrabControllers[i].CurrentGrabbable, "mouse", ref candidate, ref ignoredStillHeld);
            for (int i = 0; i < gloveGrabControllers.Length; i++)
                InspectHeld(gloveGrabControllers[i].CurrentGrabbable, "guanto_sim", ref candidate, ref ignoredStillHeld);

            // Presa delle mani WEART (VR / guanto reale): legge l'oggetto afferrato dal bridge.
            if (graspBridge != null)
            {
                InspectHeldObject(graspBridge.LeftGrasped, "weart_sx", ref candidate, ref ignoredStillHeld);
                InspectHeldObject(graspBridge.RightGrasped, "weart_dx", ref candidate, ref ignoredStillHeld);
            }
        }

        private void InspectHeld(IGrabbable grabbable, string source, ref RecognizableShape candidate, ref bool ignoredStillHeld)
        {
            if (grabbable == null || !grabbableToShape.TryGetValue(grabbable, out var rec)) return;
            if (rec == ignoredShape) ignoredStillHeld = true;
            else if (candidate == null)
            {
                candidate = rec;
                candidateSource = source;
            }
        }

        // Variante per la presa WEART: dall'oggetto fisico afferrato (il GameObject del
        // WeArtTouchableObject, anche una sotto-mesh) risale alla forma con GetComponentInParent.
        private void InspectHeldObject(GameObject grasped, string source, ref RecognizableShape candidate, ref bool ignoredStillHeld)
        {
            if (grasped == null) return;
            var rec = grasped.GetComponentInParent<RecognizableShape>();
            if (rec == null || !shapeToDef.ContainsKey(rec)) return;
            if (rec == ignoredShape) ignoredStillHeld = true;
            else if (candidate == null)
            {
                candidate = rec;
                candidateSource = source;
            }
        }

        private void Confirm(RecognizableShape shape)
        {
            shapeToDef.TryGetValue(shape, out var def);
            bool correct = def != null && currentTarget != null && def.Id == currentTarget.Id;
            float elapsed = Time.time - roundStartTime;
            string source = heldSource ?? "-"; // da salvare prima del reset di heldShape/heldSource

            StopHoldAudio();
            holdTimer = 0f;
            cooldownTimer = selectionCooldown;
            ignoredShape = shape; // va rilasciata prima che un nuovo hold conti
            heldShape = null;
            heldSource = null;

            // Dopo OGNI conferma azzera le prese del bridge WEART: serve un nuovo gesto
            // apri->chiudi per riprendere una forma. Senza questo, una presa rimasta appesa
            // (es. l'altra mano ferma su una forma con le dita chiuse, che non rilascia mai)
            // riconferma la stessa forma all'infinito; e la forma indovinata ancora in mano
            // verrebbe conteggiata come risposta (sbagliata) del round successivo.
            if (graspBridge != null) graspBridge.Clear();

            if (correct)
            {
                shape.MarkSolved(); // feedback visivo: la forma indovinata diventa verde
                Voice("answer_correct", correctClip, true); // "esatto" -> poi il prossimo annuncio accodato
                Log("answer_correct",
                    $"{{\"round\":{roundIndex + 1},\"target\":\"{currentTarget.Id}\",\"errors\":{currentRoundErrors},\"timeSec\":{elapsed:0.00},\"source\":\"{source}\"}}");
                NextRound();
            }
            else
            {
                currentRoundErrors++;
                // La sorgente in chiaro anche in Console: se una presa "fantasma" (es. un
                // controller sbagliato) maschera quella del guanto, l'operatore lo vede subito.
                Debug.LogWarning($"[ShapeRecognition] SBAGLIATO: tenuta '{def?.Id}' (oggetto '{shape.name}', sorgente {source}), bersaglio '{currentTarget.Id}'");
                Voice("answer_wrong", wrongClip, false); // subito; stesso bersaglio, niente ri-annuncio
                Log("answer_wrong",
                    $"{{\"round\":{roundIndex + 1},\"target\":\"{currentTarget.Id}\",\"chosen\":\"{def?.Id}\",\"errors\":{currentRoundErrors},\"source\":\"{source}\"}}");
                // stesso bersaglio: si resta in AwaitingSelection
            }
        }

        private void LevelComplete()
        {
            state = State.LevelComplete;
            levelEndTime = Time.time;
            currentTarget = null;
            StopHoldAudio();
            Voice("level_complete", levelCompleteClip, true); // dopo l'ultimo "esatto" accodato
            Log("level_complete", $"{{\"rounds\":{roundOrder.Count}}}");
        }

        // --- Audio -------------------------------------------------------------

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null || voiceSource == null) return;
            voiceSource.PlayOneShot(clip);
        }

        // Riproduce una battuta: se il NarrationManager ha la traccia pre-generata con quella
        // chiave usa la voce ElevenLabs, altrimenti ricade sul clip da Inspector (fallback
        // storico). queue=true accoda senza interrompere; false interrompe e parla subito.
        private void Voice(string key, AudioClip fallback, bool queue)
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(key))
            {
                if (queue) nm.SpeakQueued(key);
                else nm.Speak(key);
                return;
            }
            // Nessuna traccia vocale: comportamento audio originale. Il testo va comunque
            // nei sottotitoli, cosi' l'operatore vede cosa e' stato chiesto.
            if (fallback != null) VoiceSubtitles.ReportSaid(VoiceLines.TextOf(key) ?? $"[{key}]", fallback.length);
            PlayOneShot(fallback);
        }

        private void StartHoldAudio()
        {
            if (holdSource == null || holdLoopClip == null) return;
            holdSource.clip = holdLoopClip;
            holdSource.pitch = 1f;
            if (!holdSource.isPlaying) holdSource.Play();
        }

        private void UpdateHoldAudio(float progress01)
        {
            if (holdSource == null || holdLoopClip == null) return;
            holdSource.pitch = rampHoldPitch ? Mathf.Lerp(1f, 2f, Mathf.Clamp01(progress01)) : 1f;
        }

        private void StopHoldAudio()
        {
            if (holdSource == null) return;
            holdSource.pitch = 1f;
            if (holdSource.isPlaying) holdSource.Stop();
        }

        // --- Util --------------------------------------------------------------

        private void Log(string eventType, string json)
        {
            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            sessionLogger?.Log(levelId, eventType, json);
        }

        private void ClearSpawned()
        {
            foreach (var go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
            grabbableToShape.Clear();
            shapeToDef.Clear();
        }
    }
}
