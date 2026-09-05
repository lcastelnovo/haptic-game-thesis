using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HapticResearch.Audio
{
    // Tabella chiave -> testo italiano delle battute vocali, letta da
    // Resources/Voice/voice_lines.json. E' lo STESSO file che gli script
    // Tools/generate_voice*.py usano per generare gli mp3: una sola fonte, cosi' i testi
    // letti a schermo combaciano sempre con quelli pronunciati.
    //
    // Serve ai sottotitoli (VoiceSubtitles): quando parte la traccia "find_cubo"
    // l'operatore legge "Trova il cubo.".
    //
    // Il file e' un oggetto JSON piatto { "chiave": "testo", ... }. JsonUtility non legge
    // dizionari e non vogliamo dipendenze esterne per una cosa cosi' piccola, quindi il
    // parser qui sotto gestisce solo quel formato (con gli escape standard delle stringhe).
    public static class VoiceLines
    {
        private const string ResourcePath = "Voice/voice_lines";

        private static Dictionary<string, string> table;

        // Testo della battuta, o null se la chiave non esiste nel file.
        public static string TextOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EnsureLoaded();
            return table.TryGetValue(key, out var text) ? text : null;
        }

        public static bool Has(string key) => TextOf(key) != null;

        public static int Count
        {
            get { EnsureLoaded(); return table.Count; }
        }

        private static void EnsureLoaded()
        {
            if (table != null) return;
            table = new Dictionary<string, string>();

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[VoiceLines] Manca Resources/{ResourcePath}.json: i sottotitoli mostreranno solo la chiave della traccia.");
                return;
            }

            try
            {
                Parse(asset.text, table);
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"[VoiceLines] {ResourcePath}.json non leggibile ({e.Message}): sottotitoli parziali.");
            }
        }

        // Parser minimale: oggetto JSON con valori stringa. Valori di altro tipo (numeri,
        // oggetti annidati...) vengono saltati senza errore.
        internal static void Parse(string json, Dictionary<string, string> into)
        {
            int i = 0;
            SkipWhitespace(json, ref i);
            Expect(json, ref i, '{');

            while (true)
            {
                SkipWhitespace(json, ref i);
                if (i >= json.Length) throw new FormatException("fine del file inattesa");
                if (json[i] == '}') return;
                if (json[i] == ',') { i++; continue; }

                string key = ParseString(json, ref i);
                SkipWhitespace(json, ref i);
                Expect(json, ref i, ':');
                SkipWhitespace(json, ref i);

                if (i < json.Length && json[i] == '"') into[key] = ParseString(json, ref i);
                else SkipValue(json, ref i);
            }
        }

        private static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c)
                throw new FormatException($"atteso '{c}' alla posizione {i}");
            i++;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '\uFEFF')) i++;
        }

        // Salta un valore non-stringa fino alla virgola o alla chiusura dell'oggetto radice.
        private static void SkipValue(string s, ref int i)
        {
            int depth = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { ParseString(s, ref i); continue; }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']')
                {
                    if (depth == 0) return; // chiusura dell'oggetto radice: la gestisce il chiamante
                    depth--;
                }
                else if (c == ',' && depth == 0) return;
                i++;
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"')
                throw new FormatException($"attesa una stringa alla posizione {i}");
            i++;

            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("escape \\u troncato");
                        // TryParse esadecimale puro: un segno o uno spazio nelle 4 cifre e'
                        // un errore di formato, non un'eccezione di altro tipo.
                        if (!int.TryParse(s.Substring(i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                            throw new FormatException($"escape \\u non valido alla posizione {i}");
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("stringa non terminata");
        }
    }
}
