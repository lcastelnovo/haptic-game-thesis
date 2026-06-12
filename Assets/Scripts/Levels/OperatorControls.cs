using UnityEngine;

namespace HapticResearch.Levels
{
    // Pannello a schermo per l'OPERATORE VEDENTE: bottoni cliccabili col mouse per
    // avviare / ri-avviare il livello e ri-annunciare la forma corrente.
    //
    // È un controllo "da regia": compare sul monitor dell'operatore (non nel visore) ed è
    // separato dai bottoni in-world accessibili usati dal giocatore non vedente.
    // Per usarlo: aggiungi questo componente a un GameObject qualsiasi (anche quello del
    // manager). Trova il ShapeRecognitionManager da solo. In Play compare in alto a sinistra.
    public class OperatorControls : MonoBehaviour
    {
        [Tooltip("Se null, cerca un ShapeRecognitionManager in scena all'avvio.")]
        [SerializeField] private ShapeRecognitionManager manager;

        [Header("Aspetto pannello")]
        [SerializeField] private bool showPanel = true;

        [Tooltip("Ingrandimento del pannello (utile su schermi ad alta risoluzione).")]
        [SerializeField, Range(1f, 4f)] private float scale = 1.5f;

        void Awake()
        {
            if (manager == null) manager = FindFirstObjectByType<ShapeRecognitionManager>();
            if (manager == null)
                Debug.LogWarning("[OperatorControls] Nessun ShapeRecognitionManager in scena: aggiungilo e (opzionale) assegnalo nel campo Manager.");
        }

        void OnGUI()
        {
            if (!showPanel) return;

            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(12f, 12f, 0f), Quaternion.identity, Vector3.one * scale);

            const int fs = 18;
            var box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 12, 12) };
            var title = new GUIStyle(GUI.skin.label) { fontSize = fs + 2, fontStyle = FontStyle.Bold };
            var label = new GUIStyle(GUI.skin.label) { fontSize = fs };
            var button = new GUIStyle(GUI.skin.button) { fontSize = fs };

            GUILayout.BeginVertical(box, GUILayout.Width(260f));
            GUILayout.Label("Operatore - Level 1", title);

            if (manager == null)
            {
                GUILayout.Label("Manca ShapeRecognitionManager\nin scena: aggiungilo.", label);
            }
            else
            {
                GUILayout.Label(StatusLine(), label);
                GUILayout.Space(6f);

                string startLabel = manager.IsRunning ? "Riavvia livello"
                                  : manager.IsComplete ? "Nuovo partecipante"
                                  : "Avvia livello";
                if (GUILayout.Button(startLabel, button, GUILayout.Height(42f)))
                    manager.StartLevel();

                GUI.enabled = manager.IsRunning;
                if (GUILayout.Button("Ripeti annuncio", button, GUILayout.Height(42f)))
                    manager.RepeatAnnouncement();
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
            GUI.matrix = prevMatrix;
        }

        private string StatusLine()
        {
            if (manager.IsComplete) return "Stato: completato";
            if (manager.IsRunning) return $"Round {manager.CurrentRound}/{manager.TotalRounds} - trova: {manager.CurrentTargetId}";
            return "Stato: in attesa di avvio";
        }
    }
}
