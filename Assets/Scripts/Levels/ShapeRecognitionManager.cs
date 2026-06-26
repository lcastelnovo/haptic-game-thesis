using System.Collections.Generic;
using UnityEngine;
using HapticResearch.Experiment;
using HapticResearch.Haptics;

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
    public class ShapeRecognitionManager : MonoBehaviour
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

        // --- runtime ---
        private State state = State.Idle;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly Dictionary<IGrabbable, RecognizableShape> grabbableToShape = new();
        private readonly Dictionary<RecognizableShape, ShapeDefinition> shapeToDef = new();

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
        private float holdTimer;
        private float cooldownTimer;
        private int currentRoundErrors;
        private float roundStartTime;

        // --- stato pubblico (per il pannello operatore) ---
        public bool IsRunning => state == State.AwaitingSelection;
        public bool IsComplete => state == State.LevelComplete;
        public string CurrentTargetId => currentTarget != null ? currentTarget.Id : "-";
        public int CurrentRound => Mathf.Clamp(roundIndex + 1, 0, roundOrder.Count);
        public int TotalRounds => roundOrder.Count;

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

        public void StartLevel()
        {
            // Richiamabile in qualsiasi stato: l'operatore può avviare o riavviare/abortire.
            if (shapes.Count == 0)
            {
                Debug.LogWarning("[ShapeRecognition] Nessuna forma configurata: impossibile avviare.");
                return;
            }

            BuildRoundOrder();
            roundIndex = -1;
            ignoredShape = null;
            PlayOneShot(levelStartClip);
            Log("level_start", $"{{\"shapes\":{shapes.Count}}}");
            NextRound();
        }

        // Ordine casuale senza ripetizione (Fisher-Yates).
        private void BuildRoundOrder()
        {
            roundOrder.Clear();
            for (int i = 0; i < shapes.Count; i++) roundOrder.Add(i);
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

            currentTarget = shapes[roundOrder[roundIndex]];
            currentRoundErrors = 0;
            heldShape = null;
            holdTimer = 0f;
            cooldownTimer = selectionCooldown;
            roundStartTime = Time.time;
            state = State.AwaitingSelection;
            StopHoldAudio();

            AnnounceTarget();
            Log("round_start", $"{{\"round\":{roundIndex + 1},\"target\":\"{currentTarget.Id}\"}}");
        }

        private void AnnounceTarget()
        {
            if (currentTarget != null && currentTarget.AnnounceClip != null)
                PlayOneShot(currentTarget.AnnounceClip);
        }

        // L'operatore può ri-annunciare il bersaglio (repeatKey o bottone UI).
        public void RepeatAnnouncement()
        {
            if (state == State.AwaitingSelection) AnnounceTarget();
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

            // Presa desktop (mouse) e guanto simulato, via i nostri controller.
            for (int i = 0; i < handGrabControllers.Length; i++)
                InspectHeld(handGrabControllers[i].CurrentGrabbable, ref candidate, ref ignoredStillHeld);
            for (int i = 0; i < gloveGrabControllers.Length; i++)
                InspectHeld(gloveGrabControllers[i].CurrentGrabbable, ref candidate, ref ignoredStillHeld);

            // Presa delle mani WEART (VR / guanto reale): legge l'oggetto afferrato dal bridge.
            if (graspBridge != null)
            {
                InspectHeldObject(graspBridge.LeftGrasped, ref candidate, ref ignoredStillHeld);
                InspectHeldObject(graspBridge.RightGrasped, ref candidate, ref ignoredStillHeld);
            }
        }

        private void InspectHeld(IGrabbable grabbable, ref RecognizableShape candidate, ref bool ignoredStillHeld)
        {
            if (grabbable == null || !grabbableToShape.TryGetValue(grabbable, out var rec)) return;
            if (rec == ignoredShape) ignoredStillHeld = true;
            else if (candidate == null) candidate = rec;
        }

        // Variante per la presa WEART: dall'oggetto fisico afferrato (il GameObject del
        // WeArtTouchableObject, anche una sotto-mesh) risale alla forma con GetComponentInParent.
        private void InspectHeldObject(GameObject grasped, ref RecognizableShape candidate, ref bool ignoredStillHeld)
        {
            if (grasped == null) return;
            var rec = grasped.GetComponentInParent<RecognizableShape>();
            if (rec == null || !shapeToDef.ContainsKey(rec)) return;
            if (rec == ignoredShape) ignoredStillHeld = true;
            else if (candidate == null) candidate = rec;
        }

        private void Confirm(RecognizableShape shape)
        {
            shapeToDef.TryGetValue(shape, out var def);
            bool correct = def != null && currentTarget != null && def.Id == currentTarget.Id;
            float elapsed = Time.time - roundStartTime;

            StopHoldAudio();
            holdTimer = 0f;
            cooldownTimer = selectionCooldown;
            ignoredShape = shape; // va rilasciata prima che un nuovo hold conti
            heldShape = null;

            if (correct)
            {
                PlayOneShot(correctClip);
                Log("answer_correct",
                    $"{{\"round\":{roundIndex + 1},\"target\":\"{currentTarget.Id}\",\"errors\":{currentRoundErrors},\"timeSec\":{elapsed:0.00}}}");
                NextRound();
            }
            else
            {
                currentRoundErrors++;
                PlayOneShot(wrongClip);
                Log("answer_wrong",
                    $"{{\"round\":{roundIndex + 1},\"target\":\"{currentTarget.Id}\",\"chosen\":\"{def?.Id}\",\"errors\":{currentRoundErrors}}}");
                // stesso bersaglio: si resta in AwaitingSelection
            }
        }

        private void LevelComplete()
        {
            state = State.LevelComplete;
            currentTarget = null;
            StopHoldAudio();
            PlayOneShot(levelCompleteClip);
            Log("level_complete", $"{{\"rounds\":{roundOrder.Count}}}");
        }

        // --- Audio -------------------------------------------------------------

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null || voiceSource == null) return;
            voiceSource.PlayOneShot(clip);
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
