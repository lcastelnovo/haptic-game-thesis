using System.Text;
using UnityEngine;

namespace HapticResearch.Diagnostics
{
    // Fotografa, A RUNTIME, il materiale effettivo di ogni mesh delle mani.
    //
    // Serve perche' il fucsia delle mani non si vede in edit mode: compare solo a gioco
    // avviato e con il tracking vivo. In quel momento il materiale non e' piu' quello
    // serializzato nella scena, perche' WeArtDeviceTrackingObject.UpdateHands() riassegna
    // ogni frame lo sharedMaterial della ghost hand. Un materiale null assegnato li'
    // produce esattamente il magenta, e dalla scena chiusa non si puo' vedere.
    //
    // Si auto-installa al Play e non fa nulla finche' non si preme F4. Premere F4 mentre
    // la mano fucsia e' a video: stampa in console e scrive il report in
    // <persistentDataPath>/SondaMani.txt.
    //
    // Strumento di diagnosi temporaneo, come TrackerDebugger: quando il problema e'
    // chiuso si puo' togliere.
    public class HandMaterialProbe : MonoBehaviour
    {
        private const KeyCode Tasto = KeyCode.F4;

        // I rig da ispezionare: mani del guanto, mani demo, rig dei tracker.
        private static readonly string[] RigRoots = { "WEART", "HandManager", "ViveTrackerManager" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Installa()
        {
            var go = new GameObject("HandMaterialProbe");
            go.AddComponent<HandMaterialProbe>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            if (Input.GetKeyDown(Tasto)) Fotografa();
        }

        private void Fotografa()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Sonda materiali mani (runtime) ===");
            sb.AppendLine("scena: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            int visti = 0;
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string percorso = Percorso(renderer.transform);
                if (!Interessante(percorso)) continue;
                if (!renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;
                visti++;

                var materiali = renderer.sharedMaterials;
                for (int i = 0; i < materiali.Length; i++)
                {
                    var m = materiali[i];
                    sb.Append(Giudizio(m)).Append("  ").Append(percorso).Append("  slot ").Append(i);
                    if (m == null)
                    {
                        sb.AppendLine("  -> MATERIALE NULL (Unity lo disegna in magenta)");
                        continue;
                    }
                    sb.Append("  materiale '").Append(m.name).Append("'");
                    sb.Append("  shader '").Append(m.shader == null ? "NULL" : m.shader.name).Append("'");
                    if (m.shader != null) sb.Append(" supportato=").Append(m.shader.isSupported);
                    if (m.HasProperty("_FresnelColor")) sb.Append("  _FresnelColor=").Append(m.GetColor("_FresnelColor"));
                    if (m.HasProperty("_BaseColor")) sb.Append("  _BaseColor=").Append(m.GetColor("_BaseColor"));
                    sb.AppendLine();
                }
            }

            sb.AppendLine("mesh visibili esaminate: " + visti);
            string outPath = System.IO.Path.Combine(Application.persistentDataPath, "SondaMani.txt");
            System.IO.File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            sb.AppendLine("report: " + outPath);
            Debug.Log(sb.ToString());
        }

        // Un materiale null, o uno shader mancante/di errore, e' il magenta di Unity.
        private static string Giudizio(Material m)
        {
            if (m == null) return "MAGENTA";
            if (m.shader == null) return "MAGENTA";
            if (m.shader.name == "Hidden/InternalErrorShader") return "MAGENTA";
            if (!m.shader.isSupported) return "MAGENTA";
            return "  ok   ";
        }

        private static bool Interessante(string percorso)
        {
            foreach (var r in RigRoots)
                if (percorso.Contains(r)) return true;
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
