using System;

namespace HapticResearch.Memory
{
    // "Il dito e' fermo su questa tessera da N secondi": il gesto con cui si gira una
    // tessera. Classe pura, il tempo arriva da fuori, cosi' si prova in Tools/MemoryTest.
    //
    // Fermo non vuol dire immobile: un tremolio entro 'radius' dal punto in cui il dito si
    // e' posato non azzera niente. Uscire dalla tessera o spostarsi oltre il raggio si'.
    //
    // Il tono (Started) parte solo dopo 'quiet' secondi di quiete: un dito che scorre sulla
    // griglia non deve far suonare niente a ogni tessera che attraversa. Per lo stesso
    // motivo un annullo si segnala solo se la sosta era partita davvero.
    public sealed class DwellDetector
    {
        private readonly float seconds;
        private readonly float radius;
        private readonly float quiet;

        private int target = -1;
        private float anchorX, anchorZ, elapsed;
        private bool started, completed;

        public DwellDetector(float dwellSeconds, float dwellRadius, float quietSeconds)
        {
            seconds = Math.Max(0.01f, dwellSeconds);
            radius = Math.Max(0f, dwellRadius);
            quiet = Math.Max(0f, quietSeconds);
        }

        public int Target => target;
        public bool Running => started && !completed;
        public float Progress01 => Math.Min(1f, elapsed / seconds);

        public event Action<int> Started;
        public event Action<int, float, string> Cancelled;   // tessera, secondi raggiunti, motivo
        public event Action<int> Completed;

        // Una volta per frame. tile < 0: il dito non e' su una tessera che si possa girare.
        public void Update(int tile, float x, float z, float deltaTime)
        {
            if (tile != target)
            {
                Cancel("uscita");
                target = tile;
                if (tile >= 0) Anchor(x, z);
                return;
            }
            if (tile < 0) return;

            float dx = x - anchorX, dz = z - anchorZ;
            if (dx * dx + dz * dz > radius * radius)
            {
                Cancel("raggio");
                Anchor(x, z);
                return;
            }
            if (completed) return;

            elapsed += deltaTime;
            if (!started && (elapsed >= quiet || elapsed >= seconds))
            {
                started = true;
                Started?.Invoke(target);
            }
            if (elapsed >= seconds)
            {
                completed = true;
                Completed?.Invoke(target);
            }
        }

        // Cambio fase, fine livello: la sosta in corso si annulla.
        public void Reset()
        {
            Cancel("reset");
            target = -1;
        }

        private void Anchor(float x, float z)
        {
            anchorX = x;
            anchorZ = z;
            elapsed = 0f;
            started = false;
            completed = false;
        }

        private void Cancel(string reason)
        {
            if (started && !completed) Cancelled?.Invoke(target, elapsed, reason);
            started = false;
            completed = false;
            elapsed = 0f;
        }
    }
}
