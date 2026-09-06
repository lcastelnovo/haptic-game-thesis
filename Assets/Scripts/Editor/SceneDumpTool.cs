using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HapticResearch.EditorTools
{
    // Dump testuale di una scena: gerarchia, transform, componenti e campi serializzati.
    //
    // Serve per capire com'e' fatta una scena senza aprire l'editor (o per confrontarne
    // due): utile quando si porta la configurazione di un livello su un altro.
    //
    // Da menu: "HapticResearch/Strumenti/Dump scena aperta" (scrive accanto al progetto).
    // Headless:
    //   Unity -batchmode -quit -projectPath . -executeMethod HapticResearch.EditorTools.SceneDumpTool.DumpHeadless
    //         -dumpScenes Assets/Scenes/A.unity,Assets/Scenes/B.unity -dumpOut /cartella/output
    // NON salva mai la scena: apre e legge soltanto.
    public static class SceneDumpTool
    {
        [MenuItem("HapticResearch/Strumenti/Dump scena aperta")]
        public static void DumpOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            string outPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, scene.name + ".dump.txt");
            File.WriteAllText(outPath, Dump(scene), Encoding.UTF8);
            Debug.Log($"[SceneDump] Scritto {outPath}");
        }

        public static void DumpHeadless()
        {
            string scenes = Arg("-dumpScenes");
            string outDir = Arg("-dumpOut");
            if (string.IsNullOrEmpty(scenes) || string.IsNullOrEmpty(outDir))
            {
                Debug.LogError("[SceneDump] Servono -dumpScenes a.unity,b.unity e -dumpOut cartella");
                EditorApplication.Exit(2);
                return;
            }
            Directory.CreateDirectory(outDir);
            foreach (var path in scenes.Split(','))
            {
                var p = path.Trim();
                if (p.Length == 0) continue;
                var scene = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                string outPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(p) + ".dump.txt");
                File.WriteAllText(outPath, Dump(scene), Encoding.UTF8);
                Debug.Log($"[SceneDump] {p} -> {outPath}");
            }
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        public static string Dump(Scene scene)
        {
            var sb = new StringBuilder(1 << 16);
            sb.AppendLine($"# Scena: {scene.path}");
            foreach (var root in scene.GetRootGameObjects())
                DumpGameObject(root, 0, sb);
            return sb.ToString();
        }

        private static void DumpGameObject(GameObject go, int depth, StringBuilder sb)
        {
            string ind = new string(' ', depth * 2);
            var t = go.transform;
            string prefab = PrefabUtility.IsPartOfPrefabInstance(go)
                ? $" [prefab: {AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go))}]"
                : "";
            sb.AppendLine($"{ind}- {go.name}{(go.activeSelf ? "" : " (INATTIVO)")} layer={LayerMask.LayerToName(go.layer)} tag={go.tag}{prefab}");
            sb.AppendLine($"{ind}    pos={V(t.localPosition)} rot={V(t.localEulerAngles)} scale={V(t.localScale)} world={V(t.position)}");

            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) { sb.AppendLine($"{ind}    * (script mancante)"); continue; }
                if (c is Transform) continue;
                sb.Append($"{ind}    * {c.GetType().Name}");
                if (c is Behaviour b && !b.enabled) sb.Append(" (disabilitato)");
                sb.AppendLine();
                DumpFields(c, ind + "        ", sb);
            }

            for (int i = 0; i < t.childCount; i++)
                DumpGameObject(t.GetChild(i).gameObject, depth + 1, sb);
        }

        // Campi serializzati visibili (stessi dell'Inspector), un livello di annidamento.
        private static void DumpFields(Component c, string ind, StringBuilder sb)
        {
            if (c is MeshRenderer mr)
            {
                foreach (var m in mr.sharedMaterials)
                    sb.AppendLine($"{ind}material={(m != null ? m.name : "null")}");
                return;
            }
            if (c is MeshFilter mf) { sb.AppendLine($"{ind}mesh={(mf.sharedMesh != null ? mf.sharedMesh.name : "null")}"); return; }

            var so = new SerializedObject(c);
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script" || it.name == "m_ObjectHideFlags") continue;
                if (it.depth > 1) continue;
                string val = Value(it);
                if (val != null) sb.AppendLine($"{ind}{it.propertyPath}={val}");
                else if (it.isArray && it.propertyType != SerializedPropertyType.String)
                    sb.AppendLine($"{ind}{it.propertyPath}=[{it.arraySize}]{ArrayPreview(it)}");
                enter = it.propertyType == SerializedPropertyType.Generic && !it.isArray;
            }
        }

        private static string ArrayPreview(SerializedProperty arr)
        {
            var sb = new StringBuilder();
            int n = Mathf.Min(arr.arraySize, 12);
            for (int i = 0; i < n; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                string v = Value(e);
                if (v == null && e.propertyType == SerializedPropertyType.Generic)
                {
                    // elemento struct/classe: prova i primi campi semplici
                    var inner = new StringBuilder("{");
                    var ch = e.Copy();
                    var end = e.GetEndProperty();
                    bool first = true;
                    if (ch.NextVisible(true))
                        do
                        {
                            if (SerializedProperty.EqualContents(ch, end)) break;
                            if (ch.depth != e.depth + 1) continue;
                            string cv = Value(ch);
                            if (cv == null) continue;
                            inner.Append(first ? "" : ", ").Append(ch.name).Append('=').Append(cv);
                            first = false;
                        } while (ch.NextVisible(false));
                    v = inner.Append('}').ToString();
                }
                sb.Append(i == 0 ? " " : ", ").Append(v ?? "?");
            }
            if (arr.arraySize > n) sb.Append(", ...");
            return sb.ToString();
        }

        private static string Value(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.longValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue ? "true" : "false";
                case SerializedPropertyType.Float: return p.doubleValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return $"\"{p.stringValue}\"";
                case SerializedPropertyType.Enum: return p.enumDisplayNames.Length > p.enumValueIndex && p.enumValueIndex >= 0 ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString();
                case SerializedPropertyType.Vector3: return V(p.vector3Value);
                case SerializedPropertyType.Vector2: return $"({p.vector2Value.x:0.###}, {p.vector2Value.y:0.###})";
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                case SerializedPropertyType.LayerMask: return p.intValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    if (p.objectReferenceValue == null) return "null";
                    var o = p.objectReferenceValue;
                    string path = AssetDatabase.GetAssetPath(o);
                    if (o is Component oc) return $"-> {FullPath(oc.gameObject)} ({o.GetType().Name})";
                    if (o is GameObject og) return $"-> {FullPath(og)}";
                    return $"{o.name} ({o.GetType().Name}){(string.IsNullOrEmpty(path) ? "" : " @" + path)}";
                default: return null;
            }
        }

        private static string FullPath(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null) { sb.Insert(0, t.name + "/"); t = t.parent; }
            return sb.ToString();
        }

        private static string V(Vector3 v) => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";
    }
}
