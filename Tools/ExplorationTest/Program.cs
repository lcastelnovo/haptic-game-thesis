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

        // 6b) passaggio DOPO il minimo: nessuna soppressione, perche' non c'e' niente di
        //     soppresso - la temperatura del secondo oggetto arriva davvero, 0,4 s dopo.
        //     Una riga di log qui sarebbe un falso positivo, e con due soli oggetti termici
        //     questo passaggio capita decine di volte a sessione.
        {
            var g = new ThermalGate(3f, 0.4f);
            var suppressed = new List<string>();
            var armed = new List<string>();
            g.OnSuppressed += (id, reason) => suppressed.Add(id + ":" + reason);
            g.OnArm += (id, role) => armed.Add(id);

            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 3.5f);                       // minimo scaduto col dito ancora sopra
            g.SetTarget("cucchiaino", ThermalRole.Cool);
            Advance(g, 4f);
            Check(suppressed.Count == 0,
                  $"transizione dopo il minimo: nessuna soppressione (sono {suppressed.Count}: {string.Join(", ", suppressed)})");
            Check(armed.Count == 2 && armed[1] == "cucchiaino", "e il cucchiaino si arma regolarmente");
        }

        // 6c) il contrario: prima del minimo la soppressione c'e' e dice il motivo, anche
        //     quando il dito resta sul secondo oggetto fino alla commutazione.
        {
            var g = new ThermalGate(3f, 0.4f);
            var suppressed = new List<string>();
            g.OnSuppressed += (id, reason) => suppressed.Add(id + ":" + reason);

            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 0.5f);                       // minimo NON scaduto
            g.SetTarget("cucchiaino", ThermalRole.Cool);
            Advance(g, 4f);
            Check(suppressed.Count == 1 && suppressed[0] == "cucchiaino:minimo_non_scaduto",
                  $"prima del minimo la soppressione resta, una sola volta (sono {suppressed.Count})");
        }

        // 7) tremolio della mano DOPO erogazione: grazia protegge il canale
        {
            var g = new ThermalGate(3f, 0.4f);
            int arms = 0, releases = 0;
            g.OnArm += (id, role) => arms++;
            g.OnRelease += (id, held, delivered) => releases++;

            // Dito fermo oltre minHold: Delivered diventa true
            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 3.5f);
            Check(g.Delivered, "dopo 3.5 s il dito ha ricevuto la temperatura");

            // Esce per 0.2 s (sotto la grazia di 0.4 s) e rientra
            g.SetTarget(null, ThermalRole.Neutral);
            Advance(g, 0.2f);
            Check(g.Armed, "il canale rimane armato: dentro la grazia");
            g.SetTarget("tazza", ThermalRole.Warm);
            Advance(g, 0.1f);
            Check(releases == 0, "nessun OnRelease: il tremolio della mano non ha spento niente");
            Check(arms == 1, "nessun secondo OnArm: il canale non si è disarmato");

            // Ora esce per 0.5 s (sopra la grazia)
            g.SetTarget(null, ThermalRole.Neutral);
            Advance(g, 0.5f);
            Check(releases == 1, "dopo la scadenza della grazia, OnRelease è stato inviato");
        }

        // 8) Reset riporta a neutro da qualunque stato
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

    static void SuggestionSchedulerTests()
    {
        Console.WriteLine("SuggestionScheduler");

        // 1) dopo lo stallo propone qualcosa
        {
            var s = new SuggestionScheduler(3, 25f, 3, 60f) { TagPicker = () => "caldo" };
            var given = new List<string>();
            s.OnSuggest += t => given.Add(t);

            for (int i = 0; i < 200; i++) s.Tick(0.1f);   // 20 s: ancora niente
            Check(given.Count == 0, "prima dei 25 s di stallo non propone nulla");
            for (int i = 0; i < 100; i++) s.Tick(0.1f);   // 30 s
            Check(given.Count == 1 && given[0] == "caldo", "dopo lo stallo propone una richiesta");
        }

        // 2) ogni tre scoperte propone, senza aspettare lo stallo
        {
            var s = new SuggestionScheduler(3, 25f, 3, 60f) { TagPicker = () => "ruvido" };
            int given = 0;
            s.OnSuggest += t => given++;
            s.NotifyDiscovery(); s.NotifyDiscovery();
            s.Tick(0.1f);
            Check(given == 0, "due scoperte non bastano");
            s.NotifyDiscovery();
            s.Tick(0.1f);
            Check(given == 1, "la terza scoperta fa scattare la richiesta");
        }

        // 3) mai sopra una narrazione in corso
        {
            var s = new SuggestionScheduler(3, 25f, 3, 60f) { TagPicker = () => "caldo" };
            int given = 0;
            s.OnSuggest += t => given++;
            s.NotifyNarration(true);
            for (int i = 0; i < 400; i++) s.Tick(0.1f);
            Check(given == 0, "con la voce che parla non si accavalla");
            s.NotifyNarration(false);
            s.Tick(0.1f);
            Check(given == 1, "appena la voce tace, la richiesta parte");
        }

        // 4) una sola viva per volta
        {
            var s = new SuggestionScheduler(5, 1f, 0, 60f) { TagPicker = () => "caldo" };
            int given = 0;
            s.OnSuggest += t => given++;
            for (int i = 0; i < 300; i++) s.Tick(0.1f);
            Check(given == 1, "finche' una richiesta e' viva non se ne aggiungono altre");
            Check(s.HasActive && s.ActiveTag == "caldo", "la richiesta viva e' leggibile da fuori");
        }

        // 5) scaduto il tempo cade in silenzio (tetto a 1: dopo la caduta non ne parte un'altra,
        //    cosi' 'non resta appesa' verifica la caduta e non il caso di una seconda richiesta)
        {
            var s = new SuggestionScheduler(1, 1f, 0, 60f) { TagPicker = () => "caldo" };
            var dropped = new List<string>();
            s.OnDropped += t => dropped.Add(t);
            for (int i = 0; i < 800; i++) s.Tick(0.1f);   // 80 s
            Check(dropped.Count == 1 && dropped[0] == "caldo", "dopo il timeout la richiesta cade");
            Check(!s.HasActive, "e non resta appesa");
        }

        // 6) soddisfatta quando si tocca il tag giusto
        {
            var s = new SuggestionScheduler(5, 1f, 0, 60f) { TagPicker = () => "caldo" };
            var met = new List<string>();
            s.OnMet += t => met.Add(t);
            for (int i = 0; i < 20; i++) s.Tick(0.1f);
            s.NotifyTouched("stoffa");
            Check(met.Count == 0, "un tag diverso non la soddisfa");
            s.NotifyTouched("caldo");
            Check(met.Count == 1 && !s.HasActive, "il tag giusto la chiude");
        }

        // 7) il tetto massimo non si sfonda
        {
            var s = new SuggestionScheduler(2, 1f, 0, 5f) { TagPicker = () => "caldo" };
            int given = 0;
            s.OnSuggest += t => given++;
            for (int i = 0; i < 1000; i++) s.Tick(0.1f);
            Check(given == 2, $"al massimo due richieste in tutto il livello (sono {given})");
        }

        // 8) se non c'e' niente da proporre, non si propone niente
        {
            var s = new SuggestionScheduler(3, 1f, 0, 60f) { TagPicker = () => null };
            int given = 0;
            s.OnSuggest += t => given++;
            for (int i = 0; i < 300; i++) s.Tick(0.1f);
            Check(given == 0, "TagPicker vuoto: silenzio, non una richiesta senza contenuto");
        }

        // 9) dopo una caduta per timeout, le richieste successive continuano (non si zittisce il sistema)
        {
            var s = new SuggestionScheduler(3, 1f, 0, 5f) { TagPicker = () => "caldo" };
            var given = new List<string>();
            s.OnSuggest += t => given.Add(t);
            var dropped = new List<string>();
            s.OnDropped += t => dropped.Add(t);

            for (int i = 0; i < 1000; i++) s.Tick(0.1f);   // 100 s

            Check(given.Count >= 3, $"dopo timeout, continuano le richieste (sono {given.Count}, min 3)");
            Check(dropped.Count >= 2, $"multiple cadute avvenute (sono {dropped.Count})");
        }
    }

    static SceneObjectInfo Obj(string id, ThermalRole role = ThermalRole.Neutral,
                               bool discoverable = true, params string[] tags)
        => new SceneObjectInfo(id, "level3_obj_" + id, role, tags, discoverable);

    static void ValidatorTests()
    {
        Console.WriteLine("TableSceneValidator");

        var buona = new List<SceneObjectInfo>
        {
            Obj("tovaglietta", ThermalRole.Neutral, false, "stoffa"),
            Obj("tazza", ThermalRole.Warm, true, "caldo", "liscio"),
            Obj("cucchiaino", ThermalRole.Cool, true, "freddo", "metallo"),
            Obj("pane", ThermalRole.Neutral, true, "ruvido"),
        };
        var tagBuoni = new List<string> { "caldo", "ruvido" };

        Check(TableSceneValidator.Validate(buona, tagBuoni, 2, out string err), "la colazione valida passa: " + err);

        // lista vuota
        Check(!TableSceneValidator.Validate(new List<SceneObjectInfo>(), tagBuoni, 2, out _),
              "una scena senza oggetti va rifiutata");

        // id duplicato
        var doppio = new List<SceneObjectInfo>(buona) { Obj("tazza", ThermalRole.Neutral, true, "liscio") };
        Check(!TableSceneValidator.Validate(doppio, tagBuoni, 2, out _), "id duplicato rifiutato");

        // id vuoto
        var vuoto = new List<SceneObjectInfo>(buona) { Obj("", ThermalRole.Neutral, true, "liscio") };
        Check(!TableSceneValidator.Validate(vuoto, tagBuoni, 2, out _), "id vuoto rifiutato");

        // battuta mancante
        var senzaVoce = new List<SceneObjectInfo>(buona)
            { new SceneObjectInfo("burro", "", ThermalRole.Neutral, new[] { "liscio" }, true) };
        Check(!TableSceneValidator.Validate(senzaVoce, tagBuoni, 2, out _), "oggetto senza voiceKey rifiutato");

        // troppi oggetti termici: e' il vincolo fisico del Peltier, non un gusto
        var troppiTermici = new List<SceneObjectInfo>(buona) { Obj("teiera", ThermalRole.Warm, true, "caldo") };
        Check(!TableSceneValidator.Validate(troppiTermici, tagBuoni, 2, out _),
              "piu' di due oggetti termici rifiutati");

        // suggerimento che punta a un tag che nessuno ha
        Check(!TableSceneValidator.Validate(buona, new List<string> { "spugnoso" }, 2, out _),
              "suggerimento su un tag inesistente rifiutato");

        // suggerimento su un tag che ce l'ha solo un oggetto non scopribile
        Check(!TableSceneValidator.Validate(buona, new List<string> { "stoffa" }, 2, out _),
              "suggerimento soddisfacibile solo da uno sfondo rifiutato");

        // nessun oggetto scopribile
        var soloSfondo = new List<SceneObjectInfo> { Obj("tovaglietta", ThermalRole.Neutral, false, "stoffa") };
        Check(!TableSceneValidator.Validate(soloSfondo, new List<string>(), 2, out _),
              "una scena di soli sfondi va rifiutata");
    }

    static int Main()
    {
        ThermalGateTests();
        SuggestionSchedulerTests();
        ValidatorTests();
        Console.WriteLine(failures == 0 ? "\nTUTTI I CONTROLLI PASSATI" : $"\n{failures} CONTROLLI FALLITI");
        return failures == 0 ? 0 : 1;
    }
}
