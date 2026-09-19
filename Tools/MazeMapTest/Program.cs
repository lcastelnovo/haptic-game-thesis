using System;
using System.Collections.Generic;
using UnityEngine;
using HapticResearch.Labyrinth;

// Banco di prova della sola aritmetica di MazeMap, eseguito FUORI da Unity.
// Replica la risoluzione del layout (gia' verificata a parte) e poi controlla le
// proprieta' geometriche che in Scene view non si notano.
static class Program
{
    const int COLS = 6, ROWS = 3;
    const float W = 0.07f, T = 0.03f, H = 0.10f, TOP = 0.85f;
    static readonly Vector2 CENTER = new Vector2(0f, -0.195f);

    static readonly Vector2Int[] PATH = {
        new Vector2Int(5,0), new Vector2Int(4,0), new Vector2Int(3,0), new Vector2Int(2,0),
        new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(1,2), new Vector2Int(2,2),
        new Vector2Int(3,2), new Vector2Int(4,2), new Vector2Int(5,2) };

    static readonly (Vector2Int cell, MazeDir correct, MazeDir dead)[] JUNCTIONS = {
        (new Vector2Int(3,0), MazeDir.West,  MazeDir.North),
        (new Vector2Int(1,0), MazeDir.North, MazeDir.West),
        (new Vector2Int(2,2), MazeDir.East,  MazeDir.South) };

    static int failures = 0;
    static void Check(bool ok, string what) { if (!ok) { Console.WriteLine("  FALLITO: " + what); failures++; } }

    static MazeMap Build()
    {
        var kinds = new CellKind[COLS, ROWS];
        for (int c = 0; c < COLS; c++) for (int r = 0; r < ROWS; r++) kinds[c, r] = CellKind.Wall;
        var path = new List<Vector2Int>(PATH);
        foreach (var c in path) kinds[c.x, c.y] = CellKind.Corridor;

        var openings = new HashSet<Vector2Int>();
        for (int k = 0; k + 1 < path.Count; k++)
        {
            var a = path[k]; var b = path[k + 1];
            MazeDir d = b.x > a.x ? MazeDir.East : b.x < a.x ? MazeDir.West : b.y > a.y ? MazeDir.North : MazeDir.South;
            openings.Add(MazeMap.OpeningCoord(a.x, a.y, d));
        }
        foreach (var j in JUNCTIONS)
        {
            var dead = MazeMap.Neighbor(j.cell.x, j.cell.y, j.dead);
            kinds[dead.x, dead.y] = CellKind.ChoiceTile;
            openings.Add(MazeMap.OpeningCoord(j.cell.x, j.cell.y, j.dead));
            var cor = MazeMap.Neighbor(j.cell.x, j.cell.y, j.correct);
            if (kinds[cor.x, cor.y] == CellKind.Corridor) kinds[cor.x, cor.y] = CellKind.ChoiceTile;
        }
        kinds[PATH[0].x, PATH[0].y] = CellKind.Entrance;
        kinds[PATH[PATH.Length - 1].x, PATH[PATH.Length - 1].y] = CellKind.Exit;
        openings.Add(MazeMap.OpeningCoord(PATH[0].x, PATH[0].y, MazeDir.South));
        openings.Add(MazeMap.OpeningCoord(PATH[PATH.Length - 1].x, PATH[PATH.Length - 1].y, MazeDir.East));

        return new MazeMap(COLS, ROWS, W, T, H, TOP, CENTER, kinds, openings, path);
    }

    // Conta i varchi scorrendo tutte le coppie di celle adiacenti + il perimetro.
    static int CountOpenings(MazeMap m)
    {
        int n = 0;
        for (int c = 0; c < COLS; c++)
            for (int r = 0; r < ROWS; r++)
                foreach (var d in new[] { MazeDir.North, MazeDir.East, MazeDir.South, MazeDir.West })
                {
                    if (!m.IsOpen(c, r, d)) continue;
                    var nb = MazeMap.Neighbor(c, r, d);
                    // ogni varco interno si vede da entrambe le celle: si conta una volta sola
                    bool inside = nb.x >= 0 && nb.x < COLS && nb.y >= 0 && nb.y < ROWS;
                    if (!inside || d == MazeDir.North || d == MazeDir.East) n++;
                }
        return n;
    }

    // Dove finisce il labirinto DAVANTI AL PARTECIPANTE.
    //
    // Il layout e' scritto in coordinate del partecipante (lui a -z che guarda verso +z,
    // la sua destra a +x). Nella scena Labyrinth.unity il partecipante e' dalla parte
    // opposta: FrontalCamera a z=+0.8 che guarda verso -z, RightCalibrationTarget a
    // z=+0.291. Quindi il bordo vicino a lui e' z=+0.4 e serve participantYaw=180.
    //
    // Questo controllo esiste perche' il segno era stato sbagliato e il labirinto era
    // finito nella meta' LONTANA del tavolo: da seduto, la parte che non si raggiunge.
    static void ParticipantFrameTests()
    {
        Console.WriteLine("\n-- posizione davanti al partecipante --");
        const float YAW = 180f;
        const float NEAR_EDGE = 0.4f;   // bordo del tavolo dal lato del partecipante
        const float TABLE_HALF_X = 0.75f;

        var m = Build();
        // Rotazione attorno a Y fatta a mano: Quaternion.Euler chiama il runtime nativo
        // di Unity, che fuori dall'editor non c'e'.
        double rad = YAW * Math.PI / 180.0;
        float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
        Vector3 ToWorld(Vector3 l) => new Vector3(l.x * cos + l.z * sin, l.y, -l.x * sin + l.z * cos);

        // angoli del labirinto in coordinate tavolo
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var lx in new[] { m.Origin.x, m.Origin.x + m.TotalWidth })
            foreach (var lz in new[] { m.Origin.y, m.Origin.y + m.TotalDepth })
            {
                var w = ToWorld(new Vector3(lx, 0f, lz));
                minX = Math.Min(minX, w.x); maxX = Math.Max(maxX, w.x);
                minZ = Math.Min(minZ, w.z); maxZ = Math.Max(maxZ, w.z);
            }
        Console.WriteLine($"ingombro sul tavolo: x [{minX:+0.000;-0.000}, {maxX:+0.000;-0.000}]  z [{minZ:+0.000;-0.000}, {maxZ:+0.000;-0.000}]");

        Check(minX > -TABLE_HALF_X && maxX < TABLE_HALF_X, "il labirinto sta dentro il tavolo in x");
        Check(maxZ < NEAR_EDGE, "il labirinto non sborda dal bordo vicino");
        Check(minZ > 0f, "il labirinto sta nella meta' VICINA al partecipante (z > 0)");

        float distanzaMax = NEAR_EDGE - minZ;
        Console.WriteLine($"punto piu' lontano dal bordo vicino: {distanzaMax * 100:0.#} cm");
        Check(distanzaMax <= 0.40f, $"tutto entro 40 cm di allungo ({distanzaMax * 100:0.#} cm)");

        // L'ingresso deve stare sul bordo vicino, dal lato della mano dominante.
        // Guardando verso -z, la destra del partecipante e' -x.
        var ing = ToWorld(m.CellCenter(m.EntranceCell.x, m.EntranceCell.y));
        Console.WriteLine($"ingresso in ({ing.x:+0.000;-0.000}, {ing.z:+0.000;-0.000}) = {(ing.x < 0 ? "destra" : "sinistra")} del partecipante, {(NEAR_EDGE - ing.z) * 100:0.#} cm dal bordo");
        Check(ing.z > (minZ + maxZ) / 2f, "l'ingresso e' nella fascia vicina al partecipante");
        Check(ing.x < 0f, "l'ingresso e' dal lato della mano destra (x negativo guardando verso -z)");

        var usc = ToWorld(m.CellCenter(m.ExitCell.x, m.ExitCell.y));
        Console.WriteLine($"uscita   in ({usc.x:+0.000;-0.000}, {usc.z:+0.000;-0.000}), {(NEAR_EDGE - usc.z) * 100:0.#} cm dal bordo");
        Check(NEAR_EDGE - usc.z <= 0.40f, "anche l'uscita e' raggiungibile");
    }

    // La validazione del layout: errori che devono bloccare, note che NON devono.
    static void ValidatorTests()
    {
        Console.WriteLine("\n-- validazione layout --");
        var path = new List<Vector2Int>(PATH);
        var specs = new List<JunctionSpec>();
        foreach (var j in JUNCTIONS) specs.Add(new JunctionSpec(j.cell, j.correct, j.dead));

        bool ok = MazeLayoutValidator.Validate(COLS, ROWS, path, specs, out string errors, out string notes);
        int noteCount = notes.Length == 0 ? 0 : notes.Split('\n').Length;
        Console.WriteLine($"layout reale: valido={ok}, errori={(errors.Length == 0 ? "nessuno" : errors)}, note={noteCount}");
        Check(ok, "il layout reale deve essere VALIDO");
        Check(errors.Length == 0, "il layout reale non deve avere errori");
        // Le tasche cieche confinano col percorso: e' normale in un labirinto compatto e
        // deve restare una nota, non un errore. E' il bug che ha bloccato la generazione.
        Check(noteCount == 3, $"attese 3 note informative, trovate {noteCount}");

        // --- casi che DEVONO fallire ---
        void MustFail(string what, List<Vector2Int> pp, List<JunctionSpec> jj)
        {
            bool v = MazeLayoutValidator.Validate(COLS, ROWS, pp, jj, out string e, out _);
            Check(!v, $"doveva essere rifiutato: {what}");
            if (!v) Console.WriteLine($"  rifiutato come atteso: {what}");
        }

        var loop = new List<Vector2Int>(PATH); loop.Add(PATH[3]);
        MustFail("cella ripetuta nel percorso", loop, specs);

        var gap = new List<Vector2Int> { new Vector2Int(0,0), new Vector2Int(3,2) };
        MustFail("celle non adiacenti", gap, new List<JunctionSpec>());

        var sharedDead = new List<JunctionSpec>(specs);
        sharedDead[2] = new JunctionSpec(new Vector2Int(2,2), MazeDir.East, MazeDir.South);
        sharedDead[0] = new JunctionSpec(new Vector2Int(3,0), MazeDir.West, MazeDir.North);
        var dup = new List<JunctionSpec>(specs);
        dup.Add(new JunctionSpec(new Vector2Int(2,0), MazeDir.West, MazeDir.North)); // (2,1) gia' di J3
        MustFail("due bivi sullo stesso vicolo cieco", path, dup);

        var wrongDir = new List<JunctionSpec>(specs);
        wrongDir[0] = new JunctionSpec(new Vector2Int(3,0), MazeDir.East, MazeDir.North);
        MustFail("direzione corretta che non segue il percorso", path, wrongDir);

        var deadOnPath = new List<JunctionSpec>(specs);
        deadOnPath[0] = new JunctionSpec(new Vector2Int(3,0), MazeDir.West, MazeDir.East);
        MustFail("vicolo cieco che sta sul percorso", path, deadOnPath);

        // Il bivio originale del piano: l'imbocco giusto era l'ultima cella, collassava.
        var collapsing = new List<JunctionSpec>(specs);
        collapsing[2] = new JunctionSpec(new Vector2Int(4,2), MazeDir.East, MazeDir.South);
        MustFail("bivio il cui ramo giusto sfocia subito nell'uscita", path, collapsing);
    }

    static int Main()
    {
        var m = Build();
        Console.WriteLine($"Ingombro {m.TotalWidth * 100:0.#} x {m.TotalDepth * 100:0.#} cm, origine ({m.Origin.x:0.000}, {m.Origin.y:0.000})");

        Check(Math.Abs(m.TotalWidth - 0.63f) < 1e-5, "larghezza totale 63 cm");
        Check(Math.Abs(m.TotalDepth - 0.33f) < 1e-5, "profondita' totale 33 cm");

        // 1) il centro di ogni cella aperta si rilocalizza su quella cella
        int open = 0;
        for (int c = 0; c < COLS; c++)
            for (int r = 0; r < ROWS; r++)
            {
                var kind = m.KindAt(c, r);
                var p = m.CellCenter(c, r);
                var res = m.Locate(p, out var got);
                if (kind == CellKind.Wall) { Check(res == LocateResult.InsideWall, $"cella piena ({c},{r}) deve dare InsideWall, invece {res}"); continue; }
                open++;
                Check(res == LocateResult.InCell, $"centro di ({c},{r}) deve dare InCell, invece {res}");
                Check(got == new Vector2Int(c, r), $"centro di ({c},{r}) rilocalizzato su {got.x},{got.y}");
            }
        Console.WriteLine($"celle aperte: {open}");
        Check(open == 14, "14 celle aperte");

        // 2) i quattro angoli interni di ogni cella aperta restano nella cella
        float q = W * 0.5f - 0.005f;
        for (int c = 0; c < COLS; c++)
            for (int r = 0; r < ROWS; r++)
            {
                if (m.KindAt(c, r) == CellKind.Wall) continue;
                var p = m.CellCenter(c, r);
                foreach (var d in new[] { new Vector2(q, q), new Vector2(-q, q), new Vector2(q, -q), new Vector2(-q, -q) })
                {
                    var res = m.Locate(new Vector3(p.x + d.x, p.y, p.z + d.y), out var got);
                    Check(res == LocateResult.InCell && got == new Vector2Int(c, r),
                          $"angolo di ({c},{r}) offset ({d.x:0.000},{d.y:0.000}) -> {res} {got.x},{got.y}");
                }
            }

        // 3) fuori dai bordi = OutsideBounds
        foreach (var p in new[] {
            new Vector3(m.Origin.x - 0.01f, TOP, CENTER.y),
            new Vector3(m.Origin.x + m.TotalWidth + 0.01f, TOP, CENTER.y),
            new Vector3(CENTER.x, TOP, m.Origin.y - 0.01f),
            new Vector3(CENTER.x, TOP, m.Origin.y + m.TotalDepth + 0.01f) })
            Check(m.Locate(p, out _) == LocateResult.OutsideBounds, $"punto esterno ({p.x:0.000},{p.z:0.000})");

        // 4) i blocchi di muro: dentro i bordi, non sovrapposti, e l'area torna
        var runs = m.WallRuns();
        Console.WriteLine($"blocchi di muro: {runs.Count}");
        float area = 0f;
        var rects = new List<(float x0, float x1, float z0, float z1)>();
        foreach (var run in runs)
        {
            Check(run.LocalSize.x > 0f && run.LocalSize.z > 0f, "blocco con lato nullo");
            Check(Math.Abs(run.LocalSize.y - H) < 1e-5, "altezza del blocco");
            Check(Math.Abs(run.LocalCenter.y - (TOP + H / 2f)) < 1e-5, "quota del blocco");
            float x0 = run.LocalCenter.x - run.LocalSize.x / 2f, x1 = run.LocalCenter.x + run.LocalSize.x / 2f;
            float z0 = run.LocalCenter.z - run.LocalSize.z / 2f, z1 = run.LocalCenter.z + run.LocalSize.z / 2f;
            Check(x0 >= m.Origin.x - 1e-4 && x1 <= m.Origin.x + m.TotalWidth + 1e-4, "blocco dentro i bordi in x");
            Check(z0 >= m.Origin.y - 1e-4 && z1 <= m.Origin.y + m.TotalDepth + 1e-4, "blocco dentro i bordi in z");
            foreach (var o in rects)
                Check(x1 <= o.x0 + 1e-5 || x0 >= o.x1 - 1e-5 || z1 <= o.z0 + 1e-5 || z0 >= o.z1 - 1e-5,
                      $"blocchi sovrapposti attorno a ({run.LocalCenter.x:0.000},{run.LocalCenter.z:0.000})");
            rects.Add((x0, x1, z0, z1));
            area += run.LocalSize.x * run.LocalSize.z;
        }
        // Area dei muri = area totale - celle aperte - VARCHI. I varchi sono caselle
        // della griglia fine larghe quanto un muro e lunghe quanto un corridoio: non
        // sono celle, ma sono spazio libero, e vanno scalati come le celle.
        int openings = CountOpenings(m);
        Console.WriteLine($"varchi: {openings}");
        Check(openings == 15, "15 varchi (13 interni + ingresso + uscita)");
        float expected = m.TotalWidth * m.TotalDepth - open * W * W - openings * T * W;
        Console.WriteLine($"area muri {area:0.00000} m2, attesa {expected:0.00000} m2");
        Check(Math.Abs(area - expected) < 1e-5, "l'area dei muri copre esattamente cio' che non e' corridoio");

        // 5) il centro di ogni blocco di muro si localizza dentro un muro
        foreach (var run in runs)
            Check(m.Locate(run.LocalCenter, out _) == LocateResult.InsideWall,
                  $"centro del blocco ({run.LocalCenter.x:0.000},{run.LocalCenter.z:0.000}) non risulta muro");

        // 6) i varchi: dal centro di una cella verso la successiva del percorso si passa
        for (int k = 0; k + 1 < PATH.Length; k++)
        {
            var a = PATH[k]; var b = PATH[k + 1];
            var pa = m.CellCenter(a.x, a.y); var pb = m.CellCenter(b.x, b.y);
            var mid = new Vector3((pa.x + pb.x) / 2f, pa.y, (pa.z + pb.z) / 2f);
            var res = m.Locate(mid, out _);
            Check(res == LocateResult.InOpening, $"il varco fra ({a.x},{a.y}) e ({b.x},{b.y}) da' {res}");
        }

        // 7) ingresso e uscita
        Check(m.EntranceCell == PATH[0], "cella d'ingresso");
        Check(m.ExitCell == PATH[PATH.Length - 1], "cella d'uscita");
        Check(m.IsOpen(PATH[0].x, PATH[0].y, MazeDir.South), "varco d'ingresso aperto");
        Check(m.IsOpen(m.ExitCell.x, m.ExitCell.y, MazeDir.East), "varco d'uscita aperto");
        Check(!m.IsOpen(0, 2, MazeDir.North), "il perimetro e' chiuso altrove");

        ValidatorTests();
        ParticipantFrameTests();

        Console.WriteLine(failures == 0 ? "\nTUTTI I CONTROLLI PASSATI" : $"\n{failures} CONTROLLI FALLITI");
        return failures == 0 ? 0 : 1;
    }
}
