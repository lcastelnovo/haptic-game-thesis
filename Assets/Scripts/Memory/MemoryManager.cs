using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using WeArt.Core;
using HapticResearch.Audio;
using HapticResearch.Experiment;
using HapticResearch.Exploration;   // ThermalObjectCue, ThermalRole
using HapticResearch.Labyrinth;     // HapticProfile: la taratura del partecipante e' del Level 2
using HapticResearch.Levels;
using HapticResearch.UI;
using HapticResearch.Voice;

namespace HapticResearch.Memory
{
    // Level 3, alternativa alla colazione: memory tattile.
    //
    // Le sensazioni del guanto qui non imitano niente: sono un codice, come il braille.
    // Nessuno si aspetta che una tessera "sembri" qualcosa; basta che le firme si
    // distinguano e che il partecipante se le ricordi.
    //
    // Due fasi, stesso gesto (dito fermo sulla tessera = girarla):
    //  1. riscaldamento: tre coppie gia' scoperte. Un errore qui e' percezione pura.
    //  2. memory: sei coppie coperte. Bisogna ricordare DOVE si e' sentito cosa.
    //
    // La logica sta in classi pure (MemoryGridMap, DwellDetector, MemoryBoard) provate da
    // Tools/MemoryTest. Qui ci sono solo dito, suoni, voce, guanto e log.
    public class MemoryManager : LevelController
    {
        private enum State { Idle, Warmup, Memory, Finished }

        [Header("Identita' (HUD operatore)")]
        [SerializeField] private int levelNumber = 3;
        [SerializeField] private string levelTitle = "Memory tattile";
        [SerializeField] private string levelId = "level3_memory";

        [Header("Dati")]
        [Tooltip("Griglia, firme, tempi. Per una variante si duplica l'asset.")]
        [SerializeField] private MemoryLayoutAsset layout;

        [Tooltip("Taratura del partecipante (mano attuata; caldo/freddo per la variante termica). Se vuoto si usano i default.")]
        [SerializeField] private HapticProfile profile;

        [Header("Scena")]
        [Tooltip("Root 'Memory' generato dal builder: il suo Transform E' il sistema del partecipante.")]
        [SerializeField] private Transform gridRoot;

        [Tooltip("Se vuota si riempie da sola con tutte le MemoryTile della scena.")]
        [SerializeField] private List<MemoryTile> tiles = new List<MemoryTile>();

        [Header("Rilevamento")]
        [Tooltip("Sopra questa altezza la mano e' sollevata dal tavolo e non tocca niente.")]
        [SerializeField] private float maxTipHeight = 0.97f;

        [Header("Canale termico (solo layout con firme termiche)")]
        [Tooltip("Se vuoto viene aggiunto a questo GameObject.")]
        [SerializeField] private ThermalObjectCue thermalCue;

        [Header("Suoni (riusati dal Level 2: stesso significato, stesso suono)")]
        [Tooltip("Entrata su una tessera: Assets/Audio/Level2/wall_bump, piu' piano.")]
        [SerializeField] private AudioClip contactClip;
        [SerializeField, Range(0f, 1f)] private float contactVolume = 0.5f;
        [Tooltip("Tono della sosta, sale col progresso: dwell_tone.")]
        [SerializeField] private AudioClip dwellToneClip;
        [Tooltip("Tessera girata: reading_ready.")]
        [SerializeField] private AudioClip readyClip;
        [Tooltip("Coppia trovata: checkpoint_chime.")]
        [SerializeField] private AudioClip pairClip;
        [Tooltip("Coppia sbagliata: branch_down.")]
        [SerializeField] private AudioClip mismatchClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Controlli operatore")]
        [SerializeField] private KeyCode startKey = KeyCode.Return;
        [SerializeField] private KeyCode repeatKey = KeyCode.R;
        [Tooltip("Chiude il livello in anticipo, come nella colazione.")]
        [SerializeField] private KeyCode finishKey = KeyCode.End;
        [Tooltip("Gira la tessera sotto il dito: fallback quando la sosta non riesce.")]
        [SerializeField] private KeyCode flipKey = KeyCode.G;
        [SerializeField] private bool autoStart = false;

        [Header("Comandi vocali del partecipante")]
        [Tooltip("Se vuoto lo cerca in scena.")]
        [SerializeField] private VoiceCommandController voiceCommands;
        [SerializeField] private string[] missingPhrases = { "quante coppie mancano", "quante ne mancano", "cosa manca" };

        [Header("Traccia del dito")]
        [Tooltip("Posizione del dito nel CSV, in coordinate del partecipante (x destra, z lontano).")]
        [SerializeField] private bool logProbeTrace = true;
        [SerializeField, Range(1f, 30f)] private float probeHz = 10f;

        [Header("Logging")]
        [SerializeField] private SessionLogger sessionLogger;

        private State state = State.Idle;
        private MemoryBoard board;
        private MemoryGridMap grid;
        private DwellDetector dwell;
        private FingerProbeSource probes;
        private MemoryTile[] tileByIndex = new MemoryTile[0];
        private AudioSource sfxSource, toneSource;

        private int currentTile = MemoryGridMap.Outside;
        private float currentSince;
        private string pendingVia = "sosta";
        private int seed;
        private int warmupAttempts;
        private float levelStartTime = -1f, levelEndTime = -1f, phaseStartTime;
        private float probeAccumulator;

        public MemoryLayoutAsset Layout => layout;

        public override string LevelId => levelId;
        public override int LevelNumber => levelNumber;
        public override string LevelTitle => levelTitle;
        public override bool IsRunning => state == State.Warmup || state == State.Memory;
        public override bool IsComplete => state == State.Finished;

        private int Phase => state == State.Warmup ? 1 : 2;

        public override string StatusLine
        {
            get
            {
                switch (state)
                {
                    case State.Idle: return "in attesa di avvio";
                    case State.Finished:
                        return board == null ? "finito"
                            : $"finito - {board.PairsFound}/{board.PairsTotal} coppie - {board.Attempts} tentativi";
                    default:
                        return $"memory - fase {Phase} - {board.PairsFound}/{board.PairsTotal} coppie - " +
                               $"{board.Attempts} tentativi - dito su: {TileLabel(currentTile)}";
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

            sfxSource = NewSource(false);
            toneSource = NewSource(true);

            // Una mano sola, quella del profilo: con entrambe, una mano ferma sul tavolo
            // vincerebbe ogni confronto e quella che esplora diventerebbe invisibile.
            probes = new FingerProbeSource();
            probes.SideFilter =
                (profile.ActuatesLeft ? HandSideFlags.Left : HandSideFlags.None) |
                (profile.ActuatesRight ? HandSideFlags.Right : HandSideFlags.None);

            if (thermalCue == null) thermalCue = GetComponent<ThermalObjectCue>();
            if (thermalCue == null) thermalCue = gameObject.AddComponent<ThermalObjectCue>();
            thermalCue.Configure(profile);
            thermalCue.OnArmed += (id, role) =>
                Log("thermal_armed", $"{{\"id\":\"{id}\",\"role\":\"{role.ToString().ToLowerInvariant()}\"}}");
            thermalCue.OnDelivered += (id, role) =>
                Log("thermal_delivered", $"{{\"id\":\"{id}\",\"role\":\"{role.ToString().ToLowerInvariant()}\"}}");
            thermalCue.OnReleased += (id, held, delivered) =>
                Log("thermal_released", $"{{\"id\":\"{id}\",\"seconds\":{F(held)},\"delivered\":{(delivered ? "true" : "false")}}}");
            // Senza questa riga, in analisi resterebbe un partecipante che "non ha sentito
            // il freddo" quando il freddo non gli e' stato mandato.
            thermalCue.OnSuppressed += (id, reason) =>
                Log("thermal_suppressed", $"{{\"id\":\"{id}\",\"reason\":\"{reason}\"}}");

            IndexTiles();

            if (voiceCommands == null)
                voiceCommands = FindFirstObjectByType<VoiceCommandController>(FindObjectsInactive.Include);
            if (voiceCommands != null) voiceCommands.RegisterCommand(missingPhrases, SayMissing);
        }

        protected virtual void Start()
        {
            // Prima dell'avvio tutte le tessere sono coperte: chi appoggia la mano sente la
            // griglia, ma nessuna firma.
            RefreshTiles();
            if (autoStart) StartLevel();
        }

        public override void StartLevel()
        {
            // "Nuovo partecipante" (Invio) a canale armato: il guanto torna neutro.
            thermalCue?.ResetChannel();

            if (layout == null) { Debug.LogError("[Level3M] Manca il MemoryLayoutAsset: impossibile avviare."); return; }
            if (!layout.Validate(out string error))
            {
                Debug.LogError($"[Level3M] Il layout '{layout.LayoutId}' non e' valido: {error}");
                return;
            }
            if (gridRoot == null) { Debug.LogError("[Level3M] Manca il root 'Memory': rigenera la griglia dal builder."); return; }

            grid = layout.CreateGrid();
            if (tileByIndex.Length != grid.TileCount || Array.IndexOf(tileByIndex, null) >= 0)
            {
                Debug.LogError($"[Level3M] In scena ci sono {tiles.Count} tessere, il layout ne vuole {grid.TileCount}: " +
                               "rigenera la griglia con 'HapticResearch/Level 3 Memory/Rigenera griglia'.");
                return;
            }

            dwell = new DwellDetector(layout.DwellSeconds, layout.DwellRadius, layout.DwellQuietSeconds);
            dwell.Started += t => StartTone();
            dwell.Cancelled += (t, reached, reason) =>
            {
                StopTone();
                Log("dwell_cancel", $"{{\"tile\":{t},\"ms\":{Mathf.RoundToInt(reached * 1000f)},\"reason\":\"{reason}\"}}");
            };
            dwell.Completed += t =>
            {
                StopTone();
                PlaySfx(readyClip, sfxVolume);
                Flip(t, "sosta");
            };

            seed = Environment.TickCount & 0x7fffffff;
            levelStartTime = Time.time;
            levelEndTime = -1f;
            warmupAttempts = 0;
            probes.Invalidate();
            currentTile = MemoryGridMap.Outside;
            probeAccumulator = 0f;

            Log("level_start",
                $"{{\"layout\":\"{layout.LayoutId}\",\"seed\":{seed},\"hand\":\"{profile.ActuatedHandName()}\"," +
                $"\"profile\":\"{ProfileName()}\",\"thermal\":{(layout.HasThermal ? "true" : "false")}}}");
            BeginPhase(1);
        }

        public override void RepeatAnnouncement()
        {
            switch (state)
            {
                case State.Idle:
                case State.Warmup: Voice("level3m_intro"); break;
                case State.Memory: Voice("level3m_phase2"); break;
                case State.Finished: Voice("level3m_end_hint"); break;
            }
        }

        // Chiusura anticipata (tasto Fine dell'operatore).
        public void Finish(string reason)
        {
            if (!IsRunning) return;
            EndLevel(reason);
            Voice("level3m_end_hint");
        }

        protected virtual void Update()
        {
            if (Input.GetKeyDown(startKey) && !IsRunning) StartLevel();
            if (Input.GetKeyDown(repeatKey)) RepeatAnnouncement();
            if (Input.GetKeyDown(finishKey)) Finish("operatore");
            if (!IsRunning) return;

            float dt = Time.deltaTime;
            board.Tick(dt);

            probes.Refresh();
            int tile = MemoryGridMap.Outside;
            float lx = 0f, lz = 0f;
            if (probes.TryLowestTip(out var tip) && tip.y <= maxTipHeight)
            {
                var local = gridRoot.InverseTransformPoint(tip);
                lx = local.x;
                lz = local.z;
                tile = grid.Locate(lx, lz);
            }

            UpdateContact(tile);
            dwell.Update(board.CanFlip(tile) ? tile : -1, lx, lz, dt);
            if (dwell.Running && toneSource.isPlaying) toneSource.pitch = Mathf.Lerp(1f, 2f, dwell.Progress01);

            if (Input.GetKeyDown(flipKey) && board.CanFlip(tile))
            {
                dwell.Reset();
                StopTone();
                PlaySfx(readyClip, sfxVolume);
                Flip(tile, "operatore");
            }

            UpdateThermal(tile, dt);
            LogProbeTrace(dt, tile, lx, lz);
        }

        // --- Fasi -----------------------------------------------------------------------

        private void BeginPhase(int phase)
        {
            state = phase == 1 ? State.Warmup : State.Memory;

            var playing = new List<int>();
            if (phase == 1) playing.AddRange(layout.WarmupTiles);
            else for (int t = 0; t < grid.TileCount; t++) playing.Add(t);
            var signatures = phase == 1 ? layout.WarmupSignatureIndices() : layout.AllSignatureIndices();

            board = new MemoryBoard(grid.TileCount, playing, signatures, open: phase == 1,
                                    layout.MismatchDelay, seed + phase);
            board.Flipped += HandleFlipped;
            board.Matched += HandleMatched;
            board.Mismatched += HandleMismatched;
            board.Covered += (a, b) => { RefreshTile(a); RefreshTile(b); };
            board.Completed += HandleCompleted;

            dwell.Reset();
            StopTone();
            phaseStartTime = Time.time;
            RefreshTiles();

            var sb = new StringBuilder();
            sb.Append($"{{\"phase\":{phase},\"seed\":{seed + phase},\"tiles\":[");
            bool firstItem = true;
            for (int t = 0; t < board.TileCount; t++)
            {
                if (board.SignatureOf(t) < 0) continue;
                if (!firstItem) sb.Append(',');
                firstItem = false;
                sb.Append($"{{\"t\":{t},\"sig\":\"{layout.Signatures[board.SignatureOf(t)].Id}\"}}");
            }
            sb.Append("]}");
            Log("phase_start", sb.ToString());

            // La fase 2 parte subito dopo "Coppia!": si accoda, non la tronca.
            if (phase == 1) Voice("level3m_intro");
            else VoiceQueued("level3m_phase2");
        }

        private void EndLevel(string reason)
        {
            int pairs = board != null ? board.PairsFound : 0;
            int total = board != null ? board.PairsTotal : 0;
            int attempts = board != null ? board.Attempts : 0;
            bool inWarmup = state == State.Warmup;

            state = State.Finished;
            levelEndTime = Time.time;
            dwell?.Reset();
            StopTone();
            thermalCue?.ResetChannel();

            Log("level_end",
                $"{{\"phase\":{(inWarmup ? 1 : 2)},\"pairs\":{pairs},\"total\":{total}," +
                $"\"warmupAttempts\":{(inWarmup ? attempts : warmupAttempts)},\"memoryAttempts\":{(inWarmup ? 0 : attempts)}," +
                $"\"seconds\":{F(ElapsedSeconds)},\"reason\":\"{reason}\"}}");
        }

        // --- Eventi della partita -------------------------------------------------------

        private void Flip(int tile, string via)
        {
            pendingVia = via;
            board.Flip(tile);
        }

        private void HandleFlipped(int t)
        {
            RefreshTile(t);
            Log("flip",
                $"{{\"phase\":{Phase},\"turn\":{board.Turn},\"tile\":{t},\"sig\":\"{SigId(t)}\"," +
                $"\"via\":\"{pendingVia}\",\"seconds\":{F(Time.time - phaseStartTime)}}}");
        }

        private void HandleMatched(int a, int b)
        {
            RefreshTile(a);
            RefreshTile(b);
            PlaySfx(pairClip, sfxVolume);
            Voice("level3m_pair");
            Log("match",
                $"{{\"phase\":{Phase},\"turn\":{board.Attempts},\"a\":{a},\"b\":{b},\"sig\":\"{SigId(a)}\"}}");
        }

        private void HandleMismatched(MismatchInfo m)
        {
            PlaySfx(mismatchClip, sfxVolume);
            Log("mismatch",
                $"{{\"phase\":{Phase},\"turn\":{board.Attempts},\"a\":{m.First},\"b\":{m.Second}," +
                $"\"sigA\":\"{SigId(m.First)}\",\"sigB\":\"{SigId(m.Second)}\"," +
                $"\"partnerSeenBefore\":{B(m.PartnerSeenBefore)},\"secondSeenBefore\":{B(m.SecondSeenBefore)}}}");
        }

        private void HandleCompleted()
        {
            Log("phase_end",
                $"{{\"phase\":{Phase},\"attempts\":{board.Attempts},\"seconds\":{F(Time.time - phaseStartTime)}}}");

            if (state == State.Warmup)
            {
                warmupAttempts = board.Attempts;
                BeginPhase(2);
                return;
            }

            EndLevel("completato");
            VoiceQueued("level3m_complete");
            VoiceQueued("level3m_end_hint");
        }

        // --- Dito, tessere, guanto ------------------------------------------------------

        private void UpdateContact(int tile)
        {
            if (tile == currentTile) return;

            if (currentTile >= 0)
                Log("tile_exit",
                    $"{{\"tile\":{currentTile},\"state\":\"{StateName(currentTile)}\",\"ms\":{Mathf.RoundToInt((Time.time - currentSince) * 1000f)}}}");

            currentTile = tile;
            currentSince = Time.time;
            if (tile < 0) return;

            // Il colpetto segna il bordo di una tessera in gioco. Su una fuori gioco niente:
            // al tatto e' tavolo, e il suono direbbe il contrario.
            var s = board.StateOf(tile);
            if (s == TileState.Hidden || s == TileState.Flipped) PlaySfx(contactClip, contactVolume);
            Log("tile_enter", $"{{\"tile\":{tile},\"state\":\"{StateName(tile)}\"}}");
        }

        private void UpdateThermal(int tile, float dt)
        {
            if (!layout.HasThermal) return;

            // L'id e' quello della FIRMA: passare fra le due tessere di una coppia termica
            // non deve ri-armare ne' contare come soppressione.
            string id = null;
            ThermalRole role = ThermalRole.Neutral;
            if (tile >= 0 && board.FeelsSignature(tile))
            {
                var sig = layout.Signatures[board.SignatureOf(tile)];
                if (sig.Role != ThermalRole.Neutral) { id = sig.Id; role = sig.Role; }
            }
            thermalCue.SetTarget(id, role);
            thermalCue.Tick(dt);
        }

        private void IndexTiles()
        {
            if (tiles.Count == 0) tiles.AddRange(FindObjectsByType<MemoryTile>(FindObjectsSortMode.None));
            tiles.RemoveAll(t => t == null);

            int max = -1;
            foreach (var t in tiles) max = Mathf.Max(max, t.Index);
            tileByIndex = new MemoryTile[max + 1];
            foreach (var t in tiles)
            {
                if (t.Index < 0) continue;
                if (tileByIndex[t.Index] != null)
                    Debug.LogError($"[Level3M] Due tessere con indice {t.Index}: rigenera la griglia.", t);
                tileByIndex[t.Index] = t;
            }
        }

        private void RefreshTiles()
        {
            for (int t = 0; t < tileByIndex.Length; t++) RefreshTile(t);
        }

        private void RefreshTile(int t)
        {
            if (t < 0 || t >= tileByIndex.Length || tileByIndex[t] == null || layout == null) return;

            TileFeel feel;
            MemoryLayoutAsset.Signature sig = null;
            if (board == null) feel = TileFeel.Covered;
            else
            {
                var s = board.StateOf(t);
                if (s == TileState.Absent || s == TileState.Matched) feel = TileFeel.Off;
                else if (board.FeelsSignature(t)) { feel = TileFeel.Signature; sig = layout.Signatures[board.SignatureOf(t)]; }
                else feel = TileFeel.Covered;
            }
            tileByIndex[t].Apply(feel, sig, layout.CoveredStiffness);
        }

        private void LogProbeTrace(float dt, int tile, float lx, float lz)
        {
            if (!logProbeTrace || probeHz <= 0f) return;
            probeAccumulator += dt;
            float period = 1f / probeHz;
            if (probeAccumulator < period) return;
            probeAccumulator -= period;   // sottrarre, non azzerare: frequenza stabile a ogni frame rate
            if (tile == MemoryGridMap.Outside && !probes.Any) return;
            Log("probe", $"{{\"x\":{F(lx, "0.000")},\"z\":{F(lz, "0.000")},\"tile\":{tile}}}");
        }

        // --- Voce del partecipante ------------------------------------------------------

        // Dice QUANTE coppie mancano nella fase in corso, mai dove.
        private void SayMissing()
        {
            if (!IsRunning) return;
            int missing = Mathf.Max(0, board.PairsTotal - board.PairsFound);
            Voice($"level3m_missing_{Mathf.Min(missing, 6)}");
            VoiceSubtitles.ReportHeard("quante coppie mancano", "alta", true, $"ne mancano {missing}");
            Log("voice_query", $"{{\"query\":\"missing\",\"phase\":{Phase},\"missing\":{missing}}}");
        }

        // --- Util -----------------------------------------------------------------------

        private string TileLabel(int t)
        {
            if (t == MemoryGridMap.Outside) return "fuori griglia";
            if (t == MemoryGridMap.Gap) return "fra due tessere";
            return $"T{t} ({StateName(t)})";
        }

        private string StateName(int t)
        {
            switch (board.StateOf(t))
            {
                case TileState.Hidden: return board.Open ? "scoperta" : "coperta";
                case TileState.Flipped: return "girata";
                default: return "fuori gioco";
            }
        }

        private string SigId(int t) =>
            board.SignatureOf(t) >= 0 ? layout.Signatures[board.SignatureOf(t)].Id : "";

        private string ProfileName() =>
            profile == null || string.IsNullOrEmpty(profile.name) ? "default" : profile.name;

        private AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.spatialBlend = 0f;   // 2D: si sente sempre
            s.playOnAwake = false;
            s.loop = loop;
            return s;
        }

        private void StartTone()
        {
            if (dwellToneClip == null) return;
            toneSource.clip = dwellToneClip;
            toneSource.pitch = 1f;
            toneSource.volume = sfxVolume;
            if (!toneSource.isPlaying) toneSource.Play();
        }

        private void StopTone()
        {
            if (toneSource == null) return;
            toneSource.pitch = 1f;
            if (toneSource.isPlaying) toneSource.Stop();
        }

        private void PlaySfx(AudioClip clip, float volume)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, volume);
        }

        // Battuta pre-generata. Se manca lo dice invece di tacere: in un gioco per non
        // vedenti una voce assente e' un pezzo di interfaccia assente.
        private void Voice(string key)
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(key)) { nm.Speak(key); return; }
            Debug.LogWarning($"[Level3M] Traccia vocale '{key}' mancante: genera le voci con Tools/generate_voice_macos.py.");
            VoiceSubtitles.ReportSaid(VoiceLines.TextOf(key) ?? $"[{key}]", 1.5f);
        }

        private void VoiceQueued(string key)
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(key)) { nm.SpeakQueued(key); return; }
            Debug.LogWarning($"[Level3M] Traccia vocale '{key}' mancante: genera le voci con Tools/generate_voice_macos.py.");
            VoiceSubtitles.ReportSaid(VoiceLines.TextOf(key) ?? $"[{key}]", 1.5f);
        }

        private static string F(float v, string format = "0.00") => v.ToString(format, CultureInfo.InvariantCulture);
        private static string B(bool v) => v ? "true" : "false";

        private void Log(string eventType, string json)
        {
            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            sessionLogger?.Log(levelId, eventType, json);
        }
    }
}
