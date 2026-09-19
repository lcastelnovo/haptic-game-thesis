using System;
using System.Collections.Generic;
using HapticResearch.Exploration;

// Banco di prova della logica a tempo del Level 3, eseguito FUORI da Unity.
static class Program
{
    static int failures = 0;
    static void Check(bool ok, string what)
    {
        if (!ok) { Console.WriteLine("  FALLITO: " + what); failures++; }
    }

    // Avanza il gate a passi di 100 ms: un frame finto, ma regolare.
    static void Advance(ThermalGate gate, float seconds)
    {
        int steps = (int)Math.Round(seconds / 0.1f);
        for (int i = 0; i < steps; i++) gate.Tick(0.1f);
    }

    static void ThermalGateTests()
    {
        Console.WriteLine("ThermalGate");

        // 1) arma all'ingresso su un oggetto termico, non su uno neutro
        {
            var g = new ThermalGate(3f, 0.4f);
            var armed = new List<string>();
            g.OnArm += (id, role) => armed.Add(id);

            g.SetTarget("tovaglietta", ThermalRole.Neutral);
            g.Tick(0.1f);
            Check(!g.Armed, "un oggetto neutro non arma niente");

            g.SetTarget("tazza", ThermalRole.Warm);
            g.Tick(0.1f);
            Check(g.Armed && g.ArmedId == "tazza", "la tazza arma il canale");
            Check(armed.Count == 1, "un solo evento di armamento");
        }

        // 2) il dito esce e rientra entro la grazia: non rilascia, non ri-arma
        {
            var g = new ThermalGate(3f, 0.4f);
            int arms = 0, releases = 0;
            g.OnArm += (id, role) => arms++;
            g.OnRelease += (id, held, delivered) => releases++;

            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 1.0f);
            g.SetTarget(null, ThermalRole.Neutral);
            Advance(g, 0.2f);                      // sotto la grazia
            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 0.5f);
            Check(arms == 1 && releases == 0, "un tremolio della mano non spegne niente");
        }

        // 3) tiene fino al minimo anche se il dito e' uscito subito
        {
            var g = new ThermalGate(3f, 0.4f);
            float releasedAt = -1f, clock = 0f;
            g.OnRelease += (id, held, delivered) => releasedAt = clock;

            g.SetTarget("tazza", ThermalRole.Warm);
            g.Tick(0.1f); clock += 0.1f;
            g.SetTarget(null, ThermalRole.Neutral);
            for (int i = 0; i < 40; i++) { g.Tick(0.1f); clock += 0.1f; }
            Check(releasedAt >= 2.9f, $"rilascio non prima del minimo di tenuta (rilasciato a {releasedAt:0.00} s)");
        }

        // 4) 'erogata' solo se il dito era ancora li' allo scadere del minimo
        {
            var g = new ThermalGate(3f, 0.4f);
            bool delivered = false;
            g.OnDelivered += (id, role) => delivered = true;

            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 3.5f);
            Check(delivered, "dito fermo per tre secondi: la temperatura e' arrivata");

            var g2 = new ThermalGate(3f, 0.4f);
            bool delivered2 = false;
            bool releasedDelivered = true;
            g2.OnDelivered += (id, role) => delivered2 = true;
            g2.OnRelease += (id, held, d) => releasedDelivered = d;
            g2.SetTarget("tazza", ThermalRole.Warm);
            Advance(g2, 0.5f);
            g2.SetTarget(null, ThermalRole.Neutral);
            Advance(g2, 4f);
            Check(!delivered2, "dito andato via prima del minimo: niente 'e' calda'");
            Check(!releasedDelivered, "il rilascio riporta erogata=false");
        }

        // 5) il secondo oggetto termico tace, e lo dice una volta sola
        {
            var g = new ThermalGate(3f, 0.4f);
            var suppressed = new List<string>();
            g.OnSuppressed += (id, reason) => suppressed.Add(id + ":" + reason);

            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 0.5f);
            g.SetTarget("cucchiaino", ThermalRole.Cool);
            Advance(g, 0.5f);
            Check(g.ArmedId == "tazza", "il cucchiaino non ruba il canale alla tazza");
            Check(suppressed.Count == 1, $"un solo evento di soppressione, non uno per frame (sono {suppressed.Count})");
            Check(suppressed[0] == "cucchiaino:minimo_non_scaduto", "la soppressione dice il motivo");
        }

        // 6) scaduti minimo e grazia, commuta sul secondo
        {
            var g = new ThermalGate(3f, 0.4f);
            var armed = new List<string>();
            g.OnArm += (id, role) => armed.Add(id);

            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 1f);
            g.SetTarget("cucchiaino", ThermalRole.Cool);
            Advance(g, 4f);
            Check(armed.Count == 2 && armed[1] == "cucchiaino", "passato il minimo, il cucchiaino prende il canale");
            Check(g.ArmedRole == ThermalRole.Cool, "e lo prende da freddo");
        }

        // 7) Reset riporta a neutro da qualunque stato
        {
            var g = new ThermalGate(3f, 0.4f);
            int releases = 0;
            g.OnRelease += (id, held, delivered) => releases++;
            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 1f);
            g.Reset();
            Check(!g.Armed && releases == 1, "Reset rilascia e lascia il canale neutro");
            g.Tick(0.1f);
            Check(!g.Armed, "dopo il Reset non si ri-arma da solo");
        }
    }

    static int Main()
    {
        ThermalGateTests();
        Console.WriteLine(failures == 0 ? "\nTUTTI I CONTROLLI PASSATI" : $"\n{failures} CONTROLLI FALLITI");
        return failures == 0 ? 0 : 1;
    }
}
