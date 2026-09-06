using UnityEngine;

namespace HapticResearch.Levels
{
    // Tappa del labirinto (Level 2): un punto sul piano del tavolo con un raggio. Il
    // LabyrinthManager le percorre in ordine (ingresso -> checkpoint... -> uscita) e
    // considera raggiunta una tappa quando la punta dell'indice entra nel cerchio (solo
    // XZ: l'altezza della mano non conta, il gioco vive sul piano del tavolo).
    //
    // Si sposta dall'Inspector (o dalla Scene view: gizmo colorato) per adattare il
    // percorso ai muri: la logica non dipende dalla geometria.
    public class MazeZone : MonoBehaviour
    {
        public enum Kind { Entrance, Checkpoint, Exit }

        [SerializeField] private Kind kind = Kind.Checkpoint;

        [Tooltip("Nome breve per log e HUD (es. ingresso, meta, uscita).")]
        [SerializeField] private string label = "tappa";

        [Tooltip("Raggio (m) entro cui la punta del dito 'raggiunge' la tappa.")]
        [SerializeField] private float radius = 0.06f;

        public Kind ZoneKind => kind;
        public string Label => string.IsNullOrEmpty(label) ? name : label;
        public float Radius => radius;

        // Distanza sul piano XZ dalla posizione data.
        public float DistanceXZ(Vector3 worldPos)
        {
            var p = transform.position;
            float dx = worldPos.x - p.x, dz = worldPos.z - p.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public bool Contains(Vector3 worldPos) => DistanceXZ(worldPos) <= radius;

        public Color GizmoColor =>
            kind == Kind.Entrance ? new Color(0.3f, 0.85f, 0.45f) :
            kind == Kind.Exit ? new Color(0.3f, 0.65f, 0.95f) :
            new Color(0.95f, 0.75f, 0.3f);

        void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            var p = transform.position;
            const int segments = 32;
            Vector3 prev = p + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var next = p + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
            Gizmos.DrawLine(p - Vector3.right * 0.01f, p + Vector3.right * 0.01f);
            Gizmos.DrawLine(p - Vector3.forward * 0.01f, p + Vector3.forward * 0.01f);
        }
    }
}
