using System;
using HapticResearch.Hands;

// Banco di prova della presa col guanto, eseguito FUORI da Unity.
//
// Le forme sono stringhe: alla macchina a stati non interessa cosa siano, solo che
// cambino sotto la mano. Le dita sono due contatori (quante sopra la soglia di chiusura,
// quante sopra quella di apertura), gli stessi due numeri che GloveGraspDetector ricava
// dai thimble veri.
static class Program
{
    private const int MinFingers = 2;

    static int failures = 0;

    static void Check(bool ok, string what)
    {
        if (!ok) { Console.WriteLine("  FALLITO: " + what); failures++; }
    }

    static int Main()
    {
        ManoApertaNonAfferra();
        GuantoAppoggiatoNonArmaMai();
        ApriChiudiAfferra();
        LaPresaSegueLaMano();
        SlotAzzeratoChiudeLaStretta();
        RilasciandoLeDitaSiMolla();

        Console.WriteLine(failures == 0 ? "TUTTO OK" : $"{failures} controlli FALLITI");
        return failures == 0 ? 0 : 1;
    }

    static GraspLatch<string> Nuova() => new GraspLatch<string>(MinFingers);

    // Un passo con la mano APERTA (nessun dito sopra nessuna delle due soglie).
    static string Apri(GraspLatch<string> l, string sotto) => l.Step(0, 0, sotto, true);

    // Un passo con la mano CHIUSA (tutte e cinque le dita sopra entrambe le soglie).
    static string Chiudi(GraspLatch<string> l, string sotto, bool slotIntact = true)
        => l.Step(5, 5, sotto, slotIntact);

    // Mano semiaperta: sopra la soglia bassa di apertura, sotto quella di chiusura.
    // E' il guanto appoggiato al tavolo e dimenticato.
    static string Appoggia(GraspLatch<string> l, string sotto) => l.Step(0, 5, sotto, true);

    static void ManoApertaNonAfferra()
    {
        Console.WriteLine("mano aperta");
        var l = Nuova();
        Check(Apri(l, "cilindro") == null, "la mano aperta sopra una forma non la afferra");
        Check(l.Armed, "ma si arma: la prossima chiusura vale");
        Check(!l.SessionActive, "nessuna stretta in corso");
    }

    static void GuantoAppoggiatoNonArmaMai()
    {
        Console.WriteLine("guanto appoggiato e dimenticato");
        var l = Nuova();
        for (int i = 0; i < 200; i++) Appoggia(l, "cilindro");
        Check(!l.Armed, "restando a meta' strada fra le due soglie non si arma mai");
        Check(Chiudi(l, "cilindro") == null, "e una chiusura senza armo non afferra niente");
    }

    static void ApriChiudiAfferra()
    {
        Console.WriteLine("apri -> chiudi");
        var l = Nuova();
        Apri(l, "cilindro");
        Check(Chiudi(l, "cilindro") == "cilindro", "chiudendo sul cilindro lo si afferra");
        Check(l.SessionActive, "stretta in corso");
        Check(!l.Armed, "l'armo e' stato consumato");
    }

    // IL CASO CHE HA ROTTO IL LIVELLO 1.
    //
    // La mano destra riposa sopra il cilindro a inizio livello. Il partecipante chiude le
    // dita una volta li', poi va a esplorare il resto del tavolo SENZA riaprire la mano e
    // si ferma sul cubo. Se la forma in mano viene decisa solo all'istante della chiusura,
    // il gioco continua a credere che stia tenendo il cilindro: il cubo - e ogni altra
    // forma - risulta sbagliato per sempre.
    static void LaPresaSegueLaMano()
    {
        Console.WriteLine("la presa segue la mano");
        var l = Nuova();
        Apri(l, "cilindro");
        Check(Chiudi(l, "cilindro") == "cilindro", "parte afferrando il cilindro");

        Check(Chiudi(l, null) == null, "attraversando il tavolo nudo non tiene piu' niente");
        Check(l.SessionActive, "ma la stretta resta aperta: non serve riaprire la mano");

        Check(Chiudi(l, "cubo") == "cubo", "arrivata sul cubo, in mano c'e' il cubo");
        for (int i = 0; i < 100; i++) Chiudi(l, "cubo");
        Check(l.Held == "cubo", "e ci resta finche' la mano sta li'");
    }

    // Dopo ogni conferma il manager azzera lo slot apposta: serve un nuovo gesto per
    // riprendere una forma, altrimenti la stessa risposta verrebbe confermata all'infinito.
    static void SlotAzzeratoChiudeLaStretta()
    {
        Console.WriteLine("slot azzerato dal manager");
        var l = Nuova();
        Apri(l, "cubo");
        Chiudi(l, "cubo");

        Check(Chiudi(l, "cubo", slotIntact: false) == null, "lo slot azzerato molla la presa");
        Check(!l.SessionActive && !l.Armed, "e chiude la stretta");

        Check(Chiudi(l, "cubo") == null, "restando chiusa la mano non riafferra da sola");
        Apri(l, "cubo");
        Check(Chiudi(l, "cubo") == "cubo", "ci vuole un vero apri -> chiudi");
    }

    static void RilasciandoLeDitaSiMolla()
    {
        Console.WriteLine("riapertura");
        var l = Nuova();
        Apri(l, "sfera");
        Chiudi(l, "sfera");
        Check(Apri(l, "sfera") == null, "riaprendo la mano la forma si molla");
        Check(!l.SessionActive, "e la stretta si chiude");
        Apri(l, "sfera");
        Check(l.Armed, "al passo dopo la mano e' di nuovo armata");
    }
}
