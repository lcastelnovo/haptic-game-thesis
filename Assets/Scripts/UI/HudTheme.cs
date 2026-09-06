using System.Collections.Generic;
using UnityEngine;

namespace HapticResearch.UI
{
    // Tema grafico condiviso dell'interfaccia operatore (sidebar, pill, barra sottotitoli):
    // palette scura con accenti blu UniBS, font serif per i titoli e monospazio per le
    // etichette tecniche. I font sono quelli di sistema (Georgia/Consolas su Windows,
    // Georgia/Menlo su Mac): niente asset da aggiungere, con ripiego al font di Unity.
    public static class HudTheme
    {
        // Palette (dal mockup operatore).
        public static readonly Color Bg = Hex("#0B1220");          // sfondo sidebar
        public static readonly Color Panel = Hex("#101B2E");       // riquadri interni
        public static readonly Color Border = Hex("#1F2B40");      // righe divisorie
        public static readonly Color Accent = Hex("#1E63C4");      // blu bottone / badge DICO
        public static readonly Color AccentLight = Hex("#4BA3E3"); // etichette azzurre
        public static readonly Color Text = Color.white;
        public static readonly Color Muted = Hex("#8A94A6");
        public static readonly Color Ok = Hex("#4CD27A");
        public static readonly Color Warn = Hex("#E7B84A");
        public static readonly Color Grey = Hex("#7F8A9A");
        public static readonly Color BadgeHeard = Hex("#3A4A66");
        public static readonly Color BadgeHeardText = Hex("#CFE3FF");

        private static readonly Dictionary<Color, Texture2D> solids = new Dictionary<Color, Texture2D>();
        private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        private static HashSet<string> installedFonts; // enumerati una volta sola

        private static readonly string[] SerifNames = { "Georgia", "Times New Roman", "Times" };
        private static readonly string[] MonoNames = { "Consolas", "Menlo", "Courier New", "Courier" };
        private static readonly string[] SansNames = { "Segoe UI", "Helvetica Neue", "Helvetica", "Arial" };

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        // Texture 1x1 di un colore, cachata: per sfondi e riquadri IMGUI.
        public static Texture2D Solid(Color c)
        {
            if (solids.TryGetValue(c, out var t) && t != null) return t;
            t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            solids[c] = t;
            return t;
        }

        public static Font Serif(int size) => OsFont("serif", SerifNames, size);
        public static Font Mono(int size) => OsFont("mono", MonoNames, size);
        public static Font Sans(int size) => OsFont("sans", SansNames, size);

        // Font di sistema (dinamico); se nessuno dei nomi esiste, null -> IMGUI usa il default.
        private static Font OsFont(string key, string[] names, int size)
        {
            string k = key + size;
            if (fonts.TryGetValue(k, out var f)) return f;
            f = null;
            try
            {
                installedFonts ??= new HashSet<string>(Font.GetOSInstalledFontNames());
                var available = installedFonts;
                foreach (var n in names)
                {
                    if (!available.Contains(n)) continue;
                    f = Font.CreateDynamicFontFromOSFont(n, size);
                    if (f != null) { f.hideFlags = HideFlags.HideAndDontSave; break; }
                }
            }
            catch { f = null; }
            fonts[k] = f;
            return f;
        }

        // Stile etichetta pronto: font opzionale, colore, dimensione, allineamento.
        public static GUIStyle Label(Font font, int size, Color color, FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = false)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = style,
                alignment = anchor,
                wordWrap = wrap,
                richText = true,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            if (font != null) s.font = font;
            s.normal.textColor = color;
            s.hover.textColor = color;
            return s;
        }

        public static string Rich(Color c, string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";
    }
}
