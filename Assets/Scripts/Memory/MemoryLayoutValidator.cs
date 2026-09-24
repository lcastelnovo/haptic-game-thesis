using System;
using System.Collections.Generic;

namespace HapticResearch.Memory
{
    // Vista "pura" di una firma: niente tipi del SDK WEART (la texture e' il suo intero),
    // cosi' il validatore si prova fuori da Unity.
    public readonly struct SignatureInfo
    {
        public readonly string Id;
        public readonly int Texture;
        public readonly float Volume;
        public readonly float Stiffness;
        public readonly bool Thermal;

        public SignatureInfo(string id, int texture, float volume, float stiffness, bool thermal)
        {
            Id = id;
            Texture = texture;
            Volume = volume;
            Stiffness = stiffness;
            Thermal = thermal;
        }
    }

    // Il layout ridotto a numeri. La riempie MemoryLayoutAsset.ToInfo().
    public sealed class MemoryLayoutInfo
    {
        public int Columns, Rows;
        public float TileSize, TileGap;
        public float CenterX, CenterZ, YawDegrees;
        public float NearEdgeZ, TableHalfX, MaxReach;
        public float DwellSeconds, MinThermalDwellSeconds;
        public List<SignatureInfo> Signatures = new List<SignatureInfo>();
        public List<string> WarmupIds = new List<string>();
        public List<int> WarmupTiles = new List<int>();
    }

    // Controlli sul layout PRIMA di generare la griglia o di avviare il livello. Stessa
    // filosofia di MazeLayoutValidator: un asset incoerente non deve arrivare a una
    // sessione con un partecipante davanti.
    public static class MemoryLayoutValidator
    {
        // Numero massimo di coppie che MemoryManager sa pronunciare (voice_lines.json).
        public const int MaxSpokenPairs = 6;

        // Coordinate del partecipante -> coordinate del tavolo, ruotando attorno a Y come
        // fa Unity con Quaternion.Euler(0, yaw, 0). Scritta a mano perche' Quaternion
        // chiama il runtime nativo, che fuori dall'editor non c'e'.
        public static void ParticipantToTable(MemoryLayoutInfo l, float lx, float lz, out float wx, out float wz)
        {
            double rad = l.YawDegrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            wx = l.CenterX + lx * cos + lz * sin;
            wz = l.CenterZ - lx * sin + lz * cos;
        }

        public static bool Validate(MemoryLayoutInfo l, out string error)
        {
            error = null;
            if (l == null) { error = "layout mancante"; return false; }

            if (l.Columns <= 0 || l.Rows <= 0) { error = "la griglia deve avere almeno una colonna e una riga"; return false; }
            if (l.TileSize <= 0f || l.TileGap < 0f) { error = "misure di tessera non valide"; return false; }
            if (l.DwellSeconds <= 0f) { error = "la sosta deve durare piu' di zero secondi"; return false; }

            // --- firme ---
            if (l.Signatures.Count == 0) { error = "nessuna firma: non ci sarebbero coppie"; return false; }
            var ids = new HashSet<string>();
            bool thermal = false;
            for (int i = 0; i < l.Signatures.Count; i++)
            {
                var s = l.Signatures[i];
                if (string.IsNullOrWhiteSpace(s.Id)) { error = $"la firma in posizione {i + 1} non ha un id"; return false; }
                if (!ids.Add(s.Id)) { error = $"id di firma duplicato: '{s.Id}'"; return false; }
                // La tessera coperta e' senza texture: una firma senza texture le sarebbe
                // indistinguibile, e girarla non cambierebbe niente sotto il dito.
                if (s.Volume <= 0f) { error = $"la firma '{s.Id}' non ha texture: si confonderebbe con la tessera coperta"; return false; }
                for (int j = 0; j < i; j++)
                {
                    var o = l.Signatures[j];
                    if (o.Texture == s.Texture && o.Volume == s.Volume && o.Stiffness == s.Stiffness && o.Thermal == s.Thermal)
                    {
                        error = $"le firme '{o.Id}' e '{s.Id}' hanno gli stessi parametri: sarebbero una coppia sola";
                        return false;
                    }
                }
                thermal |= s.Thermal;
            }

            int tiles = l.Columns * l.Rows;
            if (2 * l.Signatures.Count > tiles)
            {
                error = $"{l.Signatures.Count} coppie non entrano in {tiles} tessere";
                return false;
            }
            // "Quante coppie mancano" ha battute registrate fino a sei (level3m_missing_0..6):
            // con piu' coppie il partecipante sentirebbe un numero sbagliato.
            if (l.Signatures.Count > MaxSpokenPairs)
            {
                error = $"{l.Signatures.Count} coppie, ma le battute 'mancano N coppie' arrivano a {MaxSpokenPairs}: " +
                        "aggiungi le voci level3m_missing_* prima di allargare il layout";
                return false;
            }

            // --- riscaldamento ---
            if (l.WarmupIds.Count == 0) { error = "il riscaldamento non ha firme"; return false; }
            var warmup = new HashSet<string>();
            foreach (var id in l.WarmupIds)
            {
                if (!ids.Contains(id)) { error = $"il riscaldamento usa la firma '{id}', che non esiste"; return false; }
                if (!warmup.Add(id)) { error = $"il riscaldamento usa due volte la firma '{id}'"; return false; }
            }
            if (l.WarmupTiles.Count != 2 * l.WarmupIds.Count)
            {
                error = $"il riscaldamento ha {l.WarmupIds.Count} coppie ma {l.WarmupTiles.Count} tessere (ne servono {2 * l.WarmupIds.Count})";
                return false;
            }
            var warmupTiles = new HashSet<int>();
            foreach (var t in l.WarmupTiles)
            {
                if (t < 0 || t >= tiles) { error = $"il riscaldamento usa la tessera {t}, fuori dalla griglia"; return false; }
                if (!warmupTiles.Add(t)) { error = $"il riscaldamento usa due volte la tessera {t}"; return false; }
            }

            // --- posizione sul tavolo ---
            var grid = new MemoryGridMap(l.Columns, l.Rows, l.TileSize, l.TileGap);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var lx in new[] { -grid.Width * 0.5f, grid.Width * 0.5f })
                foreach (var lz in new[] { -grid.Depth * 0.5f, grid.Depth * 0.5f })
                {
                    ParticipantToTable(l, lx, lz, out float wx, out float wz);
                    minX = Math.Min(minX, wx); maxX = Math.Max(maxX, wx);
                    minZ = Math.Min(minZ, wz); maxZ = Math.Max(maxZ, wz);
                }
            if (minX <= -l.TableHalfX || maxX >= l.TableHalfX) { error = "la griglia esce dal tavolo in x"; return false; }
            if (maxZ >= l.NearEdgeZ) { error = "la griglia sborda dal bordo vicino al partecipante"; return false; }
            if (minZ <= 0f) { error = "la griglia sconfina nella meta' del tavolo lontana dal partecipante"; return false; }
            if (l.NearEdgeZ - minZ > l.MaxReach)
            {
                error = $"la tessera piu' lontana e' a {(l.NearEdgeZ - minZ) * 100:0.#} cm dal bordo, oltre l'allungo di {l.MaxReach * 100:0.#} cm";
                return false;
            }
            if (l.Rows > 1)
            {
                // Sbagliare il segno di participantYaw non da' errori: gira solo la griglia,
                // e la riga 0 finisce dalla parte lontana. Qui lo si nota.
                grid.Center(grid.Index(0, 0), out float fx, out float fz);
                grid.Center(grid.Index(0, l.Rows - 1), out float bx, out float bz);
                ParticipantToTable(l, fx, fz, out _, out float firstZ);
                ParticipantToTable(l, bx, bz, out _, out float lastZ);
                if (firstZ <= lastZ) { error = "la riga 0 e' la piu' lontana dal partecipante: participantYaw ha il segno sbagliato"; return false; }
            }

            // --- canale termico ---
            // Il Peltier impiega 2-3 s: una sosta piu' corta girerebbe la tessera prima che
            // la temperatura arrivi, e il partecipante la leggerebbe come "neutra".
            if (thermal && l.DwellSeconds < l.MinThermalDwellSeconds)
            {
                error = $"ci sono firme termiche ma la sosta dura {l.DwellSeconds:0.0} s, meno del minimo di {l.MinThermalDwellSeconds:0.0} s";
                return false;
            }

            return true;
        }
    }
}
