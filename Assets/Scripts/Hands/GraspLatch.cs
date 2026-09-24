namespace HapticResearch.Hands
{
    // Macchina a stati della presa col guanto, tenuta FUORI da Unity per poterla provare
    // senza hardware (Tools/GraspTest).
    //
    // Il TouchDIVER non ha un bottone di presa: l'unica cosa che si legge e' quanto sono
    // chiuse le dita. Da li' si ricava la presa con due cancelli, in quest'ordine:
    //
    //   1. ARMO - la mano deve prima APRIRSI davvero: meno di minFingersClosed dita sopra
    //      openThreshold. Un guanto appoggiato al tavolo e dimenticato oscilla di poco
    //      attorno alla soglia di chiusura e non scende mai fin qui, quindi non arma mai:
    //      e' il cancello anti presa-fantasma.
    //   2. STRETTA - da mano armata, minFingersClosed dita sopra closureThreshold aprono
    //      una "stretta", che dura finche' le dita non si riaprono.
    //
    // Durante la stretta la forma in mano viene RICALCOLATA a ogni passo, da chi chiama,
    // e passata in `nearest`. E' il punto di questa classe: la forma non viene decisa una
    // volta sola all'istante della chiusura. Se lo fosse, basterebbe che la mano si
    // chiudesse una volta vicino a una forma - per esempio nel punto in cui riposa a
    // inizio livello - perche' il gioco continuasse a credere di avere QUELLA in mano
    // mentre il partecipante esplora il resto del tavolo: ogni risposta risulterebbe
    // sbagliata tranne quella forma li'.
    //
    // La stretta resta aperta anche quando non c'e' nessuna forma sotto la mano: il
    // partecipante puo' passare da una forma all'altra senza riaprire la mano a ogni
    // spostamento. Quello che NON puo' fare e' iniziare una stretta senza un vero
    // apri -> chiudi, cosi' il cancello anti-fantasma resta intero.
    public class GraspLatch<T> where T : class
    {
        private readonly int minFingersClosed;

        private bool armed;    // la mano si e' aperta davvero: la prossima chiusura vale
        private bool session;  // stretta in corso
        private T held;        // forma attualmente in mano (null anche a stretta aperta)

        public GraspLatch(int minFingersClosed)
        {
            this.minFingersClosed = minFingersClosed < 1 ? 1 : minFingersClosed;
        }

        public bool Armed => armed;
        public bool SessionActive => session;
        public T Held => held;

        // Un passo di simulazione.
        //   closedCount  dita sopra la soglia di chiusura
        //   openCount    dita sopra la soglia (piu' bassa) di apertura
        //   nearest      forma sotto la mano in QUESTO passo (null se nessuna)
        //   slotIntact   lo slot esterno contiene ancora cio' che ci avevamo messo noi.
        //                Falso quando qualcun altro lo ha azzerato: il manager lo fa dopo
        //                ogni conferma, apposta perche' serva un nuovo gesto per riprendere.
        // Ritorna la forma che deve risultare in mano dopo il passo.
        public T Step(int closedCount, int openCount, T nearest, bool slotIntact)
        {
            bool closedEnough = closedCount >= minFingersClosed;
            bool openEnough = openCount < minFingersClosed;

            if (!session)
            {
                if (openEnough) armed = true;
                if (armed && closedEnough)
                {
                    armed = false; // stretta consumata: per riprovare bisogna riaprire
                    session = true;
                    held = nearest;
                }
                return held;
            }

            if (!closedEnough || !slotIntact)
            {
                Reset();
                return held;
            }

            held = nearest;
            return held;
        }

        // Dimentica tutto: serve un nuovo apri -> chiudi. La usa anche chi chiama quando
        // il device sparisce, cosi' al suo ritorno la presa non riparte da sola.
        public void Reset()
        {
            armed = false;
            session = false;
            held = null;
        }
    }
}
