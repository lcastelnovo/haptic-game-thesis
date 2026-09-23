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
}
