using System;

namespace HapticResearch.Exploration
{
    // Ruolo termico di un oggetto della scena. I VALORI (quanto caldo, quanto freddo)
    // non stanno qui: vengono da HapticProfile, tarato sul singolo partecipante.
    public enum ThermalRole { Neutral, Warm, Cool }

    // Macchina a stati del canale termico. Niente Unity dentro: si prova fuori
    // dall'editor (Tools/ExplorationTest) perche' i suoi errori sono errori di tempo,
    // e il tempo in Play mode non si ispeziona. Un bug qui non si vede: si vede a fine
    // sessione, con un partecipante che "non sentiva il freddo" perche' il freddo non
    // gli e' mai stato mandato.
    //
    // Le regole, tutte dettate dal fatto che il Peltier impiega 2-3 s a raggiungere il
    // set-point:
    //  - un solo oggetto armato per volta;
    //  - si tiene almeno MinHold, anche se il dito se n'e' andato subito;
    //  - all'uscita si aspetta una grazia prima di lasciar andare: un tremolio della
    //    mano non deve spegnere niente;
    //  - finche' il minimo non e' scaduto, un secondo oggetto termico NON commuta.
    //    Tace, e lo segnala: meglio nessuna informazione che un tepore ambiguo, che il
    //    partecipante leggerebbe come un dato quando e' solo un attuatore a meta' strada.
    public class ThermalGate
    {
        private readonly float minHold;
        private readonly float grace;

        private string currentId;            // dove sta il dito in questo frame
        private ThermalRole currentRole;

        private string armedId;
        private ThermalRole armedRole;
        private float held;                  // secondi da quando e' armato
        private float leftFor;               // secondi da quando il dito e' uscito (-1 = e' dentro)
        private bool delivered;
        private string suppressedId;         // per non ripetere la segnalazione a ogni frame

        public ThermalGate(float minHoldSeconds, float releaseGraceSeconds)
        {
            minHold = Math.Max(0f, minHoldSeconds);
            grace = Math.Max(0f, releaseGraceSeconds);
            leftFor = -1f;
            armedRole = ThermalRole.Neutral;
        }

        public string ArmedId => armedId;
        public ThermalRole ArmedRole => armedRole;
        public bool Armed => armedId != null;

        // Vero quando il minimo e' trascorso col dito ancora sopra: solo da qui in poi
        // si puo' affermare che il partecipante ha davvero sentito caldo o freddo.
        public bool Delivered => delivered;
        public float HeldSeconds => held;

        public event Action<string, ThermalRole> OnArm;
        public event Action<string, float, bool> OnRelease;    // id, secondi tenuti, erogata davvero
        public event Action<string, string> OnSuppressed;      // id, motivo
        public event Action<string, ThermalRole> OnDelivered;

        // Da chiamare una volta per frame: l'oggetto sotto il dito, o (null, Neutral).
        public void SetTarget(string id, ThermalRole role)
        {
            currentId = id;
            currentRole = role;
        }

        public void Tick(float deltaTime)
        {
            if (armedId != null)
            {
                held += deltaTime;
                bool stillOn = currentId != null && currentId == armedId;
                leftFor = stillOn ? -1f : (leftFor < 0f ? deltaTime : leftFor + deltaTime);

                // "e' calda" si puo' dire solo col dito ancora sopra: altrimenti
                // annunceremmo una sensazione che nessuno sta provando.
                if (!delivered && stillOn && held >= minHold)
                {
                    delivered = true;
                    OnDelivered?.Invoke(armedId, armedRole);
                }

                // Si segnala SOLO quando il minimo non e' ancora scaduto, cioe' quando il
                // secondo oggetto davvero non avra' la sua temperatura. Se il minimo e'
                // gia' passato non e' una soppressione: e' un normale passaggio, e dopo la
                // grazia il secondo oggetto si arma regolarmente. Con due soli oggetti
                // termici quel passaggio capita decine di volte a sessione, e la riga
                // thermal_suppressed - che in analisi deve distinguere "non ha percepito"
                // da "non gli e' stato mandato" - sarebbe fatta quasi solo di falsi positivi.
                if (!stillOn && held < minHold && currentId != null &&
                    currentRole != ThermalRole.Neutral && currentId != suppressedId)
                {
                    suppressedId = currentId;
                    OnSuppressed?.Invoke(currentId, "minimo_non_scaduto");
                }

                if (!stillOn && leftFor >= grace && held >= minHold) Release();
            }

            if (armedId == null && currentId != null && currentRole != ThermalRole.Neutral)
                Arm(currentId, currentRole);

            // Finita la visita all'oggetto soppresso, il prossimo puo' tornare a segnalare.
            if (currentId != suppressedId) suppressedId = null;
        }

        // Fine livello o cambio scena: qualunque cosa fosse armata torna neutra.
        public void Reset()
        {
            if (armedId != null) Release();
            currentId = null;
            currentRole = ThermalRole.Neutral;
            suppressedId = null;
        }

        private void Arm(string id, ThermalRole role)
        {
            armedId = id;
            armedRole = role;
            held = 0f;
            leftFor = -1f;
            delivered = false;
            OnArm?.Invoke(id, role);
        }

        private void Release()
        {
            string id = armedId;
            float h = held;
            bool d = delivered;
            armedId = null;
            armedRole = ThermalRole.Neutral;
            held = 0f;
            leftFor = -1f;
            delivered = false;
            OnRelease?.Invoke(id, h, d);
        }
    }
}
