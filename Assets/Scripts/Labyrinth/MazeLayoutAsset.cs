using System.Collections.Generic;
using UnityEngine;

namespace HapticResearch.Labyrinth
{
    // Definizione dichiarativa del labirinto: e' il DATO dell'esperimento, non un
    // dettaglio di implementazione. Sta in un asset (e non in una generazione
    // procedurale con seed) perche' il layout va letto, discusso con i relatori,
    // riprodotto identico fra i partecipanti e citato nella tesi. Per fare varianti
    // si duplica l'asset.
    //
    // Da qui nascono sia la geometria in scena (MazeGeometryBuilder, in editor) sia la
    // mappa interrogata a runtime (MazeMap): una sola fonte, nessun rischio che la
    // logica e i cubi in scena raccontino due labirinti diversi.
    [CreateAssetMenu(menuName = "HapticResearch/Maze Layout", fileName = "MazeLayout")]
    public class MazeLayoutAsset : ScriptableObject
    {
        // Un bivio: la cella in cui il partecipante si ferma, la direzione giusta e
        // quella del vicolo cieco. Le due celle all'imbocco dei rami diventano
        // piastrelle di scelta.
        [System.Serializable]
        public class JunctionDef
        {
            [Tooltip("Cella in cui il corridoio si divide.")]
            public Vector2Int cell;

            [Tooltip("Direzione del ramo corretto: deve portare alla cella successiva del percorso.")]
            public MazeDir correctDir = MazeDir.West;

            [Tooltip("Direzione del vicolo cieco: una tasca di una cella, senza altre uscite.")]
            public MazeDir deadEndDir = MazeDir.North;

            [Tooltip("Nome breve per log e voce.")]
            public string label = "bivio";
        }

        [Header("Griglia")]
        [SerializeField] private int columns = 6;
        [SerializeField] private int rows = 3;

        [Header("Misure (m)")]
        [Tooltip("Larghezza del passaggio libero. 0.07 lascia scorrere il dito toccando entrambi i muri con un piccolo movimento laterale.")]
        [SerializeField] private float corridorWidth = 0.07f;

        [Tooltip("Spessore dei muri. 3 cm e' ampiamente percepibile e non spreca area utile.")]
        [SerializeField] private float wallThickness = 0.03f;

        [Tooltip("Altezza dei muri sopra il piano del tavolo.")]
        [SerializeField] private float wallHeight = 0.10f;

        [Tooltip("Quota del piano del tavolo.")]
        [SerializeField] private float tableTopY = 0.85f;

        [Tooltip("Centro del labirinto in coordinate LOCALI al tavolo (x, z). Va tenuto verso il bordo vicino al partecipante: seduto con busto fermo arriva a ~35-40 cm.")]
        [SerializeField] private Vector2 center = new Vector2(0f, -0.195f);

        [Header("Percorso corretto (in ordine, dall'ingresso all'uscita)")]
        [SerializeField]
        private List<Vector2Int> pathCells = new List<Vector2Int>
        {
            new Vector2Int(5, 0), new Vector2Int(4, 0), new Vector2Int(3, 0),
            new Vector2Int(2, 0), new Vector2Int(1, 0),
            new Vector2Int(1, 1), new Vector2Int(1, 2),
            new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(4, 2),
            new Vector2Int(5, 2),
        };

        [Header("Bivi (in ordine di percorso)")]
        [SerializeField]
        private List<JunctionDef> junctions = new List<JunctionDef>
        {
            new JunctionDef { cell = new Vector2Int(3, 0), correctDir = MazeDir.West,  deadEndDir = MazeDir.North, label = "primo bivio" },
            new JunctionDef { cell = new Vector2Int(1, 0), correctDir = MazeDir.North, deadEndDir = MazeDir.West,  label = "secondo bivio" },
            new JunctionDef { cell = new Vector2Int(2, 2), correctDir = MazeDir.East,  deadEndDir = MazeDir.South, label = "terzo bivio" },
        };

        [Header("Varchi nel perimetro")]
        [Tooltip("Lato da cui si entra nella prima cella del percorso. Il perimetro e' chiuso ovunque tranne qui e all'uscita: il partecipante trova l'ingresso seguendo il muro esterno col dito.")]
        [SerializeField] private MazeDir entranceSide = MazeDir.South;

        [Tooltip("Lato da cui si esce dall'ultima cella del percorso.")]
        [SerializeField] private MazeDir exitSide = MazeDir.East;

        [Header("Orientamento rispetto al partecipante")]
        [Tooltip("Il layout e' scritto in COORDINATE DEL PARTECIPANTE: lui sta a -z e guarda verso +z, " +
                 "quindi z che cresce = piu' lontano da lui, e +x e' la sua destra. Questo angolo (gradi, " +
                 "attorno a Y) porta quelle coordinate in quelle del tavolo. In Labyrinth.unity la " +
                 "FrontalCamera sta a z=+0.8 e guarda verso -z, cioe' il partecipante e' dalla parte " +
                 "opposta: serve 180.")]
        [SerializeField] private float participantYaw = 180f;

        [Header("Mano dominante")]
        [Tooltip("Ribalta il labirinto sull'asse X (ingresso a sinistra invece che a destra) per i mancini.")]
        [SerializeField] private bool mirror = false;

        public int Columns => columns;
        public int Rows => rows;
        public float CorridorWidth => corridorWidth;
        public float WallThickness => wallThickness;
        public float WallHeight => wallHeight;
        public float TableTopY => tableTopY;
        public Vector2 Center => center;
        public float ParticipantYaw => participantYaw;
        public bool Mirror => mirror;
        public IReadOnlyList<JunctionDef> Junctions => junctions;

        public float TotalWidth => columns * corridorWidth + (columns + 1) * wallThickness;
        public float TotalDepth => rows * corridorWidth + (rows + 1) * wallThickness;

        // --- Costruzione della mappa ---------------------------------------------------

        // Risolve celle, varchi e percorso e restituisce il modello interrogabile.
        public MazeMap BuildMap()
        {
            var path = new List<Vector2Int>(pathCells.Count);
            foreach (var c in pathCells) path.Add(Flip(c));

            var kinds = new CellKind[columns, rows];
            for (int c = 0; c < columns; c++)
                for (int r = 0; r < rows; r++)
                    kinds[c, r] = CellKind.Wall;

            foreach (var c in path)
                if (InGrid(c)) kinds[c.x, c.y] = CellKind.Corridor;

            var openings = new HashSet<Vector2Int>();

            // Varchi lungo il percorso corretto.
            for (int k = 0; k + 1 < path.Count; k++)
            {
                if (TryDirBetween(path[k], path[k + 1], out MazeDir d))
                    openings.Add(MazeMap.OpeningCoord(path[k].x, path[k].y, d));
            }

            // Bivi: il vicolo cieco diventa una cella vera, e le due celle all'imbocco
            // dei rami diventano piastrelle di scelta.
            foreach (var j in junctions)
            {
                var cell = Flip(j.cell);
                var correct = FlipDir(j.correctDir);
                var dead = FlipDir(j.deadEndDir);

                var deadCell = MazeMap.Neighbor(cell.x, cell.y, dead);
                if (InGrid(deadCell))
                {
                    kinds[deadCell.x, deadCell.y] = CellKind.ChoiceTile;
                    openings.Add(MazeMap.OpeningCoord(cell.x, cell.y, dead));
                }

                var correctCell = MazeMap.Neighbor(cell.x, cell.y, correct);
                if (InGrid(correctCell) && kinds[correctCell.x, correctCell.y] == CellKind.Corridor)
                    kinds[correctCell.x, correctCell.y] = CellKind.ChoiceTile;
            }

            // Ingresso e uscita: due varchi nel perimetro, altrimenti chiuso.
            if (path.Count > 0)
            {
                var first = path[0];
                var last = path[path.Count - 1];
                kinds[first.x, first.y] = CellKind.Entrance;
                kinds[last.x, last.y] = CellKind.Exit;
                openings.Add(MazeMap.OpeningCoord(first.x, first.y, FlipDir(entranceSide)));
                openings.Add(MazeMap.OpeningCoord(last.x, last.y, FlipDir(exitSide)));
            }

            return new MazeMap(columns, rows, corridorWidth, wallThickness, wallHeight,
                               tableTopY, center, kinds, openings, path);
        }

        // Il bivio k-esimo gia' ribaltato per la mano dominante.
        public bool TryGetJunction(int index, out Vector2Int cell, out MazeDir correctDir,
                                   out MazeDir deadEndDir, out string label)
        {
            cell = Vector2Int.zero; correctDir = MazeDir.North; deadEndDir = MazeDir.South; label = null;
            if (index < 0 || index >= junctions.Count) return false;
            var j = junctions[index];
            cell = Flip(j.cell);
            correctDir = FlipDir(j.correctDir);
            deadEndDir = FlipDir(j.deadEndDir);
            label = j.label;
            return true;
        }

        public int JunctionCount => junctions.Count;

        // --- Verifica ---------------------------------------------------------------------

        // Errori e note sono cose diverse: solo gli errori bloccano la generazione.
        // La logica sta in MazeLayoutValidator, che e' una classe pura e si puo' provare
        // fuori da Unity (Tools/MazeMapTest).
        public bool Validate(out string errors, out string notes)
        {
            var specs = new List<JunctionSpec>(junctions.Count);
            foreach (var j in junctions) specs.Add(new JunctionSpec(j.cell, j.correctDir, j.deadEndDir));
            return MazeLayoutValidator.Validate(columns, rows, pathCells, specs, out errors, out notes);
        }

        public bool Validate(out string report)
        {
            bool ok = Validate(out string errors, out string notes);
            if (ok) report = string.IsNullOrEmpty(notes) ? "Layout valido." : "Layout valido. Note:\n" + notes;
            else report = errors;
            return ok;
        }

        // --- Utilita' ---------------------------------------------------------------------

        private bool InGrid(Vector2Int c) => c.x >= 0 && c.x < columns && c.y >= 0 && c.y < rows;

        private Vector2Int Flip(Vector2Int c) => mirror ? new Vector2Int(columns - 1 - c.x, c.y) : c;

        private MazeDir FlipDir(MazeDir d)
        {
            if (!mirror) return d;
            if (d == MazeDir.East) return MazeDir.West;
            if (d == MazeDir.West) return MazeDir.East;
            return d;
        }

        private static bool TryDirBetween(Vector2Int from, Vector2Int to, out MazeDir dir) =>
            MazeLayoutValidator.TryDirBetween(from, to, out dir);
    }
}
