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
    // La vista 3D viene spostata a destra della sidebar (viewport delle camere su Display 1),
    // cosi' il tavolo resta tutto visibile.
    //
    // E' solo per chi conduce la sessione: il partecipante non vedente non ne ha bisogno.
    // Si auto-installa nelle scene che hanno un LevelController (quindi non nel menu, che
    // ha la sua grafica). I pannelli storici (OperatorControls, toggle demo, indicatore
    // voce, watermark) si spengono da soli quando questo HUD e' attivo.
    public class OperatorHud : MonoBehaviour
    {
        public static OperatorHud Instance { get; private set; }
        public static bool Active => Instance != null && Instance.isActiveAndEnabled && Instance.visible && Instance.level != null;
        // Bordo sinistro dell'area 3D (in pixel): gli altri overlay si allineano qui.
        public static float ContentLeft => Active ? Instance.SidebarWidth : 0f;

        [Header("Layout")]
        [Tooltip("Larghezza della sidebar come frazione dello schermo (entro i limiti in pixel).")]
        [SerializeField, Range(0.18f, 0.4f)] private float sidebarFraction = 0.26f;
        [SerializeField] private float sidebarMinWidth = 300f;
        [SerializeField] private float sidebarMaxWidth = 470f;
        [SerializeField] private float padding = 28f;

        [Tooltip("Restringe il viewport delle camere a destra della sidebar (il tavolo resta tutto visibile).")]
        [SerializeField] private bool shrinkCameraViewport = true;

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

        // Stato hardware (aggiornato da eventi del SDK, sola lettura).
        private string devicesText = "—";
        private Color devicesColor;
        private string calibText = "—";
        private Color calibColor;
        private WeArtClient hookedClient;

        private Vector3 lastLeftTrackerPos, lastRightTrackerPos;
        private float lastTrackerMoveTime = float.NegativeInfinity;

        // Camere a cui abbiamo ristretto il viewport (da ripristinare).
        private readonly Dictionary<Camera, Rect> touchedCameras = new Dictionary<Camera, Rect>();

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
            if (Instance != null) return;
            var existing = FindFirstObjectByType<OperatorHud>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return; }
            if (LevelController.Find() == null) return; // menu o scena senza livello: niente HUD
            new GameObject("OperatorHud").AddComponent<OperatorHud>();
        }

        // --- Ciclo di vita ------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this)
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

            devicesColor = HudTheme.Grey;
            calibColor = HudTheme.Grey;
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

            HookClient();
            TrackTrackers();
            if (shrinkCameraViewport && Active) ShrinkCameras();
        }

        // --- Hardware: sola lettura sugli eventi del SDK -------------------------------

        private void HookClient()
        {
            var controller = WeArtController.Instance;
            if (controller == null) return;
            var client = controller.Client;
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
            if (e == null || !e.MiddlewareRunning) { devicesText = "—"; devicesColor = HudTheme.Grey; return; }
            bool right = false, left = false;
            if (e.Devices != null)
                foreach (var d in e.Devices)
                {
                    if (d == null) continue;
                    if (d.HandSide == HandSide.Right) right = true;
                    else if (d.HandSide == HandSide.Left) left = true;
                }
            devicesText = right && left ? "DX + SX" : right ? "DX" : left ? "SX" : "NESSUNO";
            devicesColor = right || left ? HudTheme.Ok : HudTheme.Warn;
        }

        // Solo assegnazioni di stringhe: l'evento potrebbe non arrivare sul main thread.
        private void OnClientMessage(WeArtClient.MessageType type, IWeArtMessage message)
        {
            switch (message)
            {
                case TrackingCalibrationStatus s:
                    if (s.Status == CalibrationStatus.Calibrating) { calibText = $"IN CORSO ({Side(s.HandSide)})"; calibColor = HudTheme.Warn; }
                    else if (s.Status == CalibrationStatus.Running) { calibText = "OK"; calibColor = HudTheme.Ok; }
                    break;
                case TrackingCalibrationResult r:
                    calibText = r.Success ? $"OK ({Side(r.HandSide)})" : $"FALLITA ({Side(r.HandSide)})";
                    calibColor = r.Success ? HudTheme.Ok : HudTheme.Warn;
                    break;
            }
        }

        private static string Side(HandSide side) => side == HandSide.Left ? "SX" : "DX";

        private void TrackTrackers()
        {
            if (trackers == null) return;
            float thr = trackerMoveThreshold * trackerMoveThreshold;
            if (trackers.leftTrackerTarget != null)
            {
                var p = trackers.leftTrackerTarget.position;
                if ((p - lastLeftTrackerPos).sqrMagnitude > thr) { lastLeftTrackerPos = p; lastTrackerMoveTime = Time.unscaledTime; }
            }
            if (trackers.rightTrackerTarget != null)
            {
                var p = trackers.rightTrackerTarget.position;
                if ((p - lastRightTrackerPos).sqrMagnitude > thr) { lastRightTrackerPos = p; lastTrackerMoveTime = Time.unscaledTime; }
            }
        }

        // --- Viewport camere ----------------------------------------------------------

        private void ShrinkCameras()
        {
            float frac = SidebarWidth / Screen.width;
            var target = new Rect(frac, 0f, 1f - frac, 1f);
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || cam.targetTexture != null || cam.targetDisplay != 0) continue;
                if (!touchedCameras.ContainsKey(cam)) touchedCameras[cam] = cam.rect;
                if (cam.rect != target) cam.rect = target;
            }
        }

        private void RestoreCameras()
        {
            foreach (var kv in touchedCameras)
                if (kv.Key != null) kv.Key.rect = kv.Value;
            touchedCameras.Clear();
        }

        // --- Disegno ------------------------------------------------------------------

        private bool stylesReady;
        private GUIStyle orgStyle, roleStyle, levelLabelStyle, titleStyle, statusStyle, sectionStyle,
            rowLabelStyle, rowValueStyle, primaryButton, secondaryButton, footerStyle, pillStyle, logoStyle;
        private Texture2D bgTex, borderTex, panelTex, accentTex, accentHoverTex, pillTex, okTex, greyTex, blueTex;

        void OnGUI()
        {
            if (!Active) return;
            EnsureStyles();

            float w = SidebarWidth;
            float h = Screen.height;
            GUI.DrawTexture(new Rect(0f, 0f, w, h), bgTex);
            GUI.DrawTexture(new Rect(w - 2f, 0f, 2f, h), borderTex);

            float x = padding;
            float cw = w - 2f * padding;
            float y = padding;

            // Intestazione: logo + "UniBS / OPERATORE".
            const float logoSize = 64f;
            var logoRect = new Rect(x, y, logoSize, logoSize);
            GUI.DrawTexture(logoRect, panelTex);
            GUI.DrawTexture(new Rect(x, y, logoSize, 1f), borderTex);
            GUI.DrawTexture(new Rect(x, y + logoSize - 1f, logoSize, 1f), borderTex);
            GUI.DrawTexture(new Rect(x, y, 1f, logoSize), borderTex);
            GUI.DrawTexture(new Rect(x + logoSize - 1f, y, 1f, logoSize), borderTex);
            if (logo != null) GUI.DrawTexture(new Rect(x + 6f, y + 6f, logoSize - 12f, logoSize - 12f), logo, ScaleMode.ScaleToFit, true);
            else GUI.Label(logoRect, "UB", logoStyle);
            GUI.Label(new Rect(x + logoSize + 18f, y + 10f, cw - logoSize - 18f, 26f), orgName, orgStyle);
            GUI.Label(new Rect(x + logoSize + 18f, y + 38f, cw - logoSize - 18f, 18f), roleLabel, roleStyle);
            y += logoSize + 22f;
            GUI.DrawTexture(new Rect(0f, y, w, 1f), borderTex);
            y += 34f;

            // Livello e titolo.
            GUI.Label(new Rect(x, y, cw, 18f), $"LIVELLO {level.LevelNumber}", levelLabelStyle);
            y += 26f;
            var titleContent = new GUIContent(level.LevelTitle);
            float th = titleStyle.CalcHeight(titleContent, cw);
            GUI.Label(new Rect(x, y, cw, th), titleContent, titleStyle);
            y += th + 18f;

            // Riquadro stato con bordo sinistro blu e pallino colorato.
            var statusContent = new GUIContent(level.StatusLine);
            float sh = Mathf.Max(52f, statusStyle.CalcHeight(statusContent, cw - 44f) + 24f);
            var statusRect = new Rect(x, y, cw, sh);
            GUI.DrawTexture(statusRect, panelTex);
            GUI.DrawTexture(new Rect(x, y, 3f, sh), accentTex);
            var dotTex = level.IsRunning ? okTex : level.IsComplete ? blueTex : greyTex;
            GUI.DrawTexture(new Rect(x + 18f, y + sh * 0.5f - 5f, 10f, 10f), dotTex);
            GUI.Label(new Rect(x + 36f, y + 12f, cw - 44f, sh - 24f), statusContent, statusStyle);
            y += sh + 28f;

            // Hardware.
            GUI.Label(new Rect(x, y, cw, 18f), "HARDWARE", sectionStyle);
            y += 30f;
            bool middleware = MiddlewareConnected();
            HardwareRow(ref y, x, cw, "Middleware WEART", middleware ? "CONNESSO" : "NON CONNESSO", middleware ? HudTheme.Ok : HudTheme.Warn);
            HardwareRow(ref y, x, cw, "TouchDIVER Pro", middleware ? devicesText : "—", middleware ? devicesColor : HudTheme.Grey);
            HardwareRow(ref y, x, cw, "Calibrazione", middleware ? calibText : "—", middleware ? calibColor : HudTheme.Grey);
            string trk; Color trkColor;
            if (trackers == null) { trk = "ASSENTE"; trkColor = HudTheme.Grey; }
            else if (Time.unscaledTime - lastTrackerMoveTime < trackerActiveWindow) { trk = "ATTIVO"; trkColor = HudTheme.Ok; }
            else { trk = "NON USATO"; trkColor = HudTheme.Grey; }
            HardwareRow(ref y, x, cw, "Vive Tracker", trk, trkColor);

            // Blocco in basso: bottoni + footer (dal fondo verso l'alto).
            string footer = $"F1 diagnostica · F2 sottotitoli · F3 HUD\n{handSwitchKeyLabel} switch mano · M muta voce · R ripeti";
            var footerContent = new GUIContent(footer);
            float fh = footerStyle.CalcHeight(footerContent, cw);
            float by = h - padding - fh;
            GUI.Label(new Rect(x, by, cw, fh), footerContent, footerStyle);
            by -= 22f;

            if (flow != null && flow.CanGoNext)
            {
                by -= 56f;
                if (GUI.Button(new Rect(x, by, cw, 56f), $"{flow.NextButtonLabel}  ({flow.NextLevelKey})", primaryButton))
                    flow.GoToNextLevel();
                by -= 12f;
            }

            by -= 56f;
            GUI.enabled = level.IsRunning;
            if (GUI.Button(new Rect(x, by, cw, 56f), "Ripeti annuncio", secondaryButton))
                level.RepeatAnnouncement();
            GUI.enabled = true;
            by -= 12f;

            by -= 64f;
            string startLabel = level.IsRunning ? "Riavvia livello" : level.IsComplete ? "Nuovo partecipante" : "Avvia livello";
            if (GUI.Button(new Rect(x, by, cw, 64f), startLabel, primaryButton))
                level.StartLevel();

            DrawPill();
        }

        private void HardwareRow(ref float y, float x, float cw, string label, string value, Color color)
        {
            const float rh = 44f;
            GUI.Label(new Rect(x, y, cw * 0.55f, rh), label, rowLabelStyle);
            GUI.Label(new Rect(x, y, cw, rh), HudTheme.Rich(color, value), rowValueStyle);
            GUI.DrawTexture(new Rect(x, y + rh - 1f, cw, 1f), borderTex);
            y += rh;
        }

        // Pill in alto a destra: stato demo (cliccabile), id partecipante, timer del livello.
        private void DrawPill()
        {
            bool demoOn = HandDemoModeController.Exists && HandDemoModeController.DemoActive;
            string demoLabel = demo == null ? "MANI DEMO —" : demoOn ? "MANI DEMO ON" : "MANI DEMO OFF";
            string pid = SessionLogger.Instance != null ? SessionLogger.Instance.ParticipantId : "—";
            int secs = Mathf.FloorToInt(level.ElapsedSeconds);
            string timer = $"{secs / 60:00}:{secs % 60:00}";
            string dot = HudTheme.Rich(demoOn ? HudTheme.Ok : HudTheme.Grey, "●");
            var content = new GUIContent($"{dot}  {demoLabel}  ·  {pid}  ·  {timer}");

            var size = pillStyle.CalcSize(content);
            var rect = new Rect(Screen.width - size.x - 40f - 24f, 24f, size.x + 40f, size.y + 20f);
            GUI.DrawTexture(rect, pillTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), borderTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderTex);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 10f, size.x, size.y), content, pillStyle);

            // Click sulla pill = toggle demo (stessa cosa del vecchio pannello).
            if (demo != null && GUI.Button(rect, GUIContent.none, GUIStyle.none))
                demo.SetDemoActive(!demoOn);
        }

        private static bool MiddlewareConnected()
        {
            var c = WeArtController.Instance;
            return c != null && c.Client != null && c.Client.IsConnected;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;

            bgTex = HudTheme.Solid(HudTheme.Bg);
            borderTex = HudTheme.Solid(HudTheme.Border);
            panelTex = HudTheme.Solid(HudTheme.Panel);
            accentTex = HudTheme.Solid(HudTheme.Accent);
            accentHoverTex = HudTheme.Solid(HudTheme.AccentLight);
            pillTex = HudTheme.Solid(new Color(HudTheme.Bg.r, HudTheme.Bg.g, HudTheme.Bg.b, 0.92f));
            okTex = HudTheme.Solid(HudTheme.Ok);
            greyTex = HudTheme.Solid(HudTheme.Grey);
            blueTex = HudTheme.Solid(HudTheme.AccentLight);

            orgStyle = HudTheme.Label(HudTheme.Sans(22), 22, HudTheme.Text, FontStyle.Bold);
            roleStyle = HudTheme.Label(HudTheme.Mono(13), 13, HudTheme.AccentLight);
            logoStyle = HudTheme.Label(HudTheme.Serif(26), 26, HudTheme.Text, FontStyle.Normal, TextAnchor.MiddleCenter);
            levelLabelStyle = HudTheme.Label(HudTheme.Mono(13), 13, HudTheme.AccentLight);
            titleStyle = HudTheme.Label(HudTheme.Serif(38), 38, HudTheme.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
            statusStyle = HudTheme.Label(HudTheme.Sans(19), 19, HudTheme.Text, FontStyle.Normal, TextAnchor.MiddleLeft, true);
            sectionStyle = HudTheme.Label(HudTheme.Mono(13), 13, HudTheme.Muted);
            rowLabelStyle = HudTheme.Label(HudTheme.Sans(18), 18, HudTheme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            rowValueStyle = HudTheme.Label(HudTheme.Mono(15), 15, HudTheme.Text, FontStyle.Bold, TextAnchor.MiddleRight);
            footerStyle = HudTheme.Label(HudTheme.Mono(12), 12, HudTheme.Muted, FontStyle.Normal, TextAnchor.UpperLeft, true);
            pillStyle = HudTheme.Label(HudTheme.Mono(16), 16, HudTheme.Text, FontStyle.Bold, TextAnchor.MiddleLeft);

            primaryButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0),
                normal = { background = accentTex, textColor = HudTheme.Text },
                hover = { background = accentHoverTex, textColor = HudTheme.Text },
                active = { background = accentHoverTex, textColor = HudTheme.Text },
                focused = { background = accentTex, textColor = HudTheme.Text }
            };
            var sans = HudTheme.Sans(22);
            if (sans != null) primaryButton.font = sans;

            var outlineTex = HudTheme.Solid(HudTheme.Panel);
            secondaryButton = new GUIStyle(primaryButton)
            {
                fontSize = 19,
                fontStyle = FontStyle.Normal,
                normal = { background = outlineTex, textColor = HudTheme.Text },
                hover = { background = HudTheme.Solid(HudTheme.Border), textColor = HudTheme.Text },
                active = { background = HudTheme.Solid(HudTheme.Border), textColor = HudTheme.Text },
                focused = { background = outlineTex, textColor = HudTheme.Text }
            };
            var sans19 = HudTheme.Sans(19);
            if (sans19 != null) secondaryButton.font = sans19;

            stylesReady = true;
        }
    }
}
