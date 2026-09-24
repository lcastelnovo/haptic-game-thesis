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

        // Il verdetto sulla ShaderGraph delle mani, gia' pronto da stampare.
        // null = tutto a posto.
        private static string causaShaderGraph;

        [MenuItem("HapticResearch/Strumenti/Diagnostica materiali mani")]
        public static void RunOnOpenScene()
        {
            var sb = new StringBuilder();
            Intestazione(sb);
            Analizza(SceneManager.GetActiveScene(), sb);
            Consegna(sb);
        }

        public static void RunHeadless()
        {
            var sb = new StringBuilder();
            Intestazione(sb);
            foreach (var path in ScenesHeadless)
                Analizza(EditorSceneManager.OpenScene(path, OpenSceneMode.Single), sb);
            Consegna(sb);
            EditorApplication.Exit(0);
        }

        // Il report intero non ci sta nella console di Unity, che di un messaggio lungo
        // mostra solo le prime righe: finisce su file, e in console restano il verdetto e
        // il percorso. Il verdetto e' un LogError apposta, cosi' non si perde nella lista.
        private static void Consegna(StringBuilder sb)
        {
            string outPath = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "DiagnosticaMani.txt");
            System.IO.File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);

            if (causaShaderGraph != null)
                Debug.LogError("[DiagnosticaMani] CAUSA DEL FUCSIA: " + causaShaderGraph
                    + "  --  report completo in " + outPath);
            else
                Debug.Log("[DiagnosticaMani] La ShaderGraph delle mani e' a posto su questa"
                    + " macchina: il fucsia non viene da li'. Report completo in " + outPath);
        }

        // Contesto della macchina: senza questo un report non dice nulla, perche' la
        // stessa scena si comporta diversamente a seconda di dove sta il SDK.
        private static void Intestazione(StringBuilder sb)
        {
            causaShaderGraph = null;

            sb.AppendLine("=== Diagnostica materiali mani ===");
            var srp = GraphicsSettings.defaultRenderPipeline;
            sb.AppendLine("Render pipeline attiva: " + (srp != null ? srp.name : "NESSUNA (built-in!)"));
            if (srp == null)
            {
                sb.AppendLine("  -> senza URP assegnata ogni materiale URP diventa fucsia.");
                causaShaderGraph = "nessuna render pipeline URP assegnata (sarebbe fucsia tutta la scena).";
            }

            var shaderGraph = Shader.Find("Shader Graphs/URPShaderHandMaterial");
            if (shaderGraph == null)
            {
                sb.AppendLine("ShaderGraph mani: NON TROVATA ('Shader Graphs/URPShaderHandMaterial').");
                sb.AppendLine("  -> il SDK WEART di questa macchina non la contiene, oppure non e' stata importata.");
                causaShaderGraph ??= "la ShaderGraph 'Shader Graphs/URPShaderHandMaterial' non esiste su"
                    + " questa macchina: il SDK WEART locale non la contiene o non e' stata importata.";
                return;
            }

            string percorso = AssetDatabase.GetAssetPath(shaderGraph);
            sb.AppendLine("ShaderGraph mani: trovata in " + percorso);
            sb.AppendLine("  supportata su questa macchina: " + shaderGraph.isSupported);
            Messaggi(shaderGraph, sb, "  ");

            if (!shaderGraph.isSupported)
                causaShaderGraph ??= "la ShaderGraph delle mani (" + percorso + ") esiste ma NON e'"
                    + " supportata: import o compilazione falliti su questa macchina.";
            else if (ShaderUtil.GetShaderMessageCount(shaderGraph) > 0)
                causaShaderGraph ??= "la ShaderGraph delle mani (" + percorso + ") compila con "
                    + ShaderUtil.GetShaderMessageCount(shaderGraph) + " messaggi: vedi il report.";
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
