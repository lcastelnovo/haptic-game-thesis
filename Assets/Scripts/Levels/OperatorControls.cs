using UnityEngine;

using HapticResearch.UI;
namespace HapticResearch.Levels
{
    // Pannello a schermo per l'OPERATORE VEDENTE: bottoni cliccabili col mouse per
    // avviare / ri-avviare il livello e ri-annunciare la forma corrente. Stile UniBS.
    //
    // È un controllo "da regia": compare sul monitor dell'operatore (non nel visore) ed è
    // separato dai bottoni in-world accessibili usati dal giocatore non vedente.
    public class OperatorControls : MonoBehaviour
    {
        [Tooltip("Se null, cerca il LevelController della scena all'avvio.")]
        [SerializeField] private LevelController manager;

        [Header("Aspetto pannello")]
        [SerializeField] private bool showPanel = true;

        [Tooltip("Ingrandimento del pannello (utile su schermi ad alta risoluzione).")]
        [SerializeField, Range(1f, 4f)] private float scale = 1.5f;

        [Header("Animazione")]
        [Tooltip("Velocità dello slide verso sinistra all'avvio del livello.")]
        [SerializeField] private float slideSpeed = 7f;
        [Tooltip("Dimensione stimata del pannello (non scalata), per centrarlo prima dell'avvio.")]
        [SerializeField] private Vector2 panelSizeEstimate = new Vector2(260f, 210f);

        private Vector2 panelPos = new Vector2(12f, 12f);

        // Stile UniBS (approssimazione palette unibs.it).
        private bool stylesReady;
        private Texture2D panelTex, titleTex, btnTex, btnHoverTex;
        private GUIStyle panelStyle, titleStyle, labelStyle, buttonStyle;

        void Awake()
        {
            if (manager == null) manager = LevelController.Find();
            if (manager == null)
                Debug.LogWarning("[OperatorControls] Nessun LevelController in scena: aggiungilo e (opzionale) assegnalo nel campo Manager.");
        }

        void Start()
        {
            panelPos = CenterPos(); // parte centrato
        }

        void Update()
        {
            // Centrato finché il livello non è avviato; all'avvio slida in alto a sinistra.
            bool running = manager != null && manager.IsRunning;
            Vector2 target = running ? new Vector2(12f, 12f) : CenterPos();
            panelPos = Vector2.Lerp(panelPos, target, 1f - Mathf.Exp(-slideSpeed * Time.deltaTime));
        }

        private Vector2 CenterPos()
        {
            Vector2 sz = panelSizeEstimate * scale;
            return new Vector2((Screen.width - sz.x) * 0.5f, (Screen.height - sz.y) * 0.5f);
        }

        void OnGUI()
        {
            // Pannello storico: con l'HUD operatore (sidebar) attivo non serve piu'.
            if (!showPanel || OperatorHud.Active) return;
            EnsureStyles();

            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(panelPos.x, panelPos.y, 0f), Quaternion.identity, Vector3.one * scale);

            GUILayout.BeginVertical(panelStyle, GUILayout.Width(260f));
            GUILayout.Label($"  UNIBS · Operatore - Livello {(manager != null ? manager.LevelNumber : 1)}", titleStyle);

            if (manager == null)
            {
                GUILayout.Label("Manca il LevelController\nin scena: aggiungilo.", labelStyle);
            }
            else
            {
                GUILayout.Label(StatusLine(), labelStyle);
                GUILayout.Space(6f);

                string startLabel = manager.IsRunning ? "Riavvia livello"
                                  : manager.IsComplete ? "Nuovo partecipante"
                                  : "Avvia livello";
                if (GUILayout.Button(startLabel, buttonStyle, GUILayout.Height(40f)))
                    manager.StartLevel();

                GUI.enabled = manager.IsRunning;
                if (GUILayout.Button("Ripeti annuncio", buttonStyle, GUILayout.Height(40f)))
                    manager.RepeatAnnouncement();
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
            GUI.matrix = prevMatrix;
        }

        private string StatusLine() => "Stato: " + manager.StatusLine;

        private void EnsureStyles()
        {
            if (stylesReady) return;

            panelTex = Solid(new Color32(2, 40, 78, 250));     // blu UniBS
            titleTex = Solid(new Color32(0, 150, 214, 255));   // azzurro accento
            btnTex = Solid(new Color32(0, 92, 155, 255));      // bottone blu
            btnHoverTex = Solid(new Color32(0, 120, 190, 255));

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = panelTex },
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 10)
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white, background = titleTex },
                fontStyle = FontStyle.Bold,
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 0, 8)
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                fontSize = 15,
                wordWrap = true,
                padding = new RectOffset(12, 10, 2, 2)
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { textColor = Color.white, background = btnTex },
                hover = { textColor = Color.white, background = btnHoverTex },
                active = { textColor = Color.white, background = btnHoverTex },
                focused = { textColor = Color.white, background = btnTex },
                fontStyle = FontStyle.Bold,
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(10, 10, 4, 4),
                border = new RectOffset(0, 0, 0, 0)
            };
            stylesReady = true;
        }

        private Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
