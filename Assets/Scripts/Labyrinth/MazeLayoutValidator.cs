using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HapticResearch.Labyrinth
{
    // Un bivio in forma minima, senza dipendere dal ScriptableObject: cosi' la validazione
    // si puo' eseguire anche fuori da Unity (vedi Tools/MazeMapTest).
    public struct JunctionSpec
    {
        public Vector2Int Cell;
        public MazeDir CorrectDir;
        public MazeDir DeadEndDir;

        public JunctionSpec(Vector2Int cell, MazeDir correct, MazeDir deadEnd)
        { Cell = cell; CorrectDir = correct; DeadEndDir = deadEnd; }
    }

    // Controlla che un layout sia sensato PRIMA di generare la geometria. Un labirinto con
    // un anello, o con un vicolo cieco che sbuca sul percorso, e' inservibile per chi non
    // vede e in Scene view non si nota: sembra un labirinto normale.
    //
    // Classe pura e statica: e' la parte del layout che vale la pena provare davvero, e un
    // ScriptableObject non si istanzia fuori da Unity.
    public static class MazeLayoutValidator
    {
        // ERRORI e NOTE sono cose diverse e vanno tenute separate.
        //
        // Un errore blocca la generazione. Una nota descrive una situazione normale ma
        // fragile: p.es. una tasca cieca che confina con il percorso, dove l'unica cosa
        // che impedisce la scorciatoia e' il muro che li separa. In un labirinto compatto
        // succede quasi sempre e non e' un difetto; serve saperlo se un domani si sposta
        // il percorso.
        public static bool Validate(int columns, int rows, IReadOnlyList<Vector2Int> path,
                                    IReadOnlyList<JunctionSpec> junctions,
                                    out string errors, out string notes)
        {
            var err = new StringBuilder();
            var note = new StringBuilder();

            bool InGrid(Vector2Int c) => c.x >= 0 && c.x < columns && c.y >= 0 && c.y < rows;

            if (path == null || path.Count < 2) err.AppendLine("Il percorso deve avere almeno due celle.");
            path ??= new List<Vector2Int>();
            junctions ??= new List<JunctionSpec>();

            foreach (var c in path)
                if (!InGrid(c)) err.AppendLine($"Cella di percorso fuori griglia: {c}.");

            for (int k = 0; k + 1 < path.Count; k++)
                if (!TryDirBetween(path[k], path[k + 1], out _))
                    err.AppendLine($"Le celle {path[k]} e {path[k + 1]} non sono adiacenti.");

            var seen = new HashSet<Vector2Int>();
            foreach (var c in path)
                if (!seen.Add(c)) err.AppendLine($"La cella {c} compare due volte nel percorso: si creerebbe un anello.");

            var deadEnds = new HashSet<Vector2Int>();
            for (int k = 0; k < junctions.Count; k++)
            {
                var j = junctions[k];
                int idx = IndexOf(path, j.Cell);
                if (idx < 0) { err.AppendLine($"Il bivio {k + 1} e' su {j.Cell}, che non sta sul percorso."); continue; }
                if (idx + 1 >= path.Count) { err.AppendLine($"Il bivio {k + 1} e' sull'ultima cella: non ha un ramo corretto."); continue; }

                var expected = path[idx + 1];
                var actual = MazeMap.Neighbor(j.Cell.x, j.Cell.y, j.CorrectDir);
                if (actual != expected)
                    err.AppendLine($"Il bivio {k + 1}: la direzione corretta punta a {actual}, ma il percorso prosegue su {expected}.");

                // Il ramo giusto deve avere una cella OLTRE l'imbocco: se l'imbocco e' gia'
                // l'ultima cella, non c'e' modo di "impegnarsi" nella scelta e il bivio
                // si risolverebbe da solo nel momento in cui lo si tocca.
                int ci = IndexOf(path, actual);
                if (ci >= 0 && ci + 1 >= path.Count)
                    err.AppendLine($"Il bivio {k + 1}: l'imbocco giusto {actual} e' l'ultima cella del percorso, il bivio collasserebbe.");

                var dead = MazeMap.Neighbor(j.Cell.x, j.Cell.y, j.DeadEndDir);
                if (!InGrid(dead)) err.AppendLine($"Il bivio {k + 1}: il vicolo cieco {dead} e' fuori griglia.");
                else if (Contains(path, dead)) err.AppendLine($"Il bivio {k + 1}: il vicolo cieco {dead} sta sul percorso, sarebbe una scorciatoia.");
                else if (!deadEnds.Add(dead)) err.AppendLine($"Il vicolo cieco {dead} e' usato da due bivi: si creerebbe un anello.");

                if (j.DeadEndDir == j.CorrectDir)
                    err.AppendLine($"Il bivio {k + 1}: ramo corretto e vicolo cieco puntano nella stessa direzione.");
            }

            foreach (var dead in deadEnds)
                foreach (var d in AllDirs)
                {
                    var n = MazeMap.Neighbor(dead.x, dead.y, d);
                    if (InGrid(n) && Contains(path, n) && !IsJunctionOf(junctions, dead, n))
                        note.AppendLine($"Il vicolo cieco {dead} confina con la cella di percorso {n}: li separa solo un muro.");
                }

            errors = err.ToString().TrimEnd();
            notes = note.ToString().TrimEnd();
            return err.Length == 0;
        }

        private static readonly MazeDir[] AllDirs = { MazeDir.North, MazeDir.East, MazeDir.South, MazeDir.West };

        private static int IndexOf(IReadOnlyList<Vector2Int> list, Vector2Int v)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == v) return i;
            return -1;
        }

        private static bool Contains(IReadOnlyList<Vector2Int> list, Vector2Int v) => IndexOf(list, v) >= 0;

        private static bool IsJunctionOf(IReadOnlyList<JunctionSpec> junctions, Vector2Int deadEnd, Vector2Int cell)
        {
            foreach (var j in junctions)
                if (j.Cell == cell && MazeMap.Neighbor(j.Cell.x, j.Cell.y, j.DeadEndDir) == deadEnd) return true;
            return false;
        }

        public static bool TryDirBetween(Vector2Int from, Vector2Int to, out MazeDir dir)
        {
            dir = MazeDir.North;
            int dx = to.x - from.x, dy = to.y - from.y;
            if (dx == 1 && dy == 0) { dir = MazeDir.East; return true; }
            if (dx == -1 && dy == 0) { dir = MazeDir.West; return true; }
            if (dx == 0 && dy == 1) { dir = MazeDir.North; return true; }
            if (dx == 0 && dy == -1) { dir = MazeDir.South; return true; }
            return false;
        }
    }
}
