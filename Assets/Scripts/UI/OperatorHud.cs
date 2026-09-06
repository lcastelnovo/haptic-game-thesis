using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;
using HapticResearch.Levels;
using HapticResearch.Experiment;
using HapticResearch.Hands;

namespace HapticResearch.UI
{
    // HUD dell'OPERATORE VEDENTE, uguale in tutti i livelli (dal mockup grafico):
    //   - sidebar a sinistra: intestazione UniBS, livello e titolo, stato, righe hardware
    //     (middleware WEART, TouchDIVER, calibrazione, Vive Tracker), bottoni Avvia/Ripeti
    //     (+ "livello successivo" a fine livello), footer con i tasti;
    //   - pill in alto a destra: mani demo ON/OFF (cliccabile), id partecipante, timer;
    //   - la barra SENTO/DICO in basso la disegna VoiceSubtitles allineata a questa sidebar.
    // La vista 3D viene spostata a destra della sidebar (viewport delle camere su Display 1)
    // compensando il campo visivo, cosi' l'inquadratura orizzontale resta quella originale
    // e il tavolo resta tutto visibile.
    //
    // E' solo per chi conduce la sessione: il partecipante non vedente non ne ha bisogno.
    // Si auto-installa nelle scene che hanno un LevelController (quindi non nel menu, che
    // ha la sua grafica). I pannelli storici (OperatorControls, toggle demo, indicatore
    // voce, watermark) si spengono da soli quando questo HUD e' attivo e ricompaiono se lo
    // si nasconde con F3.
    //
    // Layout adattivo: se lo schermo e' basso (720p) passa a misure compatte, cosi' il
    // blocco bottoni non sale sopra le righe hardware.
    public class OperatorHud : MonoBehaviour
    {
        public static OperatorHud Instance { get; private set; }
        public static bool Active => Instance != null && Instance.isActiveAndEnabled && Instance.visible && Instance.level != null;
        // Bordo sinistro dell'area 3D (in pixel): gli altri overlay si allineano qui.
        public static float ContentLeft => Active ? Instance.SidebarWidth : 0f;
        // Altezza occupata in alto dalla pill: gli overlay in alto a destra partono sotto.
        public static float TopInset => Active ? Instance.pillBottom : 0f;

        [Header("Layout")]
        [Tooltip("Larghezza della sidebar come frazione dello schermo (entro i limiti in pixel).")]
        [SerializeField, Range(0.18f, 0.4f)] private float sidebarFraction = 0.26f;
        [SerializeField] private float sidebarMinWidth = 300f;
        [SerializeField] private float sidebarMaxWidth = 470f;
        [SerializeField] private float padding = 28f;

        [Tooltip("Restringe il viewport delle camere a destra della sidebar.")]
        [SerializeField] private bool shrinkCameraViewport = true;

        [Tooltip("Allarga il campo visivo delle camere ristrette: l'inquadratura orizzontale resta quella originale (tavolo intero).")]
        [SerializeField] private bool compensateFieldOfView = true;

        [Tooltip("Tasto per nascondere/mostrare l'HUD.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        [Header("Testi")]
        [SerializeField] private string orgName = "UniBS";
        [SerializeField] private string roleLabel = "OPERATORE";
        [Tooltip("Etichetta del tasto che cambia mano (HandInputManager), mostrata nel footer.")]
        [SerializeField] private string handSwitchKeyLabel = "TAB";

        [Header("Vive Tracker")]
        [Tooltip("Un tracker conta come attivo se il suo target si e' mosso di piu' di questa soglia (m) negli ultimi secondi.")]
        [SerializeField] private float trackerMoveThreshold = 0.003f;
        [SerializeField] private float trackerActiveWindow = 3f;

        private bool visible = true;
        private LevelController level;
        private LevelFlowController flow;
        private HandDemoModeController demo;
        private ViveTrackerManager trackers;
        private Texture2D logo;
        private float pillBottom;

        // Stato hardware. Gli handler del SDK possono girare fuori dal main thread: qui si
        // scrivono solo stringhe e int (scritture atomiche); testi e colori si compongono in OnGUI.
        private WeArtController weart;
        private float nextControllerSearch;
        private WeArtClient hookedClient;
        private string devicesText = "n/d";
        private int devicesState;      // 0 nessuno/ignoto, 1 ok, 2 attenzione
        private int calibLeftState;    // 0 assente, 1 in corso, 2 ok, 3 fallita
        private int calibRightState;

        private bool trackerInit;
        private Vector3 lastLeftTrackerPos, lastRightTrackerPos;
        private float lastTrackerMoveTime = float.NegativeInfinity;

        // Camere a cui abbiamo ristretto il viewport (con i valori originali da ripristinare).
        private struct CameraState { public Rect rect; public float fov; public float ortho; }
        private readonly Dictionary<Camera, CameraState> touchedCameras = new Dictionary<Camera, CameraState>();

        private float SidebarWidth => Mathf.Clamp(Screen.width * sidebarFraction, sidebarMinWidth, Mathf.Min(sidebarMaxWidth, Screen.width * 0.5f));

        // --- Auto-install -------------------------------------------------------------

        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!subscribed)
            {
                subscribed = true;
                SceneManager.sceneLoaded += (_, _) => EnsureInstance();
            }
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            // A sceneLoaded l'HUD della scena precedente puo' essere ancora vivo: conta solo
            // se sta nella scena attiva.
            var active = SceneManager.GetActiveScene();
            if (Instance != null && Instance.gameObject.scene == active) return;
            foreach (var existing in FindObjectsByType<OperatorHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing.gameObject.scene != active) continue;
                Instance = existing;
                return;
            }
            var level = LevelController.Find();
            if (level == null || level.gameObject.scene != active) return; // menu o scena senza livello: niente HUD
            new GameObject("OperatorHud").AddComponent<OperatorHud>();
        }

        // --- Ciclo di vita ------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this && Instance.gameObject.scene == gameObject.scene)
            {
                Debug.LogWarning($"[OperatorHud] Doppione su '{gameObject.name}': rimosso.");
                Destroy(this);
                return;
            }
            Instance = this;

            level = LevelController.Find();
            flow = FindFirstObjectByType<LevelFlowController>(FindObjectsInactive.Include);
            demo = FindFirstObjectByType<HandDemoModeController>(FindObjectsInactive.Include);
            trackers = FindFirstObjectByType<ViveTrackerManager>(FindObjectsInactive.Include);
            logo = Resources.Load<Texture2D>("UnibsLogo");
            if (logo == null)
            {
                var branding = FindFirstObjectByType<HapticResearch.Branding.UnibsBranding>(FindObjectsInactive.Include);
                if (branding != null) logo = branding.Logo;
            }
        }

        void OnEnable()
        {
            WeArtStatusTracker.ConnectedDevicesReady += OnDevicesReady;
        }

        void OnDisable()
        {
            WeArtStatusTracker.ConnectedDevicesReady -= OnDevicesReady;
            UnhookClient();
            RestoreCameras();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
                if (!visible) RestoreCameras();
            }
            if (level == null) level = LevelController.Find();

            // WeArtController.Instance cerca in scena quando manca: al massimo una volta al secondo.
            if (weart == null && Time.unscaledTime >= nextControllerSearch)
            {
                weart = WeArtController.Instance;
                nextControllerSearch = Time.unscaledTime + 1f;
            }

            HookClient();
            TrackTrackers();
            if (shrinkCameraViewport && Active) ShrinkCameras();
        }

        // --- Hardware: sola lettura sugli eventi del SDK -------------------------------

        private void HookClient()
        {
            if (weart == null) return;
            var client = weart.Client;
            if (client == null || client == hookedClient) return;
            UnhookClient();
            client.OnMessage += OnClientMessage;
            hookedClient = client;
        }

        private void UnhookClient()
        {
            if (hookedClient == null) return;
            hookedClient.OnMessage -= OnClientMessage;
            hookedClient = null;
        }

        private void OnDevicesReady(ConnectedDevices e)
        {
            if (e == null) { devicesText = "n/d"; devicesState = 0; return; }
            // I guanti si leggono sempre dalla lista: il middleware puo' essere in IDLE o in
            // CALIBRATION (non RUNNING) con i device gia' collegati.
            bool right = false, left = false;
            if (e.Devices != null)
                foreach (var d in e.Devices)
                {
                    if (d == null) continue;
                    if (d.HandSide == HandSide.Right) right = true;
                    else if (d.HandSide == HandSide.Left) left = true;
                }
            string sides = right && left ? "DX + SX" : right ? "DX" : left ? "SX" : "NESSUNO";
            devicesText = e.MiddlewareRunning ? sides : sides + " (avvio)";
            devicesState = right || left ? (e.MiddlewareRunning ? 1 : 2) : 2;
        }

        private void OnClientMessage(WeArtClient.MessageType type, IWeArtMessage message)
        {
            switch (message)
            {
                case TrackingCalibrationStatus s:
                    int st = s.Status == CalibrationStatus.Calibrating ? 1 : s.Status == CalibrationStatus.Running ? 2 : 0;
                    if (st != 0) SetCalib(s.HandSide, st);
                    break;
                case TrackingCalibrationResult r:
                    SetCalib(r.HandSide, r.Success ? 2 : 3);
                    break;
            }
        }

        private void SetCalib(HandSide side, int state)
        {
            if (side == HandSide.Left) calibLeftState = state; else calibRightState = state;
        }

        private static string CalibWord(int state) => state == 1 ? "IN CORSO" : state == 2 ? "OK" : state == 3 ? "FALLITA" : "n/d";
        private static Color CalibColor(int state) => state == 1 ? HudTheme.Warn : state == 2 ? HudTheme.Ok : state == 3 ? HudTheme.Warn : HudTheme.Grey;

        // "DX OK, SX IN CORSO": ogni guanto col suo stato.
        private string CalibText()
        {
            if (calibLeftState == 0 && calibRightState == 0) return HudTheme.Rich(HudTheme.Grey, "n/d");
            string right = calibRightState == 0 ? null : HudTheme.Rich(CalibColor(calibRightState), "DX " + CalibWord(calibRightState));
            string left = calibLeftState == 0 ? null : HudTheme.Rich(CalibColor(calibLeftState), "SX " + CalibWord(calibLeftState));
            return right != null && left != null ? right + ", " + left : right ?? left;
        }

        private void TrackTrackers()
        {
            if (trackers == null) return;
            var lt = trackers.leftTrackerTarget;
            var rt = trackers.rightTrackerTarget;
            if (!trackerInit)
            {
                // Prima lettura: si memorizza la posizione senza contarla come movimento.
                if (lt != null) lastLeftTrackerPos = lt.position;
                if (rt != null) lastRightTrackerPos = rt.position;
                trackerInit = true;
                return;
            }
            float thr = trackerMoveThreshold * trackerMoveThreshold;
            if (lt != null && (lt.position - lastLeftTrackerPos).sqrMagnitude > thr) { lastLeftTrackerPos = lt.position; lastTrackerMoveTime = Time.unscaledTime; }
            if (rt != null && (rt.position - lastRightTrackerPos).sqrMagnitude > thr) { lastRightTrackerPos = rt.position; lastTrackerMoveTime = Time.unscaledTime; }
        }

        private bool MiddlewareConnected() => weart != null && weart.Client != null && weart.Client.IsConnected;

        // --- Viewport camere ----------------------------------------------------------

        private void ShrinkCameras()
        {
            float frac = SidebarWidth / Screen.width;
            var target = new Rect(frac, 0f, 1f - frac, 1f);
            float k = 1f / Mathf.Max(0.05f, 1f - frac); // rapporto di larghezza originale/ristretta
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || cam.targetTexture != null || cam.targetDisplay != 0) continue;
                if (!touchedCameras.TryGetValue(cam, out var orig))
                {
                    orig = new CameraState { rect = cam.rect, fov = cam.fieldOfView, ortho = cam.orthographicSize };
                    touchedCameras[cam] = orig;
                }
                if (cam.rect != target) cam.rect = target;
                if (!compensateFieldOfView) continue;
                // Viewport piu' stretto = stesso FOV verticale, meno FOV orizzontale (taglio ai
                // lati). Si allarga il verticale quanto basta a riavere l'orizzontale originale.
                if (cam.orthographic) cam.orthographicSize = orig.ortho * k;
                else cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(orig.fov * 0.5f * Mathf.Deg2Rad) * k) * Mathf.Rad2Deg;
            }
        }

        private void RestoreCameras()
        {
            foreach (var kv in touchedCameras)
            {
                if (kv.Key == null) continue;
                kv.Key.rect = kv.Value.rect;
                kv.Key.fieldOfView = kv.Value.fov;
                kv.Key.orthographicSize = kv.Value.ortho;
            }
            touchedCameras.Clear();
        }

        // --- Disegno ------------------------------------------------------------------

        // Misure e stili per le due modalita' (normale / compatta per schermi bassi).
        private class StyleSet
        {
            public float logoSize, titleGap, statusMin, rowHeight, primaryH, secondaryH, sectionGap;
            public GUIStyle org, role, levelLabel, title, status, section, rowLabel, rowValue, primary, secondary, footer, logoText;
        }

        private StyleSet normal, compact;
        private GUIStyle pillStyle;
        private Texture2D bgTex, borderTex, panelTex, accentTex, accentHoverTex, pillTex, okTex, greyTex, blueTex, outlineTex, outlineHoverTex;

        void OnGUI()
        {
            if (!Active) return;
            EnsureStyles();

            float w = SidebarWidth;
            float h = Screen.height;
            float x = padding;
            float cw = w - 2f * padding;

            // Scelta misure: se con quelle normali il blocco alto scende sopra quello basso, compatte.
            bool canGoNext = flow != null && flow.CanGoNext;
            bool demoOn = HandDemoModeController.Exists && HandDemoModeController.DemoActive;
            string footer = FooterText(demoOn);
            var set = normal;
            if (NeededHeight(normal, cw, footer, canGoNext) > h) set = compact;

            GUI.DrawTexture(new Rect(0f, 0f, w, h), bgTex);
            GUI.DrawTexture(new Rect(w - 2f, 0f, 2f, h), borderTex);

            // Blocco basso (dal fondo): footer, bottoni.
            var footerContent = new GUIContent(footer);
            float fh = set.footer.CalcHeight(footerContent, cw);
            float by = h - padding - fh;
            GUI.Label(new Rect(x, by, cw, fh), footerContent, set.footer);
            by -= set.sectionGap;

            if (canGoNext)
            {
                by -= set.secondaryH;
                if (GUI.Button(new Rect(x, by, cw, set.secondaryH), $"{flow.NextButtonLabel}  ({flow.NextLevelKey})", set.primary))
                    flow.GoToNextLevel();
                by -= 10f;
            }

            by -= set.secondaryH;
            GUI.enabled = level.IsRunning;
            if (GUI.Button(new Rect(x, by, cw, set.secondaryH), "Ripeti annuncio", set.secondary))
                level.RepeatAnnouncement();
            GUI.enabled = true;
            by -= 10f;

            by -= set.primaryH;
            string startLabel = level.IsRunning ? "Riavvia livello" : level.IsComplete ? "Nuovo partecipante" : "Avvia livello";
            if (GUI.Button(new Rect(x, by, cw, set.primaryH), startLabel, set.primary))
                level.StartLevel();
            float bottomTop = by - set.sectionGap;

            // Blocco alto: intestazione, livello, stato, hardware.
            float y = padding;
            var logoRect = new Rect(x, y, set.logoSize, set.logoSize);
            GUI.DrawTexture(logoRect, panelTex);
            DrawFrame(logoRect);
            if (logo != null) GUI.DrawTexture(new Rect(x + 6f, y + 6f, set.logoSize - 12f, set.logoSize - 12f), logo, ScaleMode.ScaleToFit, true);
            else GUI.Label(logoRect, "UB", set.logoText);
            GUI.Label(new Rect(x + set.logoSize + 16f, y + set.logoSize * 0.14f, cw - set.logoSize - 16f, 26f), orgName, set.org);
            GUI.Label(new Rect(x + set.logoSize + 16f, y + set.logoSize * 0.58f, cw - set.logoSize - 16f, 18f), roleLabel, set.role);
            y += set.logoSize + set.sectionGap * 0.8f;
            GUI.DrawTexture(new Rect(0f, y, w, 1f), borderTex);
            y += set.sectionGap;

            GUI.Label(new Rect(x, y, cw, 18f), $"LIVELLO {level.LevelNumber}", set.levelLabel);
            y += 24f;
            var titleContent = new GUIContent(level.LevelTitle);
            float th = set.title.CalcHeight(titleContent, cw);
            GUI.Label(new Rect(x, y, cw, th), titleContent, set.title);
            y += th + set.titleGap;

            var statusContent = new GUIContent(level.StatusLine);
            float sh = Mathf.Max(set.statusMin, set.status.CalcHeight(statusContent, cw - 44f) + 20f);
            var statusRect = new Rect(x, y, cw, sh);
            GUI.DrawTexture(statusRect, panelTex);
            GUI.DrawTexture(new Rect(x, y, 3f, sh), accentTex);
            var dotTex = level.IsRunning ? okTex : level.IsComplete ? blueTex : greyTex;
            GUI.DrawTexture(new Rect(x + 18f, y + sh * 0.5f - 5f, 10f, 10f), dotTex);
            GUI.Label(new Rect(x + 36f, y + 10f, cw - 44f, sh - 20f), statusContent, set.status);
            y += sh + set.sectionGap;

            GUI.Label(new Rect(x, y, cw, 18f), "HARDWARE", set.section);
            y += 26f;
            bool middleware = MiddlewareConnected();
            HardwareRow(set, ref y, x, cw, "Middleware WEART", HudTheme.Rich(middleware ? HudTheme.Ok : HudTheme.Warn, middleware ? "CONNESSO" : "NON CONNESSO"));
            HardwareRow(set, ref y, x, cw, "TouchDIVER Pro", middleware
                ? HudTheme.Rich(devicesState == 1 ? HudTheme.Ok : devicesState == 2 ? HudTheme.Warn : HudTheme.Grey, devicesText)
                : HudTheme.Rich(HudTheme.Grey, "n/d"));
            HardwareRow(set, ref y, x, cw, "Calibrazione", middleware ? CalibText() : HudTheme.Rich(HudTheme.Grey, "n/d"));
            string trk; Color trkColor;
            if (trackers == null) { trk = "ASSENTE"; trkColor = HudTheme.Grey; }
            else if (Time.unscaledTime - lastTrackerMoveTime < trackerActiveWindow) { trk = "ATTIVO"; trkColor = HudTheme.Ok; }
            else { trk = "NON USATO"; trkColor = HudTheme.Grey; }
            HardwareRow(set, ref y, x, cw, "Vive Tracker", HudTheme.Rich(trkColor, trk));

            if (y > bottomTop)
                Debug.LogWarningFormat("[OperatorHud] Schermo troppo basso ({0}px): la sidebar si sovrappone.", Screen.height);

            DrawPill(demoOn);
        }

        // Altezza totale richiesta da sidebar completa con queste misure (per scegliere le compatte).
        private float NeededHeight(StyleSet set, float cw, string footer, bool canGoNext)
        {
            float top = padding + set.logoSize + set.sectionGap * 0.8f + 1f + set.sectionGap + 24f
                + set.title.CalcHeight(new GUIContent(level.LevelTitle), cw) + set.titleGap
                + Mathf.Max(set.statusMin, set.status.CalcHeight(new GUIContent(level.StatusLine), cw - 44f) + 20f) + set.sectionGap
                + 26f + 4f * set.rowHeight;
            float bottom = padding + set.footer.CalcHeight(new GUIContent(footer), cw) + set.sectionGap
                + (canGoNext ? set.secondaryH + 10f : 0f) + set.secondaryH + 10f + set.primaryH + set.sectionGap;
            return top + bottom;
        }

        private string FooterText(bool demoOn)
        {
            string text = $"F1 diagnostica | F2 sottotitoli | F3 HUD\n{handSwitchKeyLabel} switch mano | M muta voce | R ripeti";
            if (demoOn) text += "\nDemo: mouse muove | click chiude | G afferra | Q/E su/giu'";
            return text;
        }

        private void HardwareRow(StyleSet set, ref float y, float x, float cw, string label, string richValue)
        {
            float rh = set.rowHeight;
            var value = new GUIContent(richValue);
            float vw = set.rowValue.CalcSize(value).x;
            GUI.Label(new Rect(x, y, Mathf.Max(40f, cw - vw - 8f), rh), label, set.rowLabel);
            GUI.Label(new Rect(x, y, cw, rh), value, set.rowValue);
            GUI.DrawTexture(new Rect(x, y + rh - 1f, cw, 1f), borderTex);
            y += rh;
        }

        private void DrawFrame(Rect r)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), borderTex);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), borderTex);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), borderTex);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), borderTex);
        }

        // Pill in alto a destra: stato demo (cliccabile), id partecipante, timer del livello.
        private void DrawPill(bool demoOn)
        {
            bool demoAvailable = demo != null && demo.isActiveAndEnabled;
            string demoLabel = !demoAvailable ? "MANI DEMO n/d" : demoOn ? "MANI DEMO ON" : "MANI DEMO OFF";
            string pid = SessionLogger.Instance != null ? SessionLogger.Instance.ParticipantId : "n/d";
            int secs = Mathf.FloorToInt(level.ElapsedSeconds);
            string timer = $"{secs / 60:00}:{secs % 60:00}";
            string dot = HudTheme.Rich(demoOn ? HudTheme.Ok : HudTheme.Grey, "●");
            var content = new GUIContent($"{dot}  {demoLabel}  |  {pid}  |  {timer}");

            var size = pillStyle.CalcSize(content);
            var rect = new Rect(Screen.width - size.x - 40f - 24f, 24f, size.x + 40f, size.y + 20f);
            pillBottom = rect.yMax;
            GUI.DrawTexture(rect, pillTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), borderTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderTex);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 10f, size.x, size.y), content, pillStyle);

            // Click sulla pill = toggle demo (stessa cosa del vecchio pannello).
            if (demoAvailable && GUI.Button(rect, GUIContent.none, GUIStyle.none))
                demo.SetDemoActive(!demoOn);
        }

        private void EnsureStyles()
        {
            if (normal != null) return;

            bgTex = HudTheme.Solid(HudTheme.Bg);
            borderTex = HudTheme.Solid(HudTheme.Border);
            panelTex = HudTheme.Solid(HudTheme.Panel);
            accentTex = HudTheme.Solid(HudTheme.Accent);
            accentHoverTex = HudTheme.Solid(HudTheme.AccentLight);
            pillTex = HudTheme.Solid(new Color(HudTheme.Bg.r, HudTheme.Bg.g, HudTheme.Bg.b, 0.92f));
            okTex = HudTheme.Solid(HudTheme.Ok);
            greyTex = HudTheme.Solid(HudTheme.Grey);
            blueTex = HudTheme.Solid(HudTheme.AccentLight);
            outlineTex = HudTheme.Solid(HudTheme.Panel);
            outlineHoverTex = HudTheme.Solid(HudTheme.Border);

            pillStyle = HudTheme.Label(HudTheme.Mono(16), 16, HudTheme.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
            normal = BuildStyles(false);
            compact = BuildStyles(true);
        }

        private StyleSet BuildStyles(bool small)
        {
            var s = new StyleSet
            {
                logoSize = small ? 48f : 64f,
                titleGap = small ? 12f : 18f,
                statusMin = small ? 42f : 52f,
                rowHeight = small ? 34f : 44f,
                primaryH = small ? 50f : 64f,
                secondaryH = small ? 42f : 56f,
                sectionGap = small ? 16f : 28f,
            };
            int title = small ? 28 : 38, status = small ? 16 : 19, rowL = small ? 15 : 18, rowV = small ? 13 : 15, foot = small ? 11 : 12;
            s.org = HudTheme.Label(HudTheme.Sans(small ? 19 : 22), small ? 19 : 22, HudTheme.Text, FontStyle.Bold);
            s.role = HudTheme.Label(HudTheme.Mono(12), 12, HudTheme.AccentLight);
            s.logoText = HudTheme.Label(HudTheme.Serif(small ? 20 : 26), small ? 20 : 26, HudTheme.Text, FontStyle.Normal, TextAnchor.MiddleCenter);
            s.levelLabel = HudTheme.Label(HudTheme.Mono(13), 13, HudTheme.AccentLight);
            s.title = HudTheme.Label(HudTheme.Serif(title), title, HudTheme.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
            s.status = HudTheme.Label(HudTheme.Sans(status), status, HudTheme.Text, FontStyle.Normal, TextAnchor.MiddleLeft, true);
            s.section = HudTheme.Label(HudTheme.Mono(12), 12, HudTheme.Muted);
            s.rowLabel = HudTheme.Label(HudTheme.Sans(rowL), rowL, HudTheme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            s.rowValue = HudTheme.Label(HudTheme.Mono(rowV), rowV, HudTheme.Text, FontStyle.Bold, TextAnchor.MiddleRight);
            s.footer = HudTheme.Label(HudTheme.Mono(foot), foot, HudTheme.Muted, FontStyle.Normal, TextAnchor.UpperLeft, true);

            s.primary = new GUIStyle(GUI.skin.button)
            {
                fontSize = small ? 18 : 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0),
                normal = { background = accentTex, textColor = HudTheme.Text },
                hover = { background = accentHoverTex, textColor = HudTheme.Text },
                active = { background = accentHoverTex, textColor = HudTheme.Text },
                focused = { background = accentTex, textColor = HudTheme.Text }
            };
            var sans = HudTheme.Sans(small ? 18 : 22);
            if (sans != null) s.primary.font = sans;

            s.secondary = new GUIStyle(s.primary)
            {
                fontSize = small ? 16 : 19,
                fontStyle = FontStyle.Normal,
                normal = { background = outlineTex, textColor = HudTheme.Text },
                hover = { background = outlineHoverTex, textColor = HudTheme.Text },
                active = { background = outlineHoverTex, textColor = HudTheme.Text },
                focused = { background = outlineTex, textColor = HudTheme.Text }
            };
            var sans2 = HudTheme.Sans(small ? 16 : 19);
            if (sans2 != null) s.secondary.font = sans2;
            return s;
        }
    }
}
