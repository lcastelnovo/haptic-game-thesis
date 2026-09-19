namespace HapticResearch.Labyrinth
{
    // Come il bivio comunica al partecipante se il ramo sotto il dito e' quello giusto.
    //
    // E' un'interfaccia e non un if dentro il manager perche' le due condizioni
    // sperimentali (temperatura e audio) devono essere INTERCAMBIABILI: se la differenza
    // fra le due fosse sparsa nella logica di gioco, prima o poi divergerebbero in
    // qualcosa che non c'entra col canale sensoriale, e il confronto non varrebbe piu'.
    public interface IJunctionCue
    {
        // Finisce nei log: "thermal" / "audio".
        string CueName { get; }

        bool Armed { get; }
        bool Reading { get; }

        // La sosta e' finita: il valore e' arrivato ed e' leggibile.
        bool ReadingReady { get; }

        // 0..1 durante la sosta, per il tono che sale.
        float Progress01 { get; }

        // Quante letture complete sono state fatte su questo bivio: e' una delle misure
        // dell'esperimento (quante volte ha dovuto controllare).
        int ReadingsCompleted { get; }

        void Arm(int junctionIndex);
        void Disarm();

        void BeginReading(bool onCorrectBranch);
        void CancelReading();

        void Tick(float deltaTime);
    }
}
