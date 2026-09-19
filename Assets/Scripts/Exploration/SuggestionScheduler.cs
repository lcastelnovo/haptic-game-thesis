using System;

namespace HapticResearch.Exploration
{
    // Quando proporre una richiesta facoltativa ("prova a trovare qualcosa di caldo") e
    // quando lasciarla cadere. Niente Unity dentro, per lo stesso motivo del ThermalGate.
    //
    // Il livello e' una sandbox: la richiesta e' un invito, non un compito. Da qui le
    // regole - una sola viva per volta, mai sopra una narrazione, un tetto per livello, e
    // soprattutto se scade il tempo cade in SILENZIO. Nessuna insistenza, nessun
    // fallimento da annunciare: un partecipante che ha ignorato l'invito non ha sbagliato
    // niente.
    public class SuggestionScheduler
    {
        private readonly int maxSuggestions;
        private readonly float idleSeconds;
        private readonly int everyNDiscoveries;
        private readonly float timeoutSeconds;

        private float idleTimer;
        private int discoveriesSinceLast;
        private bool narrationBusy;
        private int given;

        private string activeTag;
        private float activeFor;
        private bool droppedDueToTimeout;

        public SuggestionScheduler(int maxSuggestions, float idleSeconds,
                                   int everyNDiscoveries, float timeoutSeconds)
        {
            this.maxSuggestions = Math.Max(0, maxSuggestions);
            this.idleSeconds = Math.Max(0f, idleSeconds);
            this.everyNDiscoveries = Math.Max(0, everyNDiscoveries);
            this.timeoutSeconds = Math.Max(0f, timeoutSeconds);
        }

        public bool HasActive => activeTag != null;
        public string ActiveTag => activeTag;
        public int Given => given;

        public event Action<string> OnSuggest;
        public event Action<string> OnMet;
        public event Action<string> OnDropped;

        // Chi sceglie il tag: lo scheduler non conosce l'asset della scena. Torna null
        // quando non c'e' piu' niente di sensato da proporre.
        public Func<string> TagPicker;

        public void NotifyNarration(bool busy) => narrationBusy = busy;

        public void NotifyDiscovery()
        {
            discoveriesSinceLast++;
            idleTimer = 0f;
        }

        // Il dito si e' fermato su un oggetto con questi tag.
        public void NotifyTouched(string tag)
        {
            if (activeTag == null || tag != activeTag) return;
            string t = activeTag;
            activeTag = null;
            activeFor = 0f;
            idleTimer = 0f;
            OnMet?.Invoke(t);
        }

        public void Reset()
        {
            idleTimer = 0f;
            discoveriesSinceLast = 0;
            narrationBusy = false;
            given = 0;
            activeTag = null;
            activeFor = 0f;
            droppedDueToTimeout = false;
        }

        public void Tick(float dt)
        {
            if (activeTag != null)
            {
                activeFor += dt;
                if (activeFor < timeoutSeconds) return;
                string t = activeTag;
                activeTag = null;
                activeFor = 0f;
                idleTimer = 0f;
                droppedDueToTimeout = true;
                OnDropped?.Invoke(t);
                return;
            }

            idleTimer += dt;
            if (given >= maxSuggestions || narrationBusy) return;
            if (droppedDueToTimeout && given < maxSuggestions - 1) return;

            bool byIdle = idleTimer >= idleSeconds;
            bool byDiscoveries = everyNDiscoveries > 0 && discoveriesSinceLast >= everyNDiscoveries;
            if (!byIdle && !byDiscoveries) return;

            string tag = TagPicker?.Invoke();
            idleTimer = 0f;
            discoveriesSinceLast = 0;
            if (string.IsNullOrEmpty(tag)) return;

            activeTag = tag;
            activeFor = 0f;
            given++;
            OnSuggest?.Invoke(tag);
        }
    }
}
