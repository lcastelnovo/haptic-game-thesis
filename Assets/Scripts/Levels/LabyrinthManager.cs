using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using WeArt.Components;
using HapticResearch.Audio;
using HapticResearch.Experiment;
using HapticResearch.Labyrinth;
using HapticResearch.UI;

namespace HapticResearch.Levels
{
    // Level 2 - Labirinto a bivi termici.
    //
    // Flusso: l'operatore avvia. Il partecipante non vedente cerca prima l'INGRESSO: il
    // labirinto e' chiuso da un muro esterno con una sola apertura, quindi lo si trova
    // seguendo il bordo col dito (un faro sonoro aiuta, ma non e' l'unico appiglio). Poi
    // segue il corridoio. A ogni BIVIO appoggia il dito all'imbocco di un ramo e lo tiene
    // fermo: dopo la sosta sente caldo se e' la strada giusta, freddo se e' sbagliata.
    // Ne basta una, di lettura: l'altro ramo e' l'opposto per costruzione.
    //
    // Perche' a soste e non con un gradiente di calore continuo: l'attuatore Peltier
    // impiega 2-3 s a raggiungere il valore e il dito nel frattempo ha percorso 12-37 cm,
    // cioe' uno o due corridoi oltre il punto che ha generato il segnale. Un gradiente
    // termico non e' campionabile abbastanza in fretta; una scelta discreta si'.
    //
    // La logica NON interroga i collider: la geometria e' generata da un MazeLayoutAsset,
    // quindi il manager ha una mappa di celle e sapere dov'e' il dito costa O(1). I
    // collider servono solo a far sentire muri e piastrelle ai guanti. Chi attraversa un
    // muro (i muri sono trigger, il dito passa) viene riconosciuto e riportato indietro:
    // per chi non vede, uscire dal labirinto senza accorgersene significa perdere tutto
    // il riferimento spaziale.
    public class LabyrinthManager : LevelController
    {
        private enum State { Idle, SeekingEntrance, Following, LevelComplete }

        [Header("Identita' (HUD operatore)")]
        [SerializeField] private int levelNumber = 2;
        [SerializeField] private string levelTitle = "Labirinto";
        [SerializeField] private string levelId = "level2_labyrinth";

        [Header("Labirinto")]
        [Tooltip("Se vuoto lo cerca in scena. E' lui che tiene il layout e lo ancora al tavolo.")]
        [SerializeField] private MazeRuntime maze;

        [Tooltip("Taratura aptica del partecipante. Se vuoto si usano i valori di default del profilo.")]
        [SerializeField] private HapticProfile profile;

        [Header("Tappe (solo marker per l'operatore)")]
        [SerializeField] private List<MazeZone> zones = new List<MazeZone>();

        [Header("Muri (per il conteggio dei contatti: la logica usa la mappa)")]
        [SerializeField] private List<Collider> walls = new List<Collider>();
        [SerializeField] private string wallNamePrefix = "Muro";

        [Header("Rilevamento (piano del tavolo)")]
        [Tooltip("Altezza a cui si proietta la punta del dito per il contatto coi muri.")]
        [SerializeField] private float probeHeight = 0.9f;

        [Tooltip("Raggio del polpastrello (m).")]
        [SerializeField] private float fingerRadius = 0.012f;

        [Tooltip("Sopra questa altezza la punta e' sollevata dal tavolo: non tocca niente e non conta come fuori percorso.")]
        [SerializeField] private float maxTipHeight = 0.97f;

        [Tooltip("Secondi minimi fra due EPISODI di contatto conteggiati.")]
        [SerializeField] private float wallTouchCooldown = 0.8f;

        [Tooltip("Quanto il dito deve restare dentro un muro prima di dichiarare il fuori percorso: sotto questa soglia e' solo uno sfioramento.")]
        [SerializeField] private float offTrackDelay = 0.6f;

        [Header("Faro sonoro")]
        [SerializeField] private AudioClip beaconClip;
        [SerializeField] private float beaconFarDistance = 0.63f;
        [SerializeField] private float beaconSlowInterval = 1.2f;
        [SerializeField] private float beaconFastInterval = 0.15f;
        [SerializeField] private float beaconFarPitch = 0.8f;
        [SerializeField] private float beaconNearPitch = 1.5f;
        [SerializeField] private bool beaconSilentWhileSpeaking = true;

        [Header("Condizione sperimentale")]
        [Tooltip("Vuoto = la prende da SessionLogger.condition. 'audio' usa il cue sonoro, qualunque altro valore usa quello termico.")]
        [SerializeField] private string forcedCondition = "";

        [Header("Suoni 2D")]
        [SerializeField] private AudioClip wallBumpClip;
        [SerializeField] private AudioClip checkpointClip;
        [SerializeField] private AudioClip exitClip;
        [SerializeField] private AudioClip levelCompleteClip;
        [Tooltip("Tono in loop durante la sosta sulla piastrella: il pitch sale da 1 a 2.")]
        [SerializeField] private AudioClip dwellToneClip;
        [Tooltip("Colpetto di fine sosta: da qui la lettura e' valida.")]
        [SerializeField] private AudioClip readingReadyClip;
        [Tooltip("Condizione 'audio': esito del ramo giusto.")]
        [SerializeField] private AudioClip branchCorrectClip;
        [Tooltip("Condizione 'audio': esito del ramo sbagliato.")]
        [SerializeField] private AudioClip branchWrongClip;
        [SerializeField] private AudioClip deadEndClip;
        [Tooltip("Loop continuo mentre si e' fuori dal labirinto.")]
        [SerializeField] private AudioClip offTrackLoopClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Controlli operatore")]
        [SerializeField] private KeyCode startKey = KeyCode.Return;
        [SerializeField] private KeyCode repeatKey = KeyCode.R;
        [SerializeField] private bool autoStart = false;

        [Header("Marker delle tappe (solo per l'operatore)")]
        [SerializeField] private bool showZoneMarkers = true;

        [Header("Logging")]
        [SerializeField] private SessionLogger sessionLogger;

        // --- runtime ---------------------------------------------------------------

        // Un bivio, gia' risolto in celle: dove ci si ferma, i due imbocchi, e la cella
        // oltre il ramo giusto che vale come "scelta fatta".
        private struct Junction
        {
            public Vector2Int cell;
            public Vector2Int correctTile;
            public Vector2Int deadTile;
            public Vector2Int resolveCell;
            public bool hasResolveCell;
            public string label;
        }

        private State state = State.Idle;
        private readonly List<Junction> junctions = new List<Junction>();
        private int nextJunction;

        private FingerProbeSource probes;
        private WallContactTracker wallTracker;
        private ProximityBeacon beacon;
        private IJunctionCue cue;

        private Vector2Int currentCell, lastGoodCell;
        private bool hasCell;
        private bool offTrack;
        private float offTrackTimer;
        private float offTrackSince;

        private bool prevReading, prevReadingReady;
        private float junctionArmedTime;
        private float lastDeadEndTime = float.NegativeInfinity;

        private float levelStartTime = -1f, levelEndTime = -1f, entranceTime = -1f;

        private AudioSource sfxSource, beaconSource, toneSource, offTrackSource;
        private readonly List<GameObject> markers = new List<GameObject>();

        // --- LevelController ----------------------------------------------------------

        public override string LevelId => levelId;
        public override int LevelNumber => levelNumber;
        public override string LevelTitle => levelTitle;
        public override bool IsRunning => state == State.SeekingEntrance || state == State.Following;
        public override bool IsComplete => state == State.LevelComplete;
        public int WallTouches => wallTracker != null ? wallTracker.Touches : 0;
        public string ConditionName => cue != null ? cue.CueName : ResolveCondition();

        public override string StatusLine
        {
            get
            {
                switch (state)
                {
                    case State.SeekingEntrance:
                        return "cerca l'ingresso, faro attivo";
                    case State.Following:
                        if (offTrack) return $"FUORI PERCORSO da {F(Time.time - offTrackSince, "0")} s";
                        if (cue != null && cue.Reading)
                            return $"bivio {nextJunction + 1}/{junctions.Count}, lettura {Mathf.RoundToInt(cue.Progress01 * 100f)}%";
                        if (cue != null && cue.Armed)
                            return $"bivio {nextJunction + 1}/{junctions.Count} armato ({cue.CueName})";
                        return $"bivi {nextJunction}/{junctions.Count}, contatti muro: {WallTouches}";
                    case State.LevelComplete:
                        return $"completato, contatti muro: {WallTouches} ({F(wallTracker != null ? wallTracker.ContactSeconds : 0f, "0")} s)";
                    default:
                        return "in attesa di avvio";
                }
            }
        }

        public override float ElapsedSeconds
        {
            get
            {
                if (levelStartTime < 0f) return 0f;
                return (levelEndTime >= 0f ? levelEndTime : Time.time) - levelStartTime;
            }
        }

        // --- Ciclo di vita ----------------------------------------------------------------

        void Awake()
        {
            if (NarrationManager.Instance == null &&
                FindObjectsByType<NarrationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
                gameObject.AddComponent<NarrationManager>();

            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            if (maze == null) maze = FindFirstObjectByType<MazeRuntime>(FindObjectsInactive.Include);

            SetupAudio();

            probes = new FingerProbeSource();
            wallTracker = new WallContactTracker(wallTouchCooldown, probeHeight, fingerRadius, maxTipHeight);
            wallTracker.OnEpisode += OnWallEpisode;

            beacon = new ProximityBeacon(beaconSource)
            {
                Clip = beaconClip,
                FarDistance = beaconFarDistance,
                SlowInterval = beaconSlowInterval,
                FastInterval = beaconFastInterval,
                FarPitch = beaconFarPitch,
                NearPitch = beaconNearPitch,
                Volume = sfxVolume,
                SilentWhileSpeaking = beaconSilentWhileSpeaking,
            };

            if (walls.Count == 0) CollectWalls();
            zones.RemoveAll(z => z == null);
        }

        void Start()
        {
            if (showZoneMarkers) BuildMarkers();
            if (autoStart) StartLevel();
        }

        void OnDestroy()
        {
            cue?.Disarm();
            if (wallTracker != null) wallTracker.OnEpisode -= OnWallEpisode;
            foreach (var m in markers) if (m != null) Destroy(m);
            markers.Clear();
        }

        private void SetupAudio()
        {
            sfxSource = NewSource(false);
            beaconSource = NewSource(false);  // sorgente separata: il pitch del faro non tocca gli altri suoni
            toneSource = NewSource(true);
            offTrackSource = NewSource(true);
            offTrackSource.clip = offTrackLoopClip;
        }

        private AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.spatialBlend = 0f; // 2D: si sente sempre
            s.playOnAwake = false;
            s.loop = loop;
            s.volume = 1f;
            return s;
        }

        // Ripiego se il tool non ha riempito la lista: oggetti tattili col nome giusto.
        private void CollectWalls()
        {
            foreach (var t in FindObjectsByType<WeArtTouchableObject>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith(wallNamePrefix)) continue;
                var c = t.GetComponent<Collider>();
                if (c != null) walls.Add(c);
            }
        }

        // --- Avvio -------------------------------------------------------------------------

        public override void StartLevel()
        {
            if (maze == null || maze.Map == null)
            {
                Debug.LogError("[Labirinto] Manca il MazeRuntime (o il suo layout): impossibile avviare. Genera la geometria con HapticResearch/Level 2/Genera geometria labirinto.");
                return;
            }

            BuildJunctions();

            state = State.SeekingEntrance;
            nextJunction = 0;
            levelStartTime = Time.time;
            levelEndTime = -1f;
            entranceTime = -1f;
            hasCell = false;
            offTrack = false;
            offTrackTimer = 0f;
            prevReading = prevReadingReady = false;
            lastDeadEndTime = float.NegativeInfinity;

            wallTracker.Reset();
            probes.Invalidate();
            beacon.FarDistance = Mathf.Max(0.2f, beaconFarDistance);
            beacon.Restart(1.5f); // poi aspetta comunque la fine delle istruzioni
            StopOffTrackSound();

            cue?.Disarm();
            cue = CreateCue();

            Voice("level2_intro", null, false);
            Log("level_start", $"{{\"junctions\":{junctions.Count},\"walls\":{walls.Count},\"cue\":\"{cue.CueName}\"}}");
        }

        public override void RepeatAnnouncement()
        {
            switch (state)
            {
                case State.SeekingEntrance: Voice("level2_find_entrance", null, false); break;
                case State.Following:
                    if (offTrack) Voice("level2_off_track", null, false);
                    else if (cue != null && cue.Armed) Voice(JunctionVoiceKey(), null, false);
                    else Voice("level2_follow", null, false);
                    break;
                case State.LevelComplete: Voice("level2_next_hint", null, false); break;
            }
        }

        // La condizione decide SOLO come si legge il bivio: labirinto, soste e tempi
        // restano identici, altrimenti il confronto non misurerebbe il canale sensoriale.
        private string ResolveCondition()
        {
            if (!string.IsNullOrEmpty(forcedCondition)) return forcedCondition.Trim().ToLowerInvariant();
            var logger = sessionLogger != null ? sessionLogger : SessionLogger.Instance;
            return logger != null ? (logger.Condition ?? "").Trim().ToLowerInvariant() : "";
        }

        private IJunctionCue CreateCue()
        {
            if (profile == null) profile = ScriptableObject.CreateInstance<HapticProfile>();

            if (ResolveCondition() == "audio")
                return new AudioJunctionCue(profile, toneSource, sfxSource, dwellToneClip, readingReadyClip,
                                            sfxVolume, branchCorrectClip, branchWrongClip);

            return new ThermalJunctionCue(profile, toneSource, sfxSource, dwellToneClip, readingReadyClip, sfxVolume);
        }

        private string JunctionVoiceKey() =>
            cue != null && cue.CueName == "audio" ? "level2_junction_audio" : "level2_junction";

        // I bivi si risolvono in celle una volta sola, all'avvio: in Update servono
        // confronti fra Vector2Int, non ricerche nel layout.
        private void BuildJunctions()
        {
            junctions.Clear();
            var layout = maze.Layout;
            var map = maze.Map;
            if (layout == null || map == null) return;

            for (int i = 0; i < layout.JunctionCount; i++)
            {
                if (!layout.TryGetJunction(i, out var cell, out var correctDir, out var deadDir, out var label)) continue;

                var correctTile = MazeMap.Neighbor(cell.x, cell.y, correctDir);
                var j = new Junction
                {
                    cell = cell,
                    correctTile = correctTile,
                    deadTile = MazeMap.Neighbor(cell.x, cell.y, deadDir),
                    label = string.IsNullOrEmpty(label) ? $"bivio {i + 1}" : label,
                };

                // "Scelta fatta" = si e' andati oltre l'imbocco giusto, non appena lo si tocca.
                int k = map.PathIndexOf(correctTile.x, correctTile.y);
                if (k >= 0 && k + 1 < map.Path.Count)
                {
                    j.resolveCell = map.Path[k + 1];
                    j.hasResolveCell = true;
                }
                junctions.Add(j);
            }
        }

        // --- Loop ---------------------------------------------------------------------------

        void Update()
        {
            if (state == State.Idle || state == State.LevelComplete)
            {
                if (Input.GetKeyDown(startKey)) StartLevel();
                return;
            }

            if (Input.GetKeyDown(repeatKey)) RepeatAnnouncement();

            probes.Refresh();

            if (state == State.SeekingEntrance) { UpdateSeekingEntrance(); return; }

            UpdateFollowing();
        }

        private void UpdateSeekingEntrance()
        {
            beacon.Tick(DistanceToCell(maze.Map.EntranceCell));

            // Finche' parlano le istruzioni l'ingresso non si convalida: se il dito e' gia'
            // li', viene riconosciuto appena la voce finisce.
            var nm = NarrationManager.Instance;
            if (beaconSilentWhileSpeaking && nm != null && nm.IsSpeaking) return;

            if (TryLocate(out var cell, out var result) &&
                (result == LocateResult.InCell || result == LocateResult.InOpening) &&
                cell == maze.Map.EntranceCell)
                EnterMaze();
        }

        private void UpdateFollowing()
        {
            float dt = Time.deltaTime;

            wallTracker.Tick(walls, probes.Tips, dt);
            cue?.Tick(dt);
            ReportCueTransitions();

            bool located = TryLocate(out var cell, out var result);

            // Mano sollevata: non si aggiorna niente e soprattutto non si dichiara il
            // fuori percorso. Alzare la mano e' un gesto legittimo, non un errore.
            if (!located) return;

            bool inside = result == LocateResult.InCell || result == LocateResult.InOpening;
            UpdateOffTrack(inside, cell, dt);
            if (!inside || offTrack) { if (offTrack) beacon.Tick(DistanceToCell(lastGoodCell)); return; }

            currentCell = cell;
            hasCell = true;
            lastGoodCell = cell;

            if (cell == maze.Map.ExitCell) { LevelComplete(); return; }

            UpdateJunctions(cell);
        }

        // --- Bivi -----------------------------------------------------------------------------

        private void UpdateJunctions(Vector2Int cell)
        {
            if (nextJunction >= junctions.Count) return;
            var j = junctions[nextJunction];

            // Prima si chiude il bivio corrente, poi si arma il successivo: possono
            // cadere sulla stessa cella (l'imbocco giusto di uno e' il bivio dopo).
            if (cue.Armed && j.hasResolveCell && cell == j.resolveCell) { ResolveJunction(j, true); return; }

            if (!cue.Armed && cell == j.cell) { ArmJunction(j); return; }

            if (!cue.Armed) return;

            if (cell == j.correctTile) cue.BeginReading(true);
            else if (cell == j.deadTile) { cue.BeginReading(false); ReportDeadEnd(j, cell); }
            else cue.CancelReading();
        }

        private void ArmJunction(Junction j)
        {
            cue.Arm(nextJunction);
            junctionArmedTime = Time.time;
            prevReading = prevReadingReady = false;
            PlaySfx(checkpointClip);
            Voice(JunctionVoiceKey(), null, true);
            Log("junction_armed",
                $"{{\"junction\":{nextJunction + 1},\"label\":\"{j.label}\",\"cell\":\"{Cell(j.cell)}\"," +
                $"\"cue\":\"{cue.CueName}\",\"t\":{F(ElapsedSeconds)}}}");
        }

        private void ResolveJunction(Junction j, bool correct)
        {
            Log("junction_resolved",
                $"{{\"junction\":{nextJunction + 1},\"label\":\"{j.label}\",\"correct\":{(correct ? "true" : "false")}," +
                $"\"readings\":{cue.ReadingsCompleted},\"decisionSeconds\":{F(Time.time - junctionArmedTime)}," +
                $"\"wallTouches\":{WallTouches},\"t\":{F(ElapsedSeconds)}}}");

            cue.Disarm();
            nextJunction++;
            PlaySfx(checkpointClip);
            Voice("level2_checkpoint", null, true);
        }

        private void ReportDeadEnd(Junction j, Vector2Int cell)
        {
            if (Time.time - lastDeadEndTime < 2f) return;
            lastDeadEndTime = Time.time;
            PlaySfx(deadEndClip);
            Log("dead_end",
                $"{{\"junction\":{nextJunction + 1},\"cell\":\"{Cell(cell)}\",\"t\":{F(ElapsedSeconds)}}}");
        }

        // Il cue non logga da solo: non deve sapere niente dell'esperimento.
        private void ReportCueTransitions()
        {
            if (cue == null) return;

            if (cue.Reading && !prevReading)
                Log("reading_begin",
                    $"{{\"junction\":{nextJunction + 1},\"branch\":\"{BranchName()}\",\"t\":{F(ElapsedSeconds)}}}");

            if (!cue.Reading && prevReading && !prevReadingReady)
                Log("reading_cancel", $"{{\"junction\":{nextJunction + 1},\"t\":{F(ElapsedSeconds)}}}");

            if (cue.ReadingReady && !prevReadingReady)
                Log("reading_ready",
                    $"{{\"junction\":{nextJunction + 1},\"branch\":\"{BranchName()}\"," +
                    $"\"readings\":{cue.ReadingsCompleted},\"t\":{F(ElapsedSeconds)}}}");

            prevReading = cue.Reading;
            prevReadingReady = cue.ReadingReady;
        }

        private string BranchName()
        {
            var b = cue as JunctionCueBase;
            return b == null ? "?" : (b.ReadingCorrectBranch ? "giusto" : "sbagliato");
        }

        // --- Fuori percorso ----------------------------------------------------------------------

        // I muri sono trigger: il dito li attraversa. Per chi vede e' un dettaglio, per chi
        // non vede e' la perdita completa del riferimento. Serve accorgersene e riportarlo.
        private void UpdateOffTrack(bool inside, Vector2Int cell, float dt)
        {
            if (inside)
            {
                offTrackTimer = 0f;
                if (!offTrack) return;
                offTrack = false;
                StopOffTrackSound();
                Voice("level2_back_on_track", null, false);
                Log("back_on_track",
                    $"{{\"cell\":\"{Cell(cell)}\",\"offSeconds\":{F(Time.time - offTrackSince)},\"t\":{F(ElapsedSeconds)}}}");
                return;
            }

            if (offTrack) return;

            offTrackTimer += dt;
            if (offTrackTimer < offTrackDelay) return;

            offTrack = true;
            offTrackSince = Time.time;
            if (offTrackSource != null && offTrackLoopClip != null && !offTrackSource.isPlaying) offTrackSource.Play();
            beacon.Restart(0.5f);
            Voice("level2_off_track", null, false);
            Log("off_track",
                $"{{\"lastCell\":\"{(hasCell ? Cell(lastGoodCell) : "?")}\",\"t\":{F(ElapsedSeconds)}}}");
        }

        private void StopOffTrackSound()
        {
            if (offTrackSource != null && offTrackSource.isPlaying) offTrackSource.Stop();
        }

        // --- Tappe del flusso -----------------------------------------------------------------------

        private void EnterMaze()
        {
            state = State.Following;
            entranceTime = Time.time;
            lastGoodCell = maze.Map.EntranceCell;
            hasCell = true;
            wallTracker.Prime(); // se il dito e' gia' su un muro entrando, non e' un errore
            beacon.Stop();
            PlaySfx(checkpointClip);
            Voice("level2_entered", null, true);
            Log("entrance_reached", $"{{\"t\":{F(ElapsedSeconds)}}}");
        }

        private void LevelComplete()
        {
            state = State.LevelComplete;
            levelEndTime = Time.time;
            cue?.Disarm();
            beacon.Stop();
            StopOffTrackSound();
            PlaySfx(exitClip);
            Voice("level2_complete", levelCompleteClip, false);
            Log("level_complete",
                $"{{\"t\":{F(ElapsedSeconds)},\"fromEntrance\":{F(entranceTime >= 0f ? levelEndTime - entranceTime : -1f)}," +
                $"\"junctions\":{nextJunction},\"wallTouches\":{WallTouches}," +
                $"\"wallContactSeconds\":{F(wallTracker.ContactSeconds)},\"cue\":\"{cue?.CueName}\"}}");
        }

        private void OnWallEpisode(Collider wall)
        {
            PlaySfx(wallBumpClip);
            if (wall == null) return;
            var p = wall.transform.position;
            Log("wall_touch",
                $"{{\"wall\":\"{wall.name}\",\"x\":{F(p.x, "0.000")},\"z\":{F(p.z, "0.000")}," +
                $"\"count\":{WallTouches},\"t\":{F(ElapsedSeconds)}}}");
        }

        // --- Punta del dito -------------------------------------------------------------------------

        // Si preferisce una punta che sia DENTRO il labirinto: con due mani in scena non
        // deve essere quella ferma fuori a dichiarare il fuori percorso.
        private bool TryLocate(out Vector2Int cell, out LocateResult result)
        {
            cell = Vector2Int.zero;
            result = LocateResult.OutsideBounds;

            bool any = false;
            foreach (var tip in probes.Tips)
            {
                if (tip.y > maxTipHeight) continue; // mano sollevata
                var r = maze.Locate(tip, out var c);
                if (!any) { cell = c; result = r; any = true; }
                if (r == LocateResult.InCell || r == LocateResult.InOpening) { cell = c; result = r; return true; }
            }
            return any;
        }

        private float DistanceToCell(Vector2Int cell)
        {
            var target = maze.CellCenterWorld(cell);
            float best = float.PositiveInfinity;
            foreach (var tip in probes.Tips)
            {
                float dx = tip.x - target.x, dz = tip.z - target.z;
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        // --- Marker -------------------------------------------------------------------------------------

        private void BuildMarkers()
        {
            var shader = Shader.Find("Sprites/Default");
            foreach (var z in zones)
            {
                if (z == null) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"Marker_{z.Label}";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(z.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = new Vector3(z.Radius * 2f, 0.0015f, z.Radius * 2f);
                if (shader != null)
                {
                    var c = z.GizmoColor; c.a = 0.55f;
                    go.GetComponent<Renderer>().material = new Material(shader) { color = c };
                }
                markers.Add(go);
            }
        }

        // --- Audio / log -----------------------------------------------------------------------------------

        private void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // Battuta pre-generata per chiave; se manca, clip di riserva (e testo nei sottotitoli).
        private void Voice(string key, AudioClip fallback, bool queue)
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(key))
            {
                if (queue) nm.SpeakQueued(key); else nm.Speak(key);
                return;
            }
            if (fallback != null)
            {
                VoiceSubtitles.ReportSaid(VoiceLines.TextOf(key) ?? $"[{key}]", fallback.length);
                PlaySfx(fallback);
            }
            else Debug.LogWarning($"[Labirinto] Traccia vocale '{key}' mancante: genera le voci con Tools/generate_voice_macos.py.");
        }

        private static string Cell(Vector2Int c) => $"{c.x},{c.y}";

        // Numeri nei JSON di log sempre col punto decimale: su Windows in italiano
        // ToString userebbe la virgola e spezzerebbe il CSV.
        private static string F(float v, string format = "0.00") => v.ToString(format, CultureInfo.InvariantCulture);

        private void Log(string eventType, string json)
        {
            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            sessionLogger?.Log(levelId, eventType, json);
        }
    }
}
