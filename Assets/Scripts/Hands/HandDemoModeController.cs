using UnityEngine;
using WeArt.Components;
using HapticResearch.Haptics;
using HapticResearch.Levels;

using HapticResearch.UI;
namespace HapticResearch.Hands
{
    /// <summary>
    /// Modalità "mani demo": permette di testare il gioco SENZA guanti WEART / Vive Tracker,
    /// muovendo le mani simulate con il mouse. È uno strumento di sviluppo/demo: non fa parte
    /// dell'esperienza per il partecipante e non tocca il percorso hardware reale.
    ///
    /// Quando la demo è attiva:
    ///  - la mano ATTIVA segue il cursore sul piano del tavolo (movimento assoluto);
    ///  - tenendo premuto il CLICK SINISTRO le dita si chiudono (grasp) e si riaprono al rilascio;
    ///  - Space cambia mano, Q/E alza/abbassa, frecce/Z/X ruotano.
    ///
    /// Usa come sorgente del raycast la CAMERA che sta davvero renderizzando (quella con depth più
    /// alta), così la mano finisce sotto il cursore anche se in scena ci sono più camere.
    ///
    /// Il gancio in HandInputManager è guardato da <see cref="Exists"/>: nelle scene senza questo
    /// componente il comportamento storico resta identico.
    /// </summary>
    public class HandDemoModeController : MonoBehaviour
    {
        public enum Mode
        {
            Auto,      // ON se nessun device WEART è connesso/calibrato, OFF se i guanti ci sono
            SempreOn,  // sempre attiva all'avvio
            SempreOff  // mai attiva all'avvio (si accende dal toggle)
        }

        [Header("Comportamento")]
        [SerializeField]
        [Tooltip("Auto: demo ON senza guanti, OFF con guanti. Oppure forza Sempre On/Off.")]
        private Mode mode = Mode.Auto;

        [SerializeField]
        [Tooltip("Velocità con cui le dita si chiudono/aprono tenendo premuto il click sinistro.")]
        private float graspSpeed = 6f;

        [SerializeField]
        [Tooltip("Tasto per afferrare/rilasciare in modo inequivocabile la forma vicina alla mano.")]
        private KeyCode grabKey = KeyCode.G;

        [SerializeField]
        [Tooltip("Raggio (m) entro cui il tasto grab cerca una forma attorno alla mano.")]
        private float keyboardGrabRadius = 0.45f;

        [SerializeField]
        [Tooltip("Distanza (m) entro cui il palmo deve toccare la forma per poterla afferrare.")]
        private float contactRadius = 0.08f;

        [Header("Riferimenti (auto-collegati a runtime se lasciati vuoti)")]
        [SerializeField] private HandInputManager inputManager;

        [Header("UI")]
        [SerializeField] private bool showToggle = true;

        // Stato globale letto da HandInputManager. Statico per non richiedere reference incrociate.
        public static bool Exists { get; private set; }
        public static bool DemoActive { get; private set; }
        // Le mani si muovono solo se: demo attiva E livello avviato (finché "in attesa di avvio" sono bloccate).
        public static bool MovementAllowed { get; private set; }

        // True dopo che l'utente ha usato il toggle: da lì la modalità Auto smette di sovrascrivere.
        private bool userOverrode = false;
        private bool wasActive = false;

        // Controller di chiusura dita delle due mani (per il grasp col click sinistro).
        private HandCloseController leftClose;
        private HandCloseController rightClose;

        // Controller di grab delle due mani (per prendere il grabPoint / punto mano).
        private HandGrabController leftGrab;
        private HandGrabController rightGrab;

        // Forma attualmente "afferrata" in demo da ciascuna mano (iniettata in WeArtGraspBridge).
        private GameObject leftGraspedShape;
        private GameObject rightGraspedShape;

        // Livello della scena: le mani sono usabili solo mentre il livello è avviato.
        private LevelController levelManager;

        // Istanza che "possiede" gli static: al cambio scena il controller vecchio viene
        // disabilitato DOPO l'OnEnable di quello nuovo e non deve azzerare il suo stato.
        private static HandDemoModeController owner;

        private void OnEnable()
        {
            owner = this;
            Exists = true;
            DemoActive = EvaluateAuto();
        }

        private void OnDisable()
        {
            if (owner != this) return;
            owner = null;
            Exists = false;
            DemoActive = false;
        }

        private void Awake()
        {
            AutoWire();
        }

        private void Start()
        {
            // Stato iniziale: con guanti/tracker (demo OFF) il rig mouse è spento; in demo è acceso.
            ApplyDemoState();
        }

        private void AutoWire()
        {
            if (inputManager == null)
                inputManager = FindFirstObjectByType<HandInputManager>();
            if (levelManager == null)
                levelManager = LevelController.Find();

            if (inputManager != null)
            {
                if (inputManager.leftHand != null)
                {
                    leftClose = inputManager.leftHand.GetComponentInChildren<HandCloseController>();
                    leftGrab = inputManager.leftHand.GetComponentInChildren<HandGrabController>();
                }
                if (inputManager.rightHand != null)
                {
                    rightClose = inputManager.rightHand.GetComponentInChildren<HandCloseController>();
                    rightGrab = inputManager.rightHand.GetComponentInChildren<HandGrabController>();
                }
            }
        }

        private void Update()
        {
            // Finché l'utente non tocca il toggle, la modalità decide lo stato.
            if (!userOverrode)
                DemoActive = EvaluateAuto();

            // Ad ogni cambio di stato accendo/spengo il rig mouse (e allineo la camera in demo).
            if (DemoActive != wasActive)
                ApplyDemoState();
            wasActive = DemoActive;

            // Mani utilizzabili solo se demo attiva E livello avviato (IsRunning). Finché è "in
            // attesa di avvio", le mani restano ferme: prima premi "Avvia livello".
            MovementAllowed = DemoActive && (levelManager == null || levelManager.IsRunning);

            if (!MovementAllowed)
            {
                ClearAllDemoGrasps(); // livello non in corso: rilascia eventuali prese demo
                return;
            }
            if (inputManager == null)
                return;

            // Afferra / rilascia la forma con il tasto dedicato: inietta l'oggetto in WeArtGraspBridge,
            // così ShapeRecognitionManager reagisce ESATTAMENTE come col guanto reale.
            if (Input.GetKeyDown(grabKey))
                ToggleDemoGrasp();

            // Le dita si chiudono se la mano tiene una forma (o, come feedback, tenendo il click SX).
            DriveFingers(leftClose, leftGraspedShape != null || (inputManager.IsLeftActive && Input.GetMouseButton(0)));
            DriveFingers(rightClose, rightGraspedShape != null || (!inputManager.IsLeftActive && Input.GetMouseButton(0)));
        }

        // Accende/spegne la demo da fuori (pill dell'HUD operatore): da qui in poi la modalità
        // Auto non sovrascrive più la scelta, come col toggle del pannello.
        public void SetDemoActive(bool on)
        {
            userOverrode = true;
            DemoActive = on;
        }

        // Interpola la chiusura delle dita di una mano verso aperta/chiusa (pilota closeAmounts
        // di HandCloseController senza toccarne il ramo WEART).
        private void DriveFingers(HandCloseController hc, bool closed)
        {
            if (hc == null || hc.closeAmounts == null)
                return;
            float target = closed ? 1f : 0f;
            for (int i = 0; i < hc.closeAmounts.Length; i++)
                hc.closeAmounts[i] = Mathf.MoveTowards(hc.closeAmounts[i], target, graspSpeed * Time.deltaTime);
        }

        // Applica lo stato demo. FONDAMENTALE: con guanti/tracker reali (demo OFF) il rig mouse è
        // DISATTIVATO, così non interferisce col gioco reale (niente collider/mani/grab di troppo).
        // In demo ON il rig è acceso e la camera allineata.
        private void ApplyDemoState()
        {
            SetMouseRigActive(DemoActive);
            if (DemoActive)
                ApplyDisplayCameraToHands();
            else
                ClearAllDemoGrasps(); // uscendo dalla demo non lasciare grasp appesi nel bridge
        }

        // Rilascia eventuali forme afferrate in demo (per non lasciare stato sporco al gioco reale).
        private void ClearAllDemoGrasps()
        {
            WeArtGraspBridge bridge = WeArtGraspBridge.Instance;
            if (bridge != null)
            {
                if (leftGraspedShape != null) bridge.ClearDemoGrasp(true);
                if (rightGraspedShape != null) bridge.ClearDemoGrasp(false);
            }
            leftGraspedShape = null;
            rightGraspedShape = null;
        }

        // Attiva/disattiva il rig mano-mouse (le due mani + il loro HandManager).
        private void SetMouseRigActive(bool on)
        {
            if (inputManager == null)
                return;
            if (inputManager.leftHand != null)
                inputManager.leftHand.gameObject.SetActive(on);
            if (inputManager.rightHand != null)
                inputManager.rightHand.gameObject.SetActive(on);
            inputManager.gameObject.SetActive(on);
        }

        // Assegna alle mani la camera che sta davvero renderizzando, così la mano segue il cursore
        // nella vista che l'utente vede (in scena possono esserci più camere).
        private void ApplyDisplayCameraToHands()
        {
            Camera cam = ResolveDisplayCamera();
            if (cam == null || inputManager == null)
                return;

            if (inputManager.leftHand != null)
                inputManager.leftHand.topCamera = cam;
            if (inputManager.rightHand != null)
                inputManager.rightHand.topCamera = cam;
        }

        // La camera "in primo piano" su Display 1: quella abilitata con depth più alta.
        private Camera ResolveDisplayCamera()
        {
            Camera best = null;
            float bestDepth = float.NegativeInfinity;
            foreach (Camera c in Camera.allCameras) // allCameras = solo quelle abilitate
            {
                if (c == null || c.targetTexture != null || c.targetDisplay != 0)
                    continue;
                if (c.depth >= bestDepth)
                {
                    bestDepth = c.depth;
                    best = c;
                }
            }
            return best;
        }

        // Afferra/rilascia in modo inequivocabile: al primo tocco del tasto afferra la forma PUNTATA
        // dal cursore (o la più vicina alla mano) iniettandola in WeArtGraspBridge come il guanto;
        // ripremendo il tasto la rilascia.
        private void ToggleDemoGrasp()
        {
            bool isLeft = inputManager.IsLeftActive;
            GameObject current = isLeft ? leftGraspedShape : rightGraspedShape;
            WeArtGraspBridge bridge = WeArtGraspBridge.Instance;

            // Se già afferrata -> rilascia.
            if (current != null)
            {
                if (bridge != null) bridge.ClearDemoGrasp(isLeft);
                if (isLeft) leftGraspedShape = null; else rightGraspedShape = null;
                Debug.Log("[Demo] Forma rilasciata.");
                return;
            }

            if (bridge == null)
            {
                Debug.LogWarning("[Demo] WeArtGraspBridge non presente: avvia il livello prima di afferrare.");
                return;
            }

            GameObject shape = FindGraspTargetShape();
            if (shape == null)
            {
                int n = FindObjectsByType<RecognizableShape>(FindObjectsSortMode.None).Length;
                Debug.Log(n == 0
                    ? "[Demo] Nessuna forma riconoscibile in scena (livello non avviato o setup rotto)."
                    : "[Demo] La mano non tocca nessuna forma: avvicina il palmo (abbassa con E) e riprova.");
                return;
            }

            bridge.SetDemoGrasp(shape, isLeft);
            if (isLeft) leftGraspedShape = shape; else rightGraspedShape = shape;
            Debug.Log($"[Demo] Afferrata '{shape.name}'. Tienila per il riconoscimento ({grabKey} per mollare).");
        }

        // Trova la forma da afferrare: prima quella PUNTATA dal cursore (raycast), poi la più vicina
        // alla mano attiva (overlap). Ritorna il GameObject che porta la RecognizableShape.
        private GameObject FindGraspTargetShape()
        {
            // Serve il CONTATTO: cerco un collider di forma entro contactRadius dal palmo (grabPoint).
            // Niente più "punta e prende da lontano": la mano deve essere a contatto con la forma.
            Vector3 center = ActiveHandCenter();
            Collider[] cols = Physics.OverlapSphere(center, contactRadius, ~0, QueryTriggerInteraction.Collide);
            RecognizableShape best = null;
            float bestDist = float.MaxValue;
            foreach (Collider c in cols)
            {
                RecognizableShape rec = c.GetComponentInParent<RecognizableShape>();
                if (rec == null)
                    continue;
                float d = Vector3.Distance(center, c.bounds.center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = rec;
                }
            }
            return best != null ? best.gameObject : null;
        }

        private Vector3 ActiveHandCenter()
        {
            HandGrabController grab = inputManager.IsLeftActive ? leftGrab : rightGrab;
            if (grab != null && grab.grabPoint != null)
                return grab.grabPoint.position;
            HandPhysicsController hand = inputManager.IsLeftActive ? inputManager.leftHand : inputManager.rightHand;
            return hand != null ? hand.transform.position : Vector3.zero;
        }


        // Ritorna lo stato demo secondo la modalità corrente.
        private bool EvaluateAuto()
        {
            switch (mode)
            {
                case Mode.SempreOn: return true;
                case Mode.SempreOff: return false;
                default: return !IsWeArtDevicePresent();
            }
        }

        // Sola lettura sullo stato del middleware WEART: NON modifica nulla del percorso guanti.
        // "Device presente" = middleware davvero CONNESSO (socket aperto). NB: il prefab WeArtController
        // è sempre in scena e il getter .Client CREA il client, quindi "Client != null" è sempre vero:
        // il segnale affidabile è Client.IsConnected. (IsCalibrated era troppo stringente: se la
        // calibrazione non andava a buon fine la demo non si spegneva mai.) Stesso segnale di
        // GloveGraspDetector, così demo e grab guanti concordano sempre.
        private bool IsWeArtDevicePresent()
        {
            var c = WeArtController.Instance;
            return c != null && c.Client != null && c.Client.IsConnected;
        }

        // ---------------------------------------------------------------------------------------
        // UI (stile UniBS: pannello blu, barra titolo azzurra, testo bianco). Approssimazione della
        // palette di unibs.it - i colori si possono ritoccare qui sotto.
        // ---------------------------------------------------------------------------------------
        private bool stylesReady;
        private Texture2D panelTex;
        private Texture2D titleTex;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle toggleStyle;

        private void OnGUI()
        {
            // Con l'HUD operatore lo stato demo sta nella pill in alto a destra.
            if (!showToggle || OperatorHud.Active)
                return;

            EnsureStyles();

            const float w = 340f;
            float h = DemoActive ? 188f : 104f;
            Rect rect = new Rect(Screen.width - w - 12f, 12f, w, h);

            GUI.Box(rect, GUIContent.none, panelStyle);
            GUILayout.BeginArea(new Rect(rect.x, rect.y, rect.width, rect.height));

            GUILayout.Label("  UNIBS · Modalità Demo", titleStyle);

            bool newVal = GUILayout.Toggle(DemoActive,
                DemoActive ? "  Mani Demo: ON" : "  Mani Demo: OFF", toggleStyle);
            if (newVal != DemoActive)
            {
                DemoActive = newVal;
                userOverrode = true;
            }

            if (DemoActive && !MovementAllowed)
            {
                GUILayout.Label("Premi \"Avvia livello\" per muovere le mani.", bodyStyle);
            }
            else if (DemoActive)
            {
                GUILayout.Label(
                    "Mouse  -  muovi la mano\n" +
                    "G  -  afferra / rilascia la forma che TOCCHI\n" +
                    "Q / E  -  alza / abbassa (per toccare la forma)\n" +
                    "Tab  -  cambia mano (SX / DX)",
                    bodyStyle);
            }
            else
            {
                GUILayout.Label("Guanti / Tracker attivi - mouse disattivato.", bodyStyle);
            }

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (stylesReady)
                return;

            panelTex = SolidTexture(new Color32(2, 40, 78, 248));    // blu UniBS
            titleTex = SolidTexture(new Color32(0, 150, 214, 255));  // azzurro accento

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = panelTex },
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 8)
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white, background = titleTex },
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 0, 8)
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                fontSize = 12,
                richText = true,
                wordWrap = true,
                padding = new RectOffset(12, 10, 2, 2)
            };

            toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                normal = { textColor = Color.white },
                onNormal = { textColor = Color.white },
                hover = { textColor = Color.white },
                onHover = { textColor = Color.white },
                focused = { textColor = Color.white },
                onFocused = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                padding = new RectOffset(24, 8, 4, 4),
                margin = new RectOffset(10, 10, 2, 8)
            };

            stylesReady = true;
        }

        private Texture2D SolidTexture(Color color)
        {
            Texture2D t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }
    }
}
