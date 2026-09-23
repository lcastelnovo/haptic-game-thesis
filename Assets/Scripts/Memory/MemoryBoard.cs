using System;
using System.Collections.Generic;

namespace HapticResearch.Memory
{
    public enum TileState
    {
        Hidden,    // in gioco, non girata
        Flipped,   // girata in questo turno (o in pausa dopo un errore)
        Matched,   // coppia trovata: fuori gioco
        Absent,    // non partecipa a questa fase (riscaldamento)
    }

    // Un errore, con quel che serve in analisi per separare memoria da percezione.
    public readonly struct MismatchInfo
    {
        public readonly int First;
        public readonly int Second;
        // La compagna della prima tessera era gia' stata girata in un turno precedente:
        // il partecipante "poteva saperlo". Falso = ha tirato a caso.
        public readonly bool PartnerSeenBefore;
        // La seconda tessera era gia' stata girata prima: l'ha scelta sapendo com'era.
        public readonly bool SecondSeenBefore;

        public MismatchInfo(int first, int second, bool partnerSeenBefore, bool secondSeenBefore)
        {
            First = first;
            Second = second;
            PartnerSeenBefore = partnerSeenBefore;
            SecondSeenBefore = secondSeenBefore;
        }
    }

    // Una partita di memory: quali firme stanno dove, cosa e' girato, chi ha trovato cosa.
    // Classe pura, provata da Tools/MemoryTest. Il tempo (la pausa dopo un errore) arriva
    // da fuori con Tick.
    //
    // 'open' = riscaldamento: le tessere coperte si sentono comunque con la loro firma. Il
    // gesto e il confronto sono identici; cambia solo quello che il dito sente.
    public sealed class MemoryBoard
    {
        private readonly int[] signature;   // per tessera; -1 = assente
        private readonly TileState[] state;
        private readonly bool[] seen;       // girata in un turno GIA' CHIUSO
        private readonly bool open;
        private readonly float mismatchDelay;

        private int first = -1;
        private int pendingA = -1, pendingB = -1;
        private float coverIn = -1f;

        public MemoryBoard(int tileCount, IReadOnlyList<int> playingTiles, IReadOnlyList<int> pairSignatures,
                           bool open, float mismatchDelay, int seed)
        {
            if (tileCount <= 0) throw new ArgumentException("nessuna tessera");
            if (playingTiles == null || pairSignatures == null || playingTiles.Count != 2 * pairSignatures.Count)
                throw new ArgumentException("servono esattamente due tessere per ogni coppia");

            signature = new int[tileCount];
            state = new TileState[tileCount];
            seen = new bool[tileCount];
            for (int t = 0; t < tileCount; t++) { signature[t] = -1; state[t] = TileState.Absent; }

            var deck = new List<int>(playingTiles.Count);
            foreach (var s in pairSignatures) { deck.Add(s); deck.Add(s); }

            // Fisher-Yates col seed del log: ogni partita si ricostruisce.
            var rng = new Random(seed);
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            for (int k = 0; k < playingTiles.Count; k++)
            {
                int t = playingTiles[k];
                if (t < 0 || t >= tileCount) throw new ArgumentException($"tessera {t} fuori dalla griglia");
                if (state[t] != TileState.Absent) throw new ArgumentException($"tessera {t} ripetuta");
                signature[t] = deck[k];
                state[t] = TileState.Hidden;
            }

            this.open = open;
            this.mismatchDelay = Math.Max(0f, mismatchDelay);
            PairsTotal = pairSignatures.Count;
        }

        public int TileCount => state.Length;
        public bool Open => open;
        public int PairsTotal { get; }
        public int PairsFound { get; private set; }
        public int Attempts { get; private set; }
        public int Turn => Attempts + 1;
        public bool Busy => coverIn >= 0f;
        public bool IsComplete => PairsFound == PairsTotal;
        public int FirstFlipped => first;

        public event Action<int> Flipped;
        public event Action<int, int> Matched;
        public event Action<MismatchInfo> Mismatched;
        public event Action<int, int> Covered;
        public event Action Completed;

        public TileState StateOf(int t) => state[t];
        public int SignatureOf(int t) => signature[t];
        public bool WasSeen(int t) => seen[t];

        public bool FeelsSignature(int t) =>
            state[t] == TileState.Flipped || (open && state[t] == TileState.Hidden);

        public bool CanFlip(int t) =>
            t >= 0 && t < state.Length && state[t] == TileState.Hidden && !Busy && !IsComplete;

        public int PartnerOf(int t)
        {
            if (signature[t] < 0) return -1;
            for (int i = 0; i < signature.Length; i++)
                if (i != t && signature[i] == signature[t]) return i;
            return -1;
        }

        public bool Flip(int t)
        {
            if (!CanFlip(t)) return false;

            state[t] = TileState.Flipped;
            Flipped?.Invoke(t);

            if (first < 0) { first = t; return true; }

            int a = first;
            first = -1;
            Attempts++;

            if (signature[a] == signature[t])
            {
                state[a] = TileState.Matched;
                state[t] = TileState.Matched;
                seen[a] = seen[t] = true;
                PairsFound++;
                Matched?.Invoke(a, t);
                if (IsComplete) Completed?.Invoke();
                return true;
            }

            // Calcolati PRIMA di segnare questo turno come visto: dicono cosa si sapeva
            // quando la scelta e' stata fatta.
            int partner = PartnerOf(a);
            var info = new MismatchInfo(a, t, partner >= 0 && seen[partner], seen[t]);
            seen[a] = seen[t] = true;
            pendingA = a;
            pendingB = t;
            coverIn = mismatchDelay;
            Mismatched?.Invoke(info);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (coverIn < 0f) return;
            coverIn -= deltaTime;
            if (coverIn > 0f) return;

            coverIn = -1f;
            state[pendingA] = TileState.Hidden;
            state[pendingB] = TileState.Hidden;
            Covered?.Invoke(pendingA, pendingB);
        }
    }
}
