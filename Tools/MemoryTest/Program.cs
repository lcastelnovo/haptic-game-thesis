using System;
using System.Collections.Generic;
using HapticResearch.Memory;

// Banco di prova del memory tattile, eseguito FUORI da Unity.
static class Program
{
    static int failures = 0;
    static void Check(bool ok, string what)
    {
        if (!ok) { Console.WriteLine("  FALLITO: " + what); failures++; }
    }

    static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

    static int Main()
    {
        GridTests();
        DwellTests();
        BoardTests();
        ValidatorTests();

        Console.WriteLine(failures == 0 ? "TUTTO OK" : $"{failures} controlli FALLITI");
        return failures == 0 ? 0 : 1;
    }

    static void GridTests()
    {
        Console.WriteLine("MemoryGridMap");
        var g = new MemoryGridMap(4, 3, 0.08f, 0.02f);

        Check(g.TileCount == 12, "dodici tessere");
        Check(Near(g.Width, 0.38f) && Near(g.Depth, 0.28f),
              $"ingombro 38x28 cm (e' {g.Width * 100:0.#}x{g.Depth * 100:0.#})");

        for (int t = 0; t < g.TileCount; t++)
        {
            g.Center(t, out float x, out float z);
            Check(g.Locate(x, z) == t, $"il centro della tessera {t} si rilocalizza in {t}");
            float h = 0.04f - 0.001f;   // appena dentro il bordo
            Check(g.Locate(x + h, z + h) == t && g.Locate(x - h, z - h) == t,
                  $"gli angoli della tessera {t} restano suoi");
        }

        g.Center(0, out float x0, out float z0);
        Check(g.Locate(x0 + 0.05f, z0) == MemoryGridMap.Gap, "fra due colonne c'e' tavolo nudo");
        Check(g.Locate(x0, z0 + 0.05f) == MemoryGridMap.Gap, "fra due righe c'e' tavolo nudo");
        Check(g.Locate(-0.2f, 0f) == MemoryGridMap.Outside, "a sinistra della griglia e' fuori");
        Check(g.Locate(0f, 0.15f) == MemoryGridMap.Outside, "oltre l'ultima riga e' fuori");

        Check(x0 < 0f && z0 < 0f, "la tessera 0 e' vicina e a sinistra per il partecipante");
        Check(g.ColumnOf(5) == 1 && g.RowOf(5) == 1 && g.Index(1, 1) == 5,
              "indice = riga * colonne + colonna");
    }

    // Frame finti da 50 ms, regolari.
    static void Step(DwellDetector d, int tile, float x, float z, float seconds)
    {
        int n = (int)Math.Round(seconds / 0.05f);
        for (int i = 0; i < n; i++) d.Update(tile, x, z, 0.05f);
    }

    static void DwellTests()
    {
        Console.WriteLine("DwellDetector");

        // 1) dito fermo oltre la sosta: un avvio, un completamento, nessun annullo
        {
            var d = new DwellDetector(1f, 0.015f, 0.25f);
            int started = 0, done = 0, cancelled = 0;
            d.Started += t => started++;
            d.Completed += t => done++;
            d.Cancelled += (t, s, r) => cancelled++;
            Step(d, 3, 0f, 0f, 1.5f);
            Check(started == 1 && done == 1 && cancelled == 0, "dito fermo 1,5 s: una sosta completata");
            Step(d, 3, 0f, 0f, 2f);
            Check(done == 1, "restando fermi non si ri-completa");
        }

        // 2) un tremolio entro il raggio non azzera
        {
            var d = new DwellDetector(1f, 0.015f, 0.25f);
            int done = 0;
            d.Completed += t => done++;
            for (int i = 0; i < 30; i++) d.Update(3, i % 2 == 0 ? 0.008f : -0.004f, 0.005f, 0.05f);
            Check(done == 1, "un tremolio di 1,2 cm non azzera la sosta");
        }

        // 3) uscire dalla tessera a meta' annulla, col motivo
        {
            var d = new DwellDetector(1f, 0.015f, 0.25f);
            string reason = null; int done = 0;
            d.Cancelled += (t, s, r) => reason = r;
            d.Completed += t => done++;
            Step(d, 3, 0f, 0f, 0.6f);
            Step(d, -1, 0f, 0f, 0.1f);
            Check(reason == "uscita" && done == 0, "uscire dalla tessera annulla (motivo: uscita)");
        }

        // 4) oltre il raggio annulla e riparte dal nuovo punto
        {
            var d = new DwellDetector(1f, 0.015f, 0.25f);
            string reason = null; int done = 0;
            d.Cancelled += (t, s, r) => reason = r;
            d.Completed += t => done++;
            Step(d, 3, 0f, 0f, 0.6f);
            Step(d, 3, 0.03f, 0f, 0.6f);
            Check(reason == "raggio" && done == 0, "muoversi di 3 cm annulla (motivo: raggio)");
            Step(d, 3, 0.03f, 0f, 0.6f);
            Check(done == 1, "e la sosta riparte dal nuovo punto");
        }

        // 5) far scorrere il dito non fa partire niente, nemmeno un annullo
        {
            var d = new DwellDetector(1f, 0.015f, 0.25f);
            int started = 0, cancelled = 0;
            d.Started += t => started++;
            d.Cancelled += (t, s, r) => cancelled++;
            for (int i = 0; i < 40; i++) d.Update(3, i * 0.005f, 0f, 0.05f);   // 10 cm/s
            Check(started == 0 && cancelled == 0, "dito che scorre a 10 cm/s: niente tono, niente log");
        }

        // 6) Reset annulla e fa ripartire da zero
        {
            var d = new DwellDetector(1f, 0.015f, 0.25f);
            string reason = null; int done = 0;
            d.Cancelled += (t, s, r) => reason = r;
            d.Completed += t => done++;
            Step(d, 3, 0f, 0f, 0.5f);
            d.Reset();
            Check(reason == "reset", "Reset a meta' sosta la annulla (motivo: reset)");
            Step(d, 3, 0f, 0f, 0.9f);
            Check(done == 0, "dopo Reset la sosta riparte da zero");
        }
    }

    static MemoryBoard NewBoard(int seed, bool open = false, float delay = 1.5f)
    {
        var tiles = new List<int>();
        for (int i = 0; i < 12; i++) tiles.Add(i);
        return new MemoryBoard(12, tiles, new[] { 0, 1, 2, 3, 4, 5 }, open, delay, seed);
    }

    static void TickBoard(MemoryBoard b, float seconds)
    {
        int n = (int)Math.Round(seconds / 0.1f);
        for (int i = 0; i < n; i++) b.Tick(0.1f);
    }

    // Le due tessere con la firma 'sig'.
    static (int, int) TilesOf(MemoryBoard b, int sig)
    {
        int first = -1;
        for (int t = 0; t < b.TileCount; t++)
        {
            if (b.SignatureOf(t) != sig) continue;
            if (first < 0) first = t; else return (first, t);
        }
        throw new InvalidOperationException($"firma {sig} non trovata due volte");
    }

    static void BoardTests()
    {
        Console.WriteLine("MemoryBoard");

        // 1) ogni firma due volte; stesso seed stessa disposizione, seed diverso no
        {
            var a = NewBoard(42); var b = NewBoard(42); var c = NewBoard(7);
            var counts = new int[6];
            bool same = true, differs = false;
            for (int t = 0; t < 12; t++)
            {
                counts[a.SignatureOf(t)]++;
                same &= a.SignatureOf(t) == b.SignatureOf(t);
                differs |= a.SignatureOf(t) != c.SignatureOf(t);
            }
            Check(Array.TrueForAll(counts, n => n == 2), "ogni firma compare esattamente due volte");
            Check(same, "stesso seed, stessa disposizione");
            Check(differs, "seed diverso, disposizione diversa");
        }

        // 2) coppia giusta: fuori gioco, conteggi, evento
        {
            var b = NewBoard(1);
            var (p, q) = TilesOf(b, 0);
            int ma = -1, mb = -1;
            b.Matched += (x, y) => { ma = x; mb = y; };
            Check(b.Flip(p) && b.StateOf(p) == TileState.Flipped && b.FirstFlipped == p, "la prima tessera si gira");
            Check(b.Flip(q), "la seconda si gira");
            Check(b.StateOf(p) == TileState.Matched && b.StateOf(q) == TileState.Matched, "coppia: entrambe fuori gioco");
            Check(b.PairsFound == 1 && b.Attempts == 1 && b.FirstFlipped == -1, "una coppia, un tentativo, turno chiuso");
            Check(ma == p && mb == q, "l'evento Matched porta le due tessere");
        }

        // 3) coppia sbagliata: evento subito, coperte dopo la pausa, niente flip nel mezzo
        {
            var b = NewBoard(1, delay: 1.5f);
            var (p0, _) = TilesOf(b, 0);
            var (p1, other) = TilesOf(b, 1);
            int mismatches = 0, covered = 0;
            b.Mismatched += info => mismatches++;
            b.Covered += (x, y) => covered++;
            b.Flip(p0); b.Flip(p1);
            Check(mismatches == 1 && b.Busy && b.Attempts == 1, "coppia sbagliata: evento e pausa");
            Check(!b.Flip(other), "durante la pausa non si gira niente");
            TickBoard(b, 1.0f);
            Check(b.StateOf(p0) == TileState.Flipped && covered == 0, "a 1 s sono ancora girate");
            TickBoard(b, 0.6f);
            Check(b.StateOf(p0) == TileState.Hidden && b.StateOf(p1) == TileState.Hidden && covered == 1,
                  "dopo 1,5 s tornano coperte");
            Check(!b.Busy && b.CanFlip(other), "finita la pausa si gioca di nuovo");
        }

        // 4) sosta ripetuta sulla tessera gia' girata o su una fuori gioco: ignorata
        {
            var b = NewBoard(1);
            var (p, q) = TilesOf(b, 0);
            b.Flip(p);
            Check(!b.Flip(p) && b.Attempts == 0 && b.FirstFlipped == p, "rigirare la stessa tessera non conta");
            b.Flip(q);
            Check(!b.Flip(p) && !b.CanFlip(q), "le tessere fuori gioco non si girano");
        }

        // 5) partnerSeenBefore / secondSeenBefore su una partita scriptata
        {
            var b = NewBoard(3, delay: 0.5f);
            var (a0, a1) = TilesOf(b, 0);
            var (b0, b1) = TilesOf(b, 1);
            var (c0, _) = TilesOf(b, 2);
            var log = new List<MismatchInfo>();
            b.Mismatched += info => log.Add(info);

            b.Flip(a0); b.Flip(b0); TickBoard(b, 1f);   // turno 1
            b.Flip(b1); b.Flip(a0); TickBoard(b, 1f);   // turno 2
            b.Flip(c0); b.Flip(a1); TickBoard(b, 1f);   // turno 3

            Check(log.Count == 3, "tre errori registrati");
            Check(!log[0].PartnerSeenBefore && !log[0].SecondSeenBefore, "turno 1: niente di gia' visto");
            Check(log[1].PartnerSeenBefore && log[1].SecondSeenBefore,
                  "turno 2: la compagna di b1 (b0) e la seconda (a0) erano gia' state girate");
            Check(!log[2].PartnerSeenBefore && !log[2].SecondSeenBefore,
                  "turno 3: la compagna di c0 e a1 non erano mai state girate");
        }

        // 6) partita aperta: le coperte si sentono; chiusa: no
        {
            var open = NewBoard(1, open: true);
            var closed = NewBoard(1, open: false);
            Check(open.FeelsSignature(0) && !closed.FeelsSignature(0), "coperta: firma solo nella partita aperta");
            closed.Flip(0);
            Check(closed.FeelsSignature(0), "girata: firma anche nella partita chiusa");
        }

        // 7) riscaldamento: tessere assenti, Completed una volta sola
        {
            var playing = new[] { 1, 2, 5, 6, 9, 10 };
            var b = new MemoryBoard(12, playing, new[] { 0, 1, 2 }, true, 1.5f, 5);
            Check(b.StateOf(0) == TileState.Absent && b.SignatureOf(0) == -1 && !b.CanFlip(0) && !b.FeelsSignature(0),
                  "le tessere fuori dal riscaldamento sono assenti");
            int completed = 0;
            b.Completed += () => completed++;
            for (int sig = 0; sig < 3; sig++)
            {
                var (p, q) = TilesOf(b, sig);
                b.Flip(p); b.Flip(q);
            }
            Check(b.IsComplete && completed == 1 && b.PairsFound == 3, "tre coppie: completata, un solo evento");
            Check(!b.CanFlip(1), "a partita finita non si gira niente");
        }

        // 8) costruttore: rifiuta dati incoerenti
        {
            bool threw = false;
            try { new MemoryBoard(12, new[] { 0, 1, 2 }, new[] { 0, 1 }, false, 1f, 1); }
            catch (ArgumentException) { threw = true; }
            Check(threw, "3 tessere per 2 coppie: rifiutato");

            threw = false;
            try { new MemoryBoard(12, new[] { 0, 0, 1, 2 }, new[] { 0, 1 }, false, 1f, 1); }
            catch (ArgumentException) { threw = true; }
            Check(threw, "tessera ripetuta: rifiutato");
        }
    }

    // I valori di MemoryLayout_v1 (Task 9): se cambiano li', cambiano qui.
    static MemoryLayoutInfo ValidInfo()
    {
        var l = new MemoryLayoutInfo
        {
            Columns = 4, Rows = 3, TileSize = 0.08f, TileGap = 0.02f,
            CenterX = 0f, CenterZ = 0.20f, YawDegrees = 180f,
            NearEdgeZ = 0.4f, TableHalfX = 0.75f, MaxReach = 0.40f,
            DwellSeconds = 1f, MinThermalDwellSeconds = 2.5f,
        };
        // CrushedRock = 10, TextileMedium = 9, ProfiledAluminiumMedium = 6 (WeArtCommon.TextureType)
        l.Signatures.Add(new SignatureInfo("roccia_dura", 10, 100f, 0.9f, false));
        l.Signatures.Add(new SignatureInfo("roccia_morbida", 10, 100f, 0.2f, false));
        l.Signatures.Add(new SignatureInfo("tessuto_duro", 9, 100f, 0.9f, false));
        l.Signatures.Add(new SignatureInfo("tessuto_morbido", 9, 100f, 0.2f, false));
        l.Signatures.Add(new SignatureInfo("metallo_duro", 6, 100f, 0.9f, false));
        l.Signatures.Add(new SignatureInfo("metallo_morbido", 6, 100f, 0.2f, false));
        l.WarmupIds.AddRange(new[] { "roccia_dura", "roccia_morbida", "tessuto_duro" });
        l.WarmupTiles.AddRange(new[] { 1, 2, 5, 6, 9, 10 });
        return l;
    }

    static void Rejected(MemoryLayoutInfo l, string what)
    {
        bool ok = MemoryLayoutValidator.Validate(l, out string error);
        Check(!ok, $"rifiutato: {what}");
        if (!ok) Console.WriteLine($"    ({what} -> {error})");
    }

    static void ValidatorTests()
    {
        Console.WriteLine("MemoryLayoutValidator");

        var v1 = ValidInfo();
        Check(MemoryLayoutValidator.Validate(v1, out string err), $"il layout v1 e' valido ({err})");

        // ingombro sul tavolo e orientamento
        var g = new MemoryGridMap(v1.Columns, v1.Rows, v1.TileSize, v1.TileGap);
        g.Center(0, out float lx0, out float lz0);
        MemoryLayoutValidator.ParticipantToTable(v1, lx0, lz0, out float wx0, out float wz0);
        g.Center(g.Index(v1.Columns - 1, 0), out float lx3, out float lz3);
        MemoryLayoutValidator.ParticipantToTable(v1, lx3, lz3, out float wx3, out float _);
        Console.WriteLine($"  tessera 0 sul tavolo: ({wx0:+0.000;-0.000}, {wz0:+0.000;-0.000})");
        Check(wz0 > 0.25f, "la riga 0 e' la piu' vicina al bordo z=+0.4 (centro atteso a z=+0.30)");
        Check(wx3 < wx0, "l'ultima colonna (destra del partecipante) sta a x minore: la sua destra e' -x");

        // layout malformati
        { var l = ValidInfo(); l.Signatures[1] = new SignatureInfo("roccia_dura", 10, 100f, 0.2f, false); Rejected(l, "id di firma duplicato"); }
        { var l = ValidInfo(); l.Signatures[1] = new SignatureInfo("gemella", 10, 100f, 0.9f, false); Rejected(l, "due firme con parametri identici"); }
        { var l = ValidInfo(); l.Signatures[0] = new SignatureInfo("roccia_dura", 10, 0f, 0.9f, false); Rejected(l, "firma senza texture (si confonde con la coperta)"); }
        { var l = ValidInfo(); l.Signatures.Add(new SignatureInfo("settima", 12, 100f, 0.5f, false)); Rejected(l, "sette coppie in dodici tessere"); }
        { var l = ValidInfo(); l.WarmupIds[2] = "inesistente"; Rejected(l, "riscaldamento con una firma inesistente"); }
        { var l = ValidInfo(); l.WarmupTiles.RemoveAt(5); Rejected(l, "riscaldamento con 5 tessere per 3 coppie"); }
        { var l = ValidInfo(); l.WarmupTiles[5] = 1; Rejected(l, "riscaldamento con una tessera ripetuta"); }
        { var l = ValidInfo(); l.CenterZ = 0.10f; Rejected(l, "griglia che sconfina nella meta' lontana"); }
        { var l = ValidInfo(); l.MaxReach = 0.30f; Rejected(l, "griglia oltre l'allungo"); }
        { var l = ValidInfo(); l.YawDegrees = 0f; Rejected(l, "participantYaw col segno sbagliato (riga 0 lontana)"); }
        {
            var l = ValidInfo();
            l.Signatures[4] = new SignatureInfo("metallo_duro", 6, 100f, 0.9f, true);
            Rejected(l, "firma termica con sosta di 1 s");
            l.DwellSeconds = 3f;
            Check(MemoryLayoutValidator.Validate(l, out string e2), $"firma termica con sosta di 3 s: valido ({e2})");
        }
    }
}
