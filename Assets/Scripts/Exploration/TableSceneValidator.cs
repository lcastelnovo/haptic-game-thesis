using System.Collections.Generic;

namespace HapticResearch.Exploration
{
    // Vista "pura" di un oggetto della scena: quel tanto che serve per validare, senza
    // tipi Unity ne' del SDK WEART. E' cio' che permette di provare il validatore fuori
    // dall'editor (e senza il pacchetto WEART, che e' in .gitignore).
    public readonly struct SceneObjectInfo
    {
        public readonly string Id;
        public readonly string VoiceKey;
        public readonly ThermalRole Role;
        public readonly string[] Tags;
        public readonly bool Discoverable;

        public SceneObjectInfo(string id, string voiceKey, ThermalRole role, string[] tags, bool discoverable)
        {
            Id = id;
            VoiceKey = voiceKey;
            Role = role;
            Tags = tags ?? new string[0];
            Discoverable = discoverable;
        }
    }

    // Controlli di coerenza sul dato della scena, PRIMA che il livello parta. Stessa
    // filosofia di MazeLayoutValidator: un asset incoerente non deve arrivare a una
    // sessione con un partecipante davanti, dove costa mezz'ora di lavoro a tutti.
    public static class TableSceneValidator
    {
        public static bool Validate(IReadOnlyList<SceneObjectInfo> objects,
                                    IReadOnlyList<string> suggestionTags,
                                    int maxThermal,
                                    out string error)
        {
            error = null;

            if (objects == null || objects.Count == 0)
            {
                error = "la scena non ha oggetti";
                return false;
            }

            var ids = new HashSet<string>();
            var discoverableTags = new HashSet<string>();
            int thermal = 0;
            int discoverable = 0;

            foreach (var o in objects)
            {
                if (string.IsNullOrWhiteSpace(o.Id))
                {
                    error = "c'e' un oggetto senza id";
                    return false;
                }
                if (!ids.Add(o.Id))
                {
                    error = $"id duplicato: '{o.Id}'";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(o.VoiceKey))
                {
                    error = $"l'oggetto '{o.Id}' non ha una chiave vocale: resterebbe senza nome";
                    return false;
                }
                if (o.Role != ThermalRole.Neutral) thermal++;
                if (!o.Discoverable) continue;

                discoverable++;
                foreach (var t in o.Tags)
                    if (!string.IsNullOrWhiteSpace(t)) discoverableTags.Add(t);
            }

            if (discoverable == 0)
            {
                error = "nessun oggetto scopribile: il livello non avrebbe contenuto";
                return false;
            }

            // Il tetto non e' un gusto: e' il tempo di salita del Peltier. Con tre o piu'
            // oggetti termici vicini, il canale passa la sessione a rincorrere il dito.
            if (thermal > maxThermal)
            {
                error = $"{thermal} oggetti termici, il massimo e' {maxThermal}: " +
                        "l'attuatore non fa in tempo a raggiungerli tutti";
                return false;
            }

            if (suggestionTags == null) return true;

            foreach (var tag in suggestionTags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    error = "c'e' un suggerimento senza tag";
                    return false;
                }
                if (!discoverableTags.Contains(tag))
                {
                    error = $"il suggerimento '{tag}' non e' soddisfacibile: " +
                            "nessun oggetto scopribile ha quel tag";
                    return false;
                }
            }

            return true;
        }
    }
}
