using UnityEngine;

namespace HapticResearch.Levels
{
    // Marcatore applicato a ogni forma: ne ricorda l'id logico e gestisce il feedback visivo
    // (blu = da indovinare, verde = indovinata). Colori regolabili nell'Inspector.
    public class RecognizableShape : MonoBehaviour
    {
        [SerializeField] private string shapeId;

        [Header("Feedback visivo")]
        [SerializeField]
        [Tooltip("Colore di base della forma (da indovinare). Blu UniBS.")]
        private Color defaultColor = new Color(0.13f, 0.31f, 0.62f);

        [SerializeField]
        [Tooltip("Colore quando la forma è stata indovinata.")]
        private Color solvedColor = new Color(0.20f, 0.80f, 0.25f);

        private bool solved;

        public string ShapeId => shapeId;
        public bool Solved => solved;

        public void SetShapeId(string id) => shapeId = id;

        // Verde: la forma è stata indovinata.
        public void MarkSolved()
        {
            solved = true;
            ApplyColor(solvedColor);
        }

        // Blu di base: chiamato al (ri)avvio del livello.
        public void ResetVisual()
        {
            solved = false;
            ApplyColor(defaultColor);
        }

        // Applica il colore a tutte le mesh della forma, usando ISTANZE di materiale
        // (renderer.material) così non si colora anche il materiale condiviso con le altre forme.
        private void ApplyColor(Color c)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                SetColor(r.material, c);
        }

        // Imposta il colore principale sia in URP (_BaseColor) sia in built-in (_Color).
        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }
}
