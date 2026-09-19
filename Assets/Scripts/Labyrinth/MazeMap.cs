using System.Collections.Generic;
using UnityEngine;

namespace HapticResearch.Labyrinth
{
    // Direzioni sul piano del tavolo. North = +Z (lontano dal partecipante), East = +X.
    public enum MazeDir { North, East, South, West }

    // Che cosa c'e' in una cella-corridoio.
    // Wall = cella non usata dal percorso: e' riempita di muro.
    public enum CellKind { Wall, Corridor, ChoiceTile, Entrance, Exit }

    // Esito della localizzazione della punta del dito sul piano del labirinto.
    public enum LocateResult { OutsideBounds, InsideWall, InCell, InOpening }

    // Un blocco di muro gia' fuso con i suoi vicini, pronto da istanziare.
    // Le coordinate sono LOCALI al tavolo, non in spazio mondo.
    public readonly struct WallRun
    {
        public readonly Vector3 LocalCenter;
        public readonly Vector3 LocalSize;
        public WallRun(Vector3 center, Vector3 size) { LocalCenter = center; LocalSize = size; }
    }

    // Modello del labirinto: griglia di celle-corridoio separate da muri, interrogabile
    // in O(1) sia per generare la geometria sia per sapere dov'e' il dito a runtime.
    //
    // Classe pura, senza MonoBehaviour e senza stato di scena: si costruisce dal
    // MazeLayoutAsset e si puo' verificare in isolamento.
    //
    // Internamente usa una GRIGLIA FINE (2*Columns+1) x (2*Rows+1) in cui gli indici
    // alternano linee di muro (pari) e corsie di corridoio (dispari):
    //
    //   i pari   -> linea di muro spessa WallThickness
    //   i dispari-> corsia di corridoio larga CorridorWidth, colonna c = (i-1)/2
    //
    // Cosi' un varco fra due celle e' semplicemente una casella della griglia fine
    // marcata come non solida, e perimetro, ingresso e uscita non sono casi speciali.
    public sealed class MazeMap
    {
        public int Columns { get; }
        public int Rows { get; }
        public float CorridorWidth { get; }
        public float WallThickness { get; }
        public float WallHeight { get; }
        public float TableTopY { get; }

        // Passo di una cella: corridoio + un muro.
        public float Pitch => CorridorWidth + WallThickness;

        public float TotalWidth => Columns * CorridorWidth + (Columns + 1) * WallThickness;
        public float TotalDepth => Rows * CorridorWidth + (Rows + 1) * WallThickness;

        // Angolo (x,z) minimo del labirinto, in coordinate locali al tavolo.
        public Vector2 Origin { get; }

        private readonly CellKind[,] cells;          // [Columns, Rows]
        private readonly bool[,] solid;              // griglia fine
        private readonly List<Vector2Int> path;      // percorso corretto, in ordine
        private readonly Dictionary<Vector2Int, int> pathIndex;

        public IReadOnlyList<Vector2Int> Path => path;
        public Vector2Int EntranceCell => path.Count > 0 ? path[0] : Vector2Int.zero;
        public Vector2Int ExitCell => path.Count > 0 ? path[path.Count - 1] : Vector2Int.zero;

        // Costruttore: riceve gia' risolti celle, varchi e percorso (li calcola il layout).
        // openings e' in coordinate della griglia FINE.
        public MazeMap(int columns, int rows, float corridorWidth, float wallThickness,
                       float wallHeight, float tableTopY, Vector2 center,
                       CellKind[,] cellKinds, HashSet<Vector2Int> openings, List<Vector2Int> pathCells)
        {
            Columns = Mathf.Max(1, columns);
            Rows = Mathf.Max(1, rows);
            CorridorWidth = corridorWidth;
            WallThickness = wallThickness;
            WallHeight = wallHeight;
            TableTopY = tableTopY;
            Origin = new Vector2(center.x - TotalWidth * 0.5f, center.y - TotalDepth * 0.5f);

            cells = cellKinds;
            path = pathCells ?? new List<Vector2Int>();
            pathIndex = new Dictionary<Vector2Int, int>(path.Count);
            for (int k = 0; k < path.Count; k++) pathIndex[path[k]] = k;

            solid = BuildSolidGrid(openings ?? new HashSet<Vector2Int>());
        }

        // --- Celle -----------------------------------------------------------------

        public bool InGrid(int col, int row) => col >= 0 && col < Columns && row >= 0 && row < Rows;

        public CellKind KindAt(int col, int row) => InGrid(col, row) ? cells[col, row] : CellKind.Wall;

        public bool IsOnPath(int col, int row) => pathIndex.ContainsKey(new Vector2Int(col, row));

        // Posizione lungo il percorso corretto, -1 se la cella non ci sta sopra.
        public int PathIndexOf(int col, int row) =>
            pathIndex.TryGetValue(new Vector2Int(col, row), out int k) ? k : -1;

        // Centro della cella in coordinate locali al tavolo, appoggiato al piano.
        public Vector3 CellCenter(int col, int row) => new Vector3(
            Origin.x + OffsetStart(2 * col + 1, CorridorWidth, WallThickness) + CorridorWidth * 0.5f,
            TableTopY,
            Origin.y + OffsetStart(2 * row + 1, CorridorWidth, WallThickness) + CorridorWidth * 0.5f);

        public static Vector2Int Neighbor(int col, int row, MazeDir dir)
        {
            switch (dir)
            {
                case MazeDir.North: return new Vector2Int(col, row + 1);
                case MazeDir.South: return new Vector2Int(col, row - 1);
                case MazeDir.East: return new Vector2Int(col + 1, row);
                default: return new Vector2Int(col - 1, row);
            }
        }

        public static MazeDir Opposite(MazeDir dir)
        {
            switch (dir)
            {
                case MazeDir.North: return MazeDir.South;
                case MazeDir.South: return MazeDir.North;
                case MazeDir.East: return MazeDir.West;
                default: return MazeDir.East;
            }
        }

        // Coordinate nella griglia fine del varco fra una cella e il suo vicino.
        public static Vector2Int OpeningCoord(int col, int row, MazeDir dir)
        {
            switch (dir)
            {
                case MazeDir.North: return new Vector2Int(2 * col + 1, 2 * row + 2);
                case MazeDir.South: return new Vector2Int(2 * col + 1, 2 * row);
                case MazeDir.East: return new Vector2Int(2 * col + 2, 2 * row + 1);
                default: return new Vector2Int(2 * col, 2 * row + 1);
            }
        }

        public bool IsOpen(int col, int row, MazeDir dir)
        {
            var f = OpeningCoord(col, row, dir);
            return InFine(f.x, f.y) && !solid[f.x, f.y];
        }

        // --- Localizzazione del dito -------------------------------------------------

        // Dove si trova un punto espresso in coordinate LOCALI al tavolo (la y viene
        // ignorata: il gioco vive sul piano XZ). Per InOpening la cella restituita e'
        // quella piu' vicina fra le due che il varco collega.
        public LocateResult Locate(Vector3 local, out Vector2Int cell)
        {
            cell = Vector2Int.zero;

            if (!TryFineIndex(local.x - Origin.x, Columns, out int i)) return LocateResult.OutsideBounds;
            if (!TryFineIndex(local.z - Origin.y, Rows, out int j)) return LocateResult.OutsideBounds;

            if (solid[i, j]) return LocateResult.InsideWall;

            bool iCorridor = (i & 1) == 1;
            bool jCorridor = (j & 1) == 1;

            if (iCorridor && jCorridor)
            {
                cell = new Vector2Int((i - 1) / 2, (j - 1) / 2);
                return LocateResult.InCell;
            }

            // Varco: sta fra due celle. Si sceglie quella valida, o la piu' vicina.
            Vector2Int a, b;
            if (!iCorridor) { a = new Vector2Int(i / 2 - 1, (j - 1) / 2); b = new Vector2Int(i / 2, (j - 1) / 2); }
            else { a = new Vector2Int((i - 1) / 2, j / 2 - 1); b = new Vector2Int((i - 1) / 2, j / 2); }

            bool aOk = InGrid(a.x, a.y) && cells[a.x, a.y] != CellKind.Wall;
            bool bOk = InGrid(b.x, b.y) && cells[b.x, b.y] != CellKind.Wall;
            if (aOk && !bOk) cell = a;
            else if (bOk && !aOk) cell = b;
            else cell = NearerCell(local, a, b, aOk, bOk);

            return LocateResult.InOpening;
        }

        private Vector2Int NearerCell(Vector3 local, Vector2Int a, Vector2Int b, bool aOk, bool bOk)
        {
            if (!aOk && !bOk) return a; // varco verso l'esterno: resta la cella interna
            var ca = CellCenter(a.x, a.y);
            var cb = CellCenter(b.x, b.y);
            float da = (ca.x - local.x) * (ca.x - local.x) + (ca.z - local.z) * (ca.z - local.z);
            float db = (cb.x - local.x) * (cb.x - local.x) + (cb.z - local.z) * (cb.z - local.z);
            return da <= db ? a : b;
        }

        // --- Geometria ----------------------------------------------------------------

        // Blocchi di muro gia' fusi: per ogni riga della griglia fine si accorpano le
        // caselle solide consecutive in un solo box. Niente sovrapposizioni, niente
        // decine di cubetti: per una griglia 6x3 vengono una ventina di box.
        public List<WallRun> WallRuns()
        {
            var runs = new List<WallRun>();
            int fw = 2 * Columns + 1, fh = 2 * Rows + 1;
            float y = TableTopY + WallHeight * 0.5f;

            for (int j = 0; j < fh; j++)
            {
                float zStart = Origin.y + OffsetStart(j, CorridorWidth, WallThickness);
                float zSize = SizeAt(j, CorridorWidth, WallThickness);

                int i = 0;
                while (i < fw)
                {
                    if (!solid[i, j]) { i++; continue; }
                    int runStart = i;
                    while (i < fw && solid[i, j]) i++;
                    int runEndExclusive = i;

                    float xStart = Origin.x + OffsetStart(runStart, CorridorWidth, WallThickness);
                    float xEnd = Origin.x + OffsetStart(runEndExclusive - 1, CorridorWidth, WallThickness)
                                 + SizeAt(runEndExclusive - 1, CorridorWidth, WallThickness);

                    runs.Add(new WallRun(
                        new Vector3((xStart + xEnd) * 0.5f, y, zStart + zSize * 0.5f),
                        new Vector3(xEnd - xStart, WallHeight, zSize)));
                }
            }
            return runs;
        }

        // Footprint di una cella, usato per le piastrelle di scelta: quadrato di lato
        // CorridorWidth che va dal piano del tavolo alla cima dei muri, cosi' la punta
        // del dito (che sta a ~0.94) ci entra davvero.
        public void CellVolume(int col, int row, out Vector3 localCenter, out Vector3 localSize)
        {
            var c = CellCenter(col, row);
            localCenter = new Vector3(c.x, TableTopY + WallHeight * 0.5f, c.z);
            localSize = new Vector3(CorridorWidth, WallHeight, CorridorWidth);
        }

        // --- Griglia fine ---------------------------------------------------------------

        private bool InFine(int i, int j) => i >= 0 && i < 2 * Columns + 1 && j >= 0 && j < 2 * Rows + 1;

        private bool[,] BuildSolidGrid(HashSet<Vector2Int> openings)
        {
            int fw = 2 * Columns + 1, fh = 2 * Rows + 1;
            var g = new bool[fw, fh];

            for (int i = 0; i < fw; i++)
            {
                for (int j = 0; j < fh; j++)
                {
                    bool iCorridor = (i & 1) == 1;
                    bool jCorridor = (j & 1) == 1;

                    if (iCorridor && jCorridor)
                    {
                        // Cella: solida solo se il percorso non la usa.
                        g[i, j] = cells[(i - 1) / 2, (j - 1) / 2] == CellKind.Wall;
                    }
                    else if (!iCorridor && !jCorridor)
                    {
                        g[i, j] = true; // pilastro all'incrocio delle linee di muro
                    }
                    else
                    {
                        // Tratto di muro fra due celle: solido a meno che non sia un varco.
                        g[i, j] = !openings.Contains(new Vector2Int(i, j));
                    }
                }
            }
            return g;
        }

        // Offset dell'inizio della casella fine idx lungo un asse.
        private static float OffsetStart(int idx, float corridor, float wall)
        {
            int pairs = idx / 2;                       // quante coppie muro+corridoio complete
            float s = pairs * (corridor + wall);
            if ((idx & 1) == 1) s += wall;             // dentro un corridoio: si supera il muro
            return s;
        }

        private static float SizeAt(int idx, float corridor, float wall) =>
            (idx & 1) == 1 ? corridor : wall;

        // Indice nella griglia fine a partire da un offset lungo l'asse.
        // Restituisce false se l'offset cade fuori dal labirinto.
        private bool TryFineIndex(float offset, int count, out int idx)
        {
            idx = 0;
            float total = count * CorridorWidth + (count + 1) * WallThickness;
            if (offset < 0f || offset > total) return false;

            float pitch = Pitch;
            int q = Mathf.FloorToInt(offset / pitch);
            if (q >= count) { idx = 2 * count; return true; } // ultimo muro perimetrale

            float rem = offset - q * pitch;
            idx = rem < WallThickness ? 2 * q : 2 * q + 1;
            return true;
        }
    }
}
