using UnityEngine;

namespace HapticResearch.Labyrinth
{
    // Il labirinto DI QUESTA SCENA: tiene il layout, costruisce la MazeMap e fa da unico
    // punto di conversione fra spazio mondo e coordinate del labirinto.
    //
    // Il sistema di riferimento del labirinto e' il Transform DI QUESTO oggetto, non
    // quello del tavolo. E' importante: la geometria viene generata come figlia di questo
    // oggetto, quindi logica e cubi in scena si muovono insieme per costruzione. Se qui
    // si usasse il tavolo mentre i muri stanno sotto un figlio ruotato, la mappa direbbe
    // "sei nel corridoio" mentre il dito e' dentro un muro, e sarebbe un errore
    // invisibile finche' qualcuno non gioca il livello.
    //
    // L'oggetto va appeso al ROOT del tavolo (non a TableTop, che e' scalato) con la
    // rotazione `participantYaw` del layout: il labirinto e' scritto in coordinate del
    // partecipante (lui a -z che guarda verso +z, la sua destra a +x) e quella rotazione
    // le porta in quelle del tavolo. Ci pensa MazeGeometryBuilder.
    [DisallowMultipleComponent]
    public class MazeRuntime : MonoBehaviour
    {
        [Header("Definizione")]
        [SerializeField] private MazeLayoutAsset layout;

        [Header("Gizmo (solo editor)")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool drawWalls = true;
        [SerializeField] private bool drawPath = true;
        [SerializeField] private bool drawCells = true;

        private MazeMap map;

        public MazeLayoutAsset Layout => layout;

        public MazeMap Map
        {
            get
            {
                if (map == null && layout != null) map = layout.BuildMap();
                return map;
            }
        }

        public void Rebuild() => map = layout != null ? layout.BuildMap() : null;

        void Awake() => Rebuild();

        // --- Spazio ------------------------------------------------------------------

        // Solo posizione e rotazione: la scala va ignorata di proposito, cosi' una scala
        // messa per sbaglio sull'oggetto non deforma la mappa in silenzio.
        public Vector3 WorldToLocal(Vector3 world) =>
            Quaternion.Inverse(transform.rotation) * (world - transform.position);

        public Vector3 LocalToWorld(Vector3 local) =>
            transform.position + transform.rotation * local;

        public Quaternion LocalToWorldRotation => transform.rotation;

        // --- Interrogazione -------------------------------------------------------------

        // Dove si trova un punto in spazio mondo (tipicamente la punta dell'indice).
        public LocateResult Locate(Vector3 world, out Vector2Int cell)
        {
            cell = Vector2Int.zero;
            var m = Map;
            if (m == null) return LocateResult.OutsideBounds;
            return m.Locate(WorldToLocal(world), out cell);
        }

        public Vector3 CellCenterWorld(int col, int row)
        {
            var m = Map;
            return m == null ? Vector3.zero : LocalToWorld(m.CellCenter(col, row));
        }

        public Vector3 CellCenterWorld(Vector2Int cell) => CellCenterWorld(cell.x, cell.y);

        // --- Gizmo -------------------------------------------------------------------------

        void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            // In editor si ricostruisce sempre: il layout puo' essere appena cambiato.
            var m = layout != null ? layout.BuildMap() : null;
            if (m == null) return;

            var old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            if (drawWalls)
            {
                Gizmos.color = new Color(0.85f, 0.85f, 0.9f, 0.9f);
                foreach (var run in m.WallRuns()) Gizmos.DrawWireCube(run.LocalCenter, run.LocalSize);
            }

            if (drawCells)
            {
                for (int c = 0; c < m.Columns; c++)
                    for (int r = 0; r < m.Rows; r++)
                    {
                        var kind = m.KindAt(c, r);
                        if (kind == CellKind.Wall) continue;
                        Gizmos.color = ColorOf(kind);
                        var p = m.CellCenter(c, r);
                        Gizmos.DrawCube(new Vector3(p.x, p.y + 0.001f, p.z),
                                        new Vector3(m.CorridorWidth * 0.8f, 0.002f, m.CorridorWidth * 0.8f));
                    }
            }

            if (drawPath && m.Path.Count > 1)
            {
                Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 1f);
                float y = m.TableTopY + m.WallHeight + 0.005f;
                for (int k = 0; k + 1 < m.Path.Count; k++)
                {
                    var a = m.CellCenter(m.Path[k].x, m.Path[k].y);
                    var b = m.CellCenter(m.Path[k + 1].x, m.Path[k + 1].y);
                    Gizmos.DrawLine(new Vector3(a.x, y, a.z), new Vector3(b.x, y, b.z));
                }
            }

            Gizmos.matrix = old;
        }

        private static Color ColorOf(CellKind kind)
        {
            switch (kind)
            {
                case CellKind.Entrance: return new Color(0.3f, 0.85f, 0.45f, 0.8f);
                case CellKind.Exit: return new Color(0.3f, 0.65f, 0.95f, 0.8f);
                case CellKind.ChoiceTile: return new Color(0.95f, 0.75f, 0.3f, 0.8f);
                default: return new Color(0.6f, 0.6f, 0.65f, 0.5f);
            }
        }
    }
}
