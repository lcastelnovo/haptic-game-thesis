using System.Text;
using UnityEngine;
using WeArt.Components;
using HapticResearch.Haptics;
using HapticResearch.Levels;
using HapticResearch.Hands;

namespace HapticResearch.Debugging
{
    // Pannello diagnostico a schermo per il percorso di presa coi guanti (toggle F1).
    //
    // Mostra in tempo reale, per ciascuna mano: chiusure dei thimble, stato del
    // GloveGraspDetector (armata/che tiene), slot del WeArtGraspBridge, stato del
    // ShapeRecognitionManager (bersaglio, forma in conteggio). Serve all'operatore per
    // capire AL VOLO perche' una presa non parte o risulta sbagliata, senza scavare
    // nella Console: basta una foto dello schermo.
    public class GraspDebugPanel : MonoBehaviour
    {
        [Tooltip("Tasto per mostrare/nascondere il pannello.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Pannello visibile all'avvio.")]
        [SerializeField] private bool startVisible = false;

        private bool visible;
        private GloveGraspDetector[] detectors;
        private WeArtThimbleTrackingObject[] thimbles;
        private ShapeRecognitionManager manager;

        private bool styleReady;
        private GUIStyle style;
        private Texture2D bg;
        private readonly StringBuilder sb = new StringBuilder(1024);

        void Awake()
        {
            visible = startVisible;
            detectors = FindObjectsByType<GloveGraspDetector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            thimbles = FindObjectsByType<WeArtThimbleTrackingObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var managers = FindObjectsByType<ShapeRecognitionManager>(FindObjectsSortMode.None);
            if (managers.Length > 0) manager = managers[0];
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            EnsureStyle();

            sb.Clear();
            sb.AppendLine($"=== DIAGNOSTICA PRESA ({toggleKey} per nascondere) ===");

            var controller = WeArtController.Instance;
            bool connected = controller != null && controller.Client != null && controller.Client.IsConnected;
            sb.AppendLine($"Middleware WEART: {(connected ? "CONNESSO" : "NON connesso")}");

            // Chiusure dei thimble, raggruppate per lato.
            sb.Append("Chiusure SX: ").AppendLine(ClosuresFor(true));
            sb.Append("Chiusure DX: ").AppendLine(ClosuresFor(false));

            // Bridge: cosa risulta afferrato da ciascuna mano.
            var bridge = WeArtGraspBridge.Instance;
            sb.AppendLine(bridge == null
                ? "Bridge: ASSENTE"
                : $"Bridge SX: {NameOf(bridge.LeftGrasped)}   DX: {NameOf(bridge.RightGrasped)}");

            // Stato del livello.
            if (manager != null)
                sb.AppendLine($"Livello: {(manager.IsRunning ? $"round {manager.CurrentRound}/{manager.TotalRounds}, trova '{manager.CurrentTargetId}'" : manager.IsComplete ? "COMPLETATO" : "fermo")}");

            // Posizione del punto di presa di ogni mano: se la "destra" risulta ferma
            // vicino a una forma mentre muovi il braccio destro, i tracker sono invertiti.
            foreach (var d in detectors)
            {
                if (d == null) continue;
                var gp = d.GrabPointPosition;
                sb.AppendLine($"GrabPoint {(d.IsLeftHand ? "SX" : "DX")}: {(gp.HasValue ? gp.Value.ToString("0.00") : "n/d")}");
            }

            sb.AppendLine($"GloveGraspDetector in scena: {detectors.Length} (log dettagliati in Console)");

            var content = new GUIContent(sb.ToString());
            var size = style.CalcSize(content);
            GUI.Label(new Rect(10f, 10f, size.x + 20f, size.y + 12f), content, style);
        }

        // Chiusure formattate dei thimble di un lato (pollice..mignolo, 0-1).
        private string ClosuresFor(bool left)
        {
            sbSide.Clear();
            foreach (var t in thimbles)
            {
                if (t == null) continue;
                if ((t.HandSide == WeArt.Core.HandSide.Left) != left) continue;
                sbSide.Append($"{t.ActuationPoint}={t.Closure.Value:0.00}  ");
            }
            return sbSide.Length > 0 ? sbSide.ToString() : "(nessun thimble)";
        }

        private readonly StringBuilder sbSide = new StringBuilder(128);

        private static string NameOf(GameObject go) => go != null ? go.name : "-";

        private void EnsureStyle()
        {
            if (styleReady) return;
            bg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.85f));
            bg.Apply();
            style = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white, background = bg },
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 6, 6)
            };
            styleReady = true;
        }
    }
}
