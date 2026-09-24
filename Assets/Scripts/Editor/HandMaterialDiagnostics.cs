using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HapticResearch.EditorTools
{
    // Perche' una mano e' fucsia: diagnostica dei materiali delle mani.
    //
    // Il fucsia acceso non e' mai il colore di un materiale (nessun materiale del SDK
    // WEART e' magenta): e' lo shader di errore di Unity, cioe' "questo materiale non ha
    // uno shader utilizzabile". Le mani sono l'unica cosa in scena che dipende dalla
    // ShaderGraph del SDK ("Shader Graphs/URPShaderHandMaterial"); tutto il resto usa
    // URP/Lit. Per questo si rompono solo loro, e in tutti i livelli: stesso prefab.
    //
    // Il SDK sta in Packages/ ed e' in .gitignore, quindi la cartella e' diversa per
    // macchina: il problema si vede su una postazione e non sull'altra. Questo strumento
    // va lanciato SULLA macchina che mostra il fucsia, perche' li' c'e' la causa.
    //
    // Da menu: "HapticResearch/Strumenti/Diagnostica materiali mani".
    // Headless:
    //   Unity -batchmode -quit -projectPath . \
    //         -executeMethod HapticResearch.EditorTools.HandMaterialDiagnostics.RunHeadless
    // NON salva mai la scena: apre e legge soltanto.
    public static class HandMaterialDiagnostics
    {
        // I rig che ci interessano: mani del guanto, mani demo, rig dei tracker.
        private static readonly string[] RigRoots = { "WEART", "HandManager", "ViveTrackerManager" };

        private static readonly string[] ScenesHeadless =
        {
            "Assets/Scenes/Level1_ShapeRecognition.unity",
            "Assets/Scenes/Labyrinth.unity",
            "Assets/Scenes/Level3_Memory.unity",
        };

        [MenuItem("HapticResearch/Strumenti/Diagnostica materiali mani")]
        public static void RunOnOpenScene()
        {
            var sb = new StringBuilder();
            Intestazione(sb);
            Analizza(SceneManager.GetActiveScene(), sb);
            Debug.Log(sb.ToString());
        }

        public static void RunHeadless()
        {
            var sb = new StringBuilder();
            Intestazione(sb);
            foreach (var path in ScenesHeadless)
                Analizza(EditorSceneManager.OpenScene(path, OpenSceneMode.Single), sb);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }

        // Contesto della macchina: senza questo un report non dice nulla, perche' la
        // stessa scena si comporta diversamente a seconda di dove sta il SDK.
        private static void Intestazione(StringBuilder sb)
        {
            sb.AppendLine("=== Diagnostica materiali mani ===");
            var srp = GraphicsSettings.defaultRenderPipeline;
            sb.AppendLine("Render pipeline attiva: " + (srp != null ? srp.name : "NESSUNA (built-in!)"));
            if (srp == null)
            {
                sb.AppendLine("  -> senza URP assegnata ogni materiale URP diventa fucsia.");
            }

            var shaderGraph = Shader.Find("Shader Graphs/URPShaderHandMaterial");
            if (shaderGraph == null)
            {
                sb.AppendLine("ShaderGraph mani: NON TROVATA ('Shader Graphs/URPShaderHandMaterial').");
                sb.AppendLine("  -> il SDK WEART di questa macchina non la contiene, oppure non e' stata importata.");
            }
            else
            {
                sb.AppendLine("ShaderGraph mani: trovata in " + AssetDatabase.GetAssetPath(shaderGraph));
                sb.AppendLine("  supportata su questa macchina: " + shaderGraph.isSupported);
                Messaggi(shaderGraph, sb, "  ");
            }
        }

        private static void Analizza(Scene scene, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- scena " + scene.name + " ---");

            var problemi = new List<string>();
            int esaminati = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!Interessante(root.name)) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materiali = renderer.sharedMaterials;
                    for (int i = 0; i < materiali.Length; i++)
                    {
                        esaminati++;
                        string motivo = Motivo(materiali[i]);
                        if (motivo == null) continue;

                        bool visibile = renderer.gameObject.activeInHierarchy && renderer.enabled;
                        var riga = new StringBuilder();
                        riga.Append(visibile ? "FUCSIA A VIDEO  " : "fucsia ma nascosto  ");
                        riga.Append(Percorso(renderer.transform));
                        riga.Append("  slot ").Append(i);
                        riga.Append("  materiale '")
                            .Append(materiali[i] == null ? "<vuoto>" : materiali[i].name)
                            .Append("'  -> ").Append(motivo);
                        problemi.Add(riga.ToString());

                        if (materiali[i] != null && materiali[i].shader != null)
                            Messaggi(materiali[i].shader, problemi);
                    }
                }
            }

            sb.AppendLine("materiali esaminati sui rig mani/tracker: " + esaminati);
            if (problemi.Count == 0)
            {
                sb.AppendLine("nessun materiale fucsia: le mani di questa scena sono a posto.");
                return;
            }
            foreach (var p in problemi) sb.AppendLine(p);
        }

        // Il motivo per cui Unity disegnerebbe questo materiale in fucsia, o null se va bene.
        private static string Motivo(Material material)
        {
            if (material == null)
                return "slot materiale VUOTO (nessun materiale assegnato)";

            var shader = material.shader;
            if (shader == null)
                return "lo shader del materiale non si risolve (il file dello shader manca)";

            // Unity sostituisce con questo shader ogni riferimento rotto: e' letteralmente
            // il fucsia. Capita quando il .shadergraph non e' importabile su questa macchina.
            if (shader.name == "Hidden/InternalErrorShader")
                return "shader sostituito da Hidden/InternalErrorShader (import o compilazione falliti)";

            if (!shader.isSupported)
                return "shader '" + shader.name + "' non supportato su questa macchina/piattaforma";

            // Uno shader del built-in pipeline non ha pass che URP sappia disegnare.
            if (shader.name == "Standard" || shader.name == "Standard (Specular setup)"
                || shader.name.StartsWith("Legacy Shaders/"))
                return "shader '" + shader.name + "' e' del pipeline built-in: URP non lo disegna";

            return null;
        }

        private static void Messaggi(Shader shader, List<string> destinazione)
        {
            var sb = new StringBuilder();
            Messaggi(shader, sb, "    ");
            if (sb.Length > 0) destinazione.Add(sb.ToString().TrimEnd());
        }

        private static void Messaggi(Shader shader, StringBuilder sb, string indent)
        {
            if (ShaderUtil.GetShaderMessageCount(shader) == 0) return;
            foreach (var m in ShaderUtil.GetShaderMessages(shader))
                sb.AppendLine(indent + m.severity + " riga " + m.line + ": " + m.message);
        }

        private static bool Interessante(string rootName)
        {
            foreach (var r in RigRoots)
                if (rootName.Contains(r)) return true;
            return false;
        }

        private static string Percorso(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
