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
}
