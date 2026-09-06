using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using HapticResearch.Audio;
using HapticResearch.Experiment;
using HapticResearch.Hands;
using HapticResearch.UI;

namespace HapticResearch.Levels
{
    // Level 2 - Labirinto.
    //
    // Flusso: l'operatore avvia (startKey / "avvia" / bottone HUD). Il partecipante non
    // vedente deve prima TROVARE L'INGRESSO: un faro sonoro batte piu' veloce man mano
    // che la mano si avvicina. Poi segue il corridoio con l'indice fino all'USCITA,
    // passando per eventuali checkpoint (tappe in ordine, vedi MazeZone). I muri si
    // sentono coi guanti (WeArtTouchableObject, gia' in scena); ogni tocco di muro fa un
    // colpetto sonoro e viene conteggiato nel log.
    //
    // La logica e' tutta sul piano XZ del tavolo: la posizione della punta dell'indice
    // viene proiettata a probeHeight e confrontata con i collider dei muri (ClosestPoint)
    // e con le tappe. Vale identica per le mani demo (mouse) e per i guanti reali con
    // tracker: cambia solo QUALE mano si ascolta (demo ON -> mani sotto HandManager,
    // demo OFF -> mani WEART del prefab, mosse dai tracker).
    //
    // Non fa nessun blocco fisico: il dito puo' attraversare i muri, come in Level 1
    // (la sensazione la danno i pad aptici).
    public class LabyrinthManager : LevelController
    {
        private enum State { Idle, SeekingEntrance, Following, LevelComplete }

        [Header("Identita' (HUD operatore)")]
        [SerializeField] private int levelNumber = 2;
        [SerializeField] private string levelTitle = "Labirinto";
        [SerializeField] private string levelId = "level2_labyrinth";

        [Header("Tappe (in ordine: ingresso, checkpoint..., uscita)")]
        [SerializeField] private List<MazeZone> zones = new List<MazeZone>();

        [Header("Muri")]
        [Tooltip("Collider dei muri. Se vuoto, a runtime raccoglie i WeArtTouchableObject della scena il cui nome inizia con il prefisso.")]
        [SerializeField] private List<Collider> walls = new List<Collider>();
        [SerializeField] private string wallNamePrefix = "Cube";

        [Header("Rilevamento (piano del tavolo)")]
        [Tooltip("Altezza a cui si proietta la punta del dito per il contatto coi muri (i muri stanno a y 0.85-0.95).")]
        [SerializeField] private float probeHeight = 0.9f;

        [Tooltip("Raggio del polpastrello (m).")]
        [SerializeField] private float fingerRadius = 0.012f;

        [Tooltip("Sopra questa altezza (m) la punta e' sollevata dal tavolo: non tocca i muri e non raggiunge le tappe. I muri arrivano a y=0.95; la punta dell'indice delle mani demo sta a ~0.94.")]
        [SerializeField] private float maxTipHeight = 0.97f;

        [Tooltip("Secondi minimi tra due EPISODI di contatto conteggiati. Un episodio = il dito tocca un muro dopo non toccarne nessuno; strisciare lungo una parete (anche su piu' segmenti) e' un solo episodio.")]
        [SerializeField] private float wallTouchCooldown = 0.8f;

        [Header("Faro dell'ingresso (beep piu' veloce e piu' acuto vicino all'ingresso)")]
        [SerializeField] private AudioClip beaconClip;
        [Tooltip("Oltre questa distanza (m) il beep va al ritmo piu' lento (il tavolo e' 1.5 x 0.8 m).")]
        [SerializeField] private float beaconFarDistance = 1.2f;
        [SerializeField] private float beaconSlowInterval = 1.2f;
        [SerializeField] private float beaconFastInterval = 0.15f;
        [Tooltip("Pitch del beep lontano/vicino: seconda dimensione del gradiente, oltre al ritmo.")]
        [SerializeField] private float beaconFarPitch = 0.8f;
        [SerializeField] private float beaconNearPitch = 1.5f;
        [Tooltip("Il faro tace mentre la voce parla (istruzioni, annunci).")]
        [SerializeField] private bool beaconSilentWhileSpeaking = true;

        [Header("Controlli operatore")]
        [SerializeField] private KeyCode startKey = KeyCode.Return;
        [SerializeField] private KeyCode repeatKey = KeyCode.R;
        [SerializeField] private bool autoStart = false;

        [Header("Suoni 2D")]
        [SerializeField] private AudioClip wallBumpClip;
        [SerializeField] private AudioClip checkpointClip;
        [Tooltip("Suono non verbale all'uscita, prima della voce 'livello completato'.")]
        [SerializeField] private AudioClip exitClip;
        [Tooltip("Clip di riserva per 'livello completato' se manca la traccia vocale.")]
        [SerializeField] private AudioClip levelCompleteClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Marker delle tappe (solo per l'operatore)")]
        [Tooltip("Dischi colorati sul tavolo alle tappe: verde ingresso, giallo checkpoint, azzurro uscita.")]
        [SerializeField] private bool showZoneMarkers = true;

        [Header("Logging")]
        [Tooltip("Se null prova ad usare SessionLogger.Instance.")]
        [SerializeField] private SessionLogger sessionLogger;

        // --- runtime ---
        private State state = State.Idle;
        private int nextZoneIndex;         // tappa da raggiungere (indice in zones)
        private int wallTouches;
        private float levelStartTime = -1f;
        private float levelEndTime = -1f;
        private float entranceTime = -1f;
        private float nextBeepTime;
        private float wallContactSeconds;   // tempo totale col dito su un muro
        private bool primeWallState;        // primo frame dopo l'ingresso: registra lo stato senza contare
        private bool touchingAnyPrev;
        private float lastTouchEpisodeTime = float.NegativeInfinity;

        private AudioSource sfxSource;
        private AudioSource beaconSource; // sorgente separata: il pitch del faro non tocca gli altri suoni
        private readonly List<GameObject> markers = new List<GameObject>();

        // Punte dell'indice di tutte le mani in scena (demo e WEART), classificate.
        private struct Probe { public Transform tip; public bool demo; public bool left; }
        private readonly List<Probe> probes = new List<Probe>();
        private readonly List<Vector3> activeTips = new List<Vector3>();
        private bool probesDemoState;
        private bool probesCollected;

        private readonly HashSet<Collider> touchingPrev = new HashSet<Collider>();
        private readonly HashSet<Collider> touchingNow = new HashSet<Collider>();

        // --- LevelController ---
        public override string LevelId => levelId;
        public override int LevelNumber => levelNumber;
        public override string LevelTitle => levelTitle;
        public override bool IsRunning => state == State.SeekingEntrance || state == State.Following;
        public override bool IsComplete => state == State.LevelComplete;
        public int WallTouches => wallTouches;

        public override string StatusLine
        {
            get
            {
                switch (state)
                {
                    case State.SeekingEntrance: return "cerca l'ingresso · faro attivo";
                    case State.Following: return $"tappa {nextZoneIndex}/{Mathf.Max(1, zones.Count - 1)} · contatti muro: {wallTouches}";
                    case State.LevelComplete: return $"completato · contatti muro: {wallTouches} ({F(wallContactSeconds, "0")} s)";
                    default: return "in attesa di avvio";
                }
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
            if (NarrationManager.Instance == null)
            {
                var found = FindObjectsByType<NarrationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found.Length == 0) gameObject.AddComponent<NarrationManager>();
            }
            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 0f; // 2D: si sente sempre
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.volume = 1f; // il volume lo passa PlayOneShot (una volta sola)

            beaconSource = gameObject.AddComponent<AudioSource>();
            beaconSource.spatialBlend = 0f;
            beaconSource.playOnAwake = false;
            beaconSource.loop = false;
            beaconSource.volume = 1f;

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
            foreach (var m in markers) if (m != null) Destroy(m);
            markers.Clear();
        }

        // Muri = oggetti tattili della scena col nome giusto (i cubi di Luca), solo se la
        // lista non e' stata riempita dal tool/Inspector.
        private void CollectWalls()
        {
            foreach (var t in FindObjectsByType<WeArtTouchableObject>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith(wallNamePrefix)) continue;
                var c = t.GetComponent<Collider>();
                if (c != null) walls.Add(c);
            }
            Debug.Log($"[Labyrinth] Muri raccolti a runtime: {walls.Count}");
        }

        // --- Loop -----------------------------------------------------------------

        void Update()
        {
            if (state == State.Idle || state == State.LevelComplete)
            {
                if (Input.GetKeyDown(startKey)) StartLevel();
                return;
            }

            if (Input.GetKeyDown(repeatKey)) RepeatAnnouncement();

            GatherActiveTips();

            if (state == State.SeekingEntrance)
            {
                UpdateBeacon();
                // Finche' parlano le istruzioni (o un ri-annuncio) l'ingresso non si conta:
                // se il dito e' gia' li', viene rilevato appena la voce finisce.
                var nm = NarrationManager.Instance;
                if (beaconSilentWhileSpeaking && nm != null && nm.IsSpeaking) return;
                if (zones.Count > 0 && AnyTipIn(zones[0])) EnterMaze();
                return;
            }

            // Following
            UpdateWallTouches();
            if (nextZoneIndex < zones.Count && AnyTipIn(zones[nextZoneIndex])) ReachZone();
        }

        public override void StartLevel()
        {
            if (zones.Count < 2)
            {
                Debug.LogWarning("[Labyrinth] Servono almeno due tappe (ingresso e uscita): impossibile avviare.");
                return;
            }

            state = State.SeekingEntrance;
            nextZoneIndex = 0;
            wallTouches = 0;
            wallContactSeconds = 0f;
            levelStartTime = Time.time;
            levelEndTime = -1f;
            entranceTime = -1f;
            nextBeepTime = Time.time + 1.5f; // poi il faro aspetta comunque la fine delle istruzioni
            touchingPrev.Clear();
            touchingAnyPrev = false;
            lastTouchEpisodeTime = float.NegativeInfinity;
            probesCollected = false;

            Voice("level2_intro", null, false);
            Log("level_start", $"{{\"zones\":{zones.Count},\"walls\":{walls.Count}}}");
        }

        public override void RepeatAnnouncement()
        {
            switch (state)
            {
                case State.SeekingEntrance: Voice("level2_find_entrance", null, false); break;
                case State.Following: Voice("level2_follow", null, false); break;
                case State.LevelComplete: Voice("level2_next_hint", null, false); break;
            }
        }

        private void EnterMaze()
        {
            state = State.Following;
            nextZoneIndex = 1;
            entranceTime = Time.time;
            touchingPrev.Clear();
            primeWallState = true; // se il dito e' gia' su un muro all'ingresso non e' un errore
            PlaySfx(checkpointClip);
            // Accodata: se l'ingresso viene trovato mentre parlano le istruzioni, non le taglia.
            Voice("level2_entered", null, true);
            Log("entrance_reached", $"{{\"t\":{F(ElapsedSeconds)}}}");
        }

        private void ReachZone()
        {
            var zone = zones[nextZoneIndex];
            bool isLast = nextZoneIndex >= zones.Count - 1;
            if (isLast || zone.ZoneKind == MazeZone.Kind.Exit)
            {
                LevelComplete();
                return;
            }

            nextZoneIndex++;
            PlaySfx(checkpointClip);
            Voice("level2_checkpoint", null, true);
            Log("checkpoint", $"{{\"zone\":\"{zone.Label}\",\"t\":{F(ElapsedSeconds)},\"wallTouches\":{wallTouches}}}");
        }

        private void LevelComplete()
        {
            state = State.LevelComplete;
            levelEndTime = Time.time;
            float fromEntrance = entranceTime >= 0f ? levelEndTime - entranceTime : -1f;
            PlaySfx(exitClip);
            Voice("level2_complete", levelCompleteClip, false);
            Log("level_complete", $"{{\"t\":{F(ElapsedSeconds)},\"fromEntrance\":{F(fromEntrance)},\"wallTouches\":{wallTouches},\"wallContactSeconds\":{F(wallContactSeconds)}}}");
        }

        // --- Mani -----------------------------------------------------------------

        // Le punte dell'indice si cercano una volta per stato demo: cambiano solo quando si
        // accende/spegne la demo (il rig mouse viene attivato/disattivato).
        private void GatherActiveTips()
        {
            bool demoOn = HandDemoModeController.Exists && HandDemoModeController.DemoActive;
            if (!probesCollected || demoOn != probesDemoState)
            {
                CollectProbes();
                probesDemoState = demoOn;
                probesCollected = true;
            }

            activeTips.Clear();
            foreach (var p in probes)
            {
                if (p.tip == null || !p.tip.gameObject.activeInHierarchy) continue;
                if (p.demo != demoOn) continue; // demo ON: solo mani mouse; OFF: solo mani WEART/tracker
                activeTips.Add(p.tip.position);
            }
        }

        private void CollectProbes()
        {
            probes.Clear();
            foreach (var h in FindObjectsByType<WeArtHapticObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if ((h.ActuationPoints & ActuationPointFlags.Index) == 0) continue;
                bool demo = h.GetComponentInParent<HandPhysicsController>(true) != null;
                bool left = (h.HandSides & HandSideFlags.Left) != 0;
                probes.Add(new Probe { tip = h.transform, demo = demo, left = left });
            }
        }

        private bool AnyTipIn(MazeZone zone)
        {
            if (zone == null) return false;
            foreach (var t in activeTips)
                if (t.y <= maxTipHeight && zone.Contains(t)) return true; // mano sollevata: non conta
            return false;
        }

        private float MinDistanceTo(MazeZone zone)
        {
            float best = float.PositiveInfinity;
            foreach (var t in activeTips)
                best = Mathf.Min(best, zone.DistanceXZ(t));
            return best;
        }

        // --- Faro -----------------------------------------------------------------

        private void UpdateBeacon()
        {
            if (beaconClip == null || zones.Count == 0 || Time.time < nextBeepTime) return;

            // Mentre la voce parla il faro tace (non si sovrappone alle istruzioni).
            var nm = NarrationManager.Instance;
            if (beaconSilentWhileSpeaking && nm != null && nm.IsSpeaking)
            {
                nextBeepTime = Time.time + 0.25f;
                return;
            }

            float d = MinDistanceTo(zones[0]);
            float t = float.IsInfinity(d) ? 1f : Mathf.Clamp01(d / Mathf.Max(0.01f, beaconFarDistance));
            float interval = Mathf.Lerp(beaconFastInterval, beaconSlowInterval, t);
            beaconSource.pitch = Mathf.Lerp(beaconNearPitch, beaconFarPitch, t);
            beaconSource.PlayOneShot(beaconClip, sfxVolume);
            nextBeepTime = Time.time + interval;
        }

        // --- Muri -----------------------------------------------------------------

        private void UpdateWallTouches()
        {
            touchingNow.Clear();
            float r2 = fingerRadius * fingerRadius;
            foreach (var wall in walls)
            {
                if (wall == null) continue;
                foreach (var tip in activeTips)
                {
                    if (tip.y > maxTipHeight) continue; // mano sollevata: nessun contatto
                    var p = new Vector3(tip.x, probeHeight, tip.z);
                    var c = wall.ClosestPoint(p);
                    if ((c - p).sqrMagnitude <= r2) { touchingNow.Add(wall); break; }
                }
            }

            bool touchingAny = touchingNow.Count > 0;
            if (touchingAny) wallContactSeconds += Time.deltaTime;

            if (primeWallState)
            {
                // Primo frame dopo l'ingresso: si registra solo lo stato di partenza.
                primeWallState = false;
            }
            else if (touchingAny && !touchingAnyPrev && Time.time - lastTouchEpisodeTime >= wallTouchCooldown)
            {
                // Nuovo EPISODIO: prima nessun muro, ora almeno uno. Strisciare lungo una
                // parete (anche passando da un segmento al successivo) resta un episodio.
                lastTouchEpisodeTime = Time.time;
                Collider first = null;
                foreach (var w in touchingNow) { first = w; break; }
                RegisterWallTouch(first);
            }

            touchingAnyPrev = touchingAny;
            touchingPrev.Clear();
            foreach (var w in touchingNow) touchingPrev.Add(w);
        }

        private void RegisterWallTouch(Collider wall)
        {
            wallTouches++;
            PlaySfx(wallBumpClip);
            if (wall == null) return;
            var p = wall.transform.position;
            Log("wall_touch", $"{{\"wall\":\"{wall.name}\",\"x\":{F(p.x, "0.000")},\"z\":{F(p.z, "0.000")},\"count\":{wallTouches},\"t\":{F(ElapsedSeconds)}}}");
        }

        // --- Marker ---------------------------------------------------------------

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
                var rend = go.GetComponent<Renderer>();
                if (shader != null)
                {
                    var c = z.GizmoColor; c.a = 0.55f;
                    rend.material = new Material(shader) { color = c };
                }
                markers.Add(go);
            }
        }

        // --- Audio / log ------------------------------------------------------------

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
                if (queue) nm.SpeakQueued(key);
                else nm.Speak(key);
                return;
            }
            if (fallback != null)
            {
                VoiceSubtitles.ReportSaid(VoiceLines.TextOf(key) ?? $"[{key}]", fallback.length);
                PlaySfx(fallback);
            }
            else Debug.LogWarning($"[Labyrinth] Traccia vocale '{key}' mancante: genera le voci con Tools/generate_voice_macos.py.");
        }

        // Numeri nei JSON di log sempre col punto decimale (Windows in italiano userebbe la virgola).
        private static string F(float v, string format = "0.00") => v.ToString(format, CultureInfo.InvariantCulture);

        private void Log(string eventType, string json)
        {
            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            sessionLogger?.Log(levelId, eventType, json);
        }
    }
}
