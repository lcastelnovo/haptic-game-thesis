using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using HapticResearch.Labyrinth;
using HapticResearch.Levels;

namespace HapticResearch.EditorTools
{
    // Genera in scena la geometria del labirinto a partire dal MazeLayoutAsset: muri,
    // piastrelle di scelta e tappe. Il layout e' l'unica fonte, cosi' la logica di gioco
    // e i cubi in scena non possono raccontare due labirinti diversi.
    //
    // Il vecchio labirinto (i 34 cubi messi a mano + i pad termici) non viene cancellato:
    // finisce sotto un root disattivato "Labirinto_vecchio". Il confronto resta a portata
    // di mano e la scena di backup Labyrinth_old.unity e' la seconda rete.
    //
    // Idempotente: rigenera SOLO i due root che possiede e non tocca il resto della scena.
    // Da menu con Labyrinth aperta, oppure headless:
    //   Unity -batchmode -quit -projectPath . -executeMethod HapticResearch.EditorTools.MazeGeometryBuilder.BuildHeadless
    public static class MazeGeometryBuilder
    {
        private const string ScenePath = "Assets/Scenes/Labyrinth.unity";
        private const string MazeRootName = "Maze";
        private const string GeometryRootName = "MazeGeometry";
        private const string TilesRootName = "MazeTiles";
        private const string ZonesRootName = "MazeZones";
        private const string ArchiveRootName = "Labirinto_vecchio";

        private const string SettingsDir = "Assets/Settings/Labyrinth";
        private const string LayoutPath = SettingsDir + "/MazeLayout_Level2.asset";
        private const string ProfilePath = SettingsDir + "/HapticProfile_Default.asset";
        private const string WallMatPath = "Assets/Materials/MuroLabirinto.mat";
        private const string TileMatPath = "Assets/Materials/PiastrellaScelta.mat";

        [MenuItem("HapticResearch/Level 2/Genera geometria labirinto")]
        public static void BuildFromMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError($"[MazeBuilder] Apri {ScenePath} prima di generare.");
                return;
            }
            if (Build(scene, out string report)) Debug.Log("[MazeBuilder] " + report);
            else Debug.LogError("[MazeBuilder] " + report);
        }

        public static void BuildHeadless()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!Build(scene, out string report))
            {
                Debug.LogError("[MazeBuilder] " + report);
                EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MazeBuilder] " + report + " Scena salvata.");
        }

        // --- Costruzione ------------------------------------------------------------

        public static bool Build(Scene scene, out string report)
        {
            var layout = EnsureLayout();
            var profile = EnsureProfile();

            if (!layout.Validate(out string errors, out string notes))
            {
                report = "Layout NON valido, niente e' stato generato:\n" + errors;
                return false;
            }
            // Le note non bloccano: descrivono situazioni normali ma fragili (tipicamente
            // una tasca cieca che confina col percorso, separata solo da un muro).
            if (!string.IsNullOrEmpty(notes))
                Debug.LogWarning("[MazeBuilder] Note sul layout (non bloccanti):\n" + notes);

            var map = layout.BuildMap();
            var anchor = FindAnchor(scene);

            // Il template della configurazione aptica si cerca PRIMA di archiviare il
            // vecchio labirinto: e' uno dei suoi cubi.
            var template = FindTouchableTemplate(scene);

            int archived = ArchiveOldGeometry(scene, anchor);

            var mazeRoot = EnsureRoot(scene, MazeRootName, anchor);

            // Il layout e' scritto in coordinate del partecipante (lui a -z che guarda
            // verso +z, la sua destra a +x). Questa rotazione le porta in quelle del
            // tavolo: in Labyrinth.unity il partecipante e' dal lato opposto, quindi 180.
            Undo.RecordObject(mazeRoot.transform, "Orienta il labirinto");
            mazeRoot.transform.localRotation = Quaternion.Euler(0f, layout.ParticipantYaw, 0f);
            mazeRoot.transform.localPosition = Vector3.zero;
            mazeRoot.transform.localScale = Vector3.one;

            var runtime = EnsureRuntime(mazeRoot, layout);

            var geometryRoot = ResetChildRoot(mazeRoot.transform, GeometryRootName);
            var tilesRoot = ResetChildRoot(mazeRoot.transform, TilesRootName);

            var wallMat = EnsureMaterial(WallMatPath, new Color(0.62f, 0.60f, 0.56f));
            var tileMat = EnsureMaterial(TileMatPath, new Color(0.80f, 0.74f, 0.58f));

            var walls = BuildWalls(map, geometryRoot.transform, template, profile, wallMat);
            int tiles = BuildTiles(map, tilesRoot.transform, template, profile, tileMat);
            var zones = BuildZones(scene, map, mazeRoot.transform);

            WireManager(scene, runtime, walls, zones, map);

            EditorSceneManager.MarkSceneDirty(scene);

            report = $"Generato: {walls.Count} blocchi di muro, {tiles} piastrelle di scelta, " +
                     $"{zones.Count} tappe. Archiviati {archived} oggetti del vecchio labirinto sotto " +
                     $"'{ArchiveRootName}' (disattivato). Ingombro {map.TotalWidth * 100f:0.#} x {map.TotalDepth * 100f:0.#} cm.";
            return true;
        }

        // --- Muri ---------------------------------------------------------------------

        private static List<Collider> BuildWalls(MazeMap map, Transform parent, WeArtTouchableObject template,
                                                 HapticProfile profile, Material mat)
        {
            var result = new List<Collider>();
            var runs = map.WallRuns();
            for (int i = 0; i < runs.Count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Muro_{i:00}";
                go.transform.SetParent(parent, false);
                go.transform.localPosition = runs[i].LocalCenter;
                go.transform.localScale = runs[i].LocalSize;
                go.GetComponent<Renderer>().sharedMaterial = mat;

                var col = go.GetComponent<BoxCollider>();
                col.isTrigger = true; // il dito attraversa: la sensazione la danno i pad aptici
                result.Add(col);

                AddHaptics(go, template);
                ConfigureTouchable(go.GetComponent<WeArtTouchableObject>(),
                                   profile.WallStiffness, profile.WallTexture, profile.WallTextureVolume);

                Undo.RegisterCreatedObjectUndo(go, "Genera muri labirinto");
            }
            return result;
        }

        // --- Piastrelle di scelta --------------------------------------------------------

        // Il collider arriva fino alla cima dei muri (la punta dell'indice sta a ~0.94),
        // ma la parte VISIBILE e' una lastra piatta appoggiata al tavolo: niente scalino,
        // niente esplorazione verticale. Quello che si sente e' la texture, non un gradino.
        //
        // Ramo giusto e ramo sbagliato hanno la STESSA identica apparenza: se l'operatore
        // vedesse la risposta sul tavolo rischierebbe di segnalarla senza volerlo. La
        // distinzione vive solo nei gizmo dell'editor.
        private static int BuildTiles(MazeMap map, Transform parent, WeArtTouchableObject template,
                                      HapticProfile profile, Material mat)
        {
            int count = 0;
            for (int c = 0; c < map.Columns; c++)
            {
                for (int r = 0; r < map.Rows; r++)
                {
                    if (map.KindAt(c, r) != CellKind.ChoiceTile) continue;

                    map.CellVolume(c, r, out Vector3 center, out Vector3 size);

                    var go = new GameObject($"Piastrella_{c}_{r}");
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = center;

                    var col = go.AddComponent<BoxCollider>();
                    col.size = size;
                    col.isTrigger = true;

                    // Lastra visibile, spessa 2 mm, appoggiata al piano.
                    var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slab.name = "Lastra";
                    Object.DestroyImmediate(slab.GetComponent<Collider>());
                    slab.transform.SetParent(go.transform, false);
                    slab.transform.localPosition = new Vector3(0f, -size.y * 0.5f + 0.001f, 0f);
                    slab.transform.localScale = new Vector3(size.x, 0.002f, size.z);
                    slab.GetComponent<Renderer>().sharedMaterial = mat;

                    AddHaptics(go, template);
                    ConfigureTouchable(go.GetComponent<WeArtTouchableObject>(),
                                       profile.TileStiffness, profile.TileTexture, profile.TileTextureVolume);

                    Undo.RegisterCreatedObjectUndo(go, "Genera piastrelle labirinto");
                    count++;
                }
            }
            return count;
        }

        // --- Tappe ------------------------------------------------------------------------

        // Restano solo ingresso e uscita: i checkpoint intermedi sono sostituiti dai bivi.
        private static List<MazeZone> BuildZones(Scene scene, MazeMap map, Transform mazeFrame)
        {
            var root = EnsureRoot(scene, ZonesRootName, null);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);

            float radius = map.CorridorWidth * 0.45f;
            var zones = new List<MazeZone>
            {
                MakeZone(root.transform, mazeFrame, "Zona_Ingresso", MazeZone.Kind.Entrance, "ingresso",
                         map.CellCenter(map.EntranceCell.x, map.EntranceCell.y), radius),
                MakeZone(root.transform, mazeFrame, "Zona_Uscita", MazeZone.Kind.Exit, "uscita",
                         map.CellCenter(map.ExitCell.x, map.ExitCell.y), radius),
            };
            return zones;
        }

        private static MazeZone MakeZone(Transform parent, Transform mazeFrame, string name,
                                         MazeZone.Kind kind, string label, Vector3 local, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = ToWorld(mazeFrame, local) + new Vector3(0f, 0.001f, 0f);

            var zone = go.AddComponent<MazeZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("label").stringValue = label;
            so.FindProperty("radius").floatValue = radius;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(go, "Genera tappe labirinto");
            return zone;
        }

        // --- Configurazione aptica ------------------------------------------------------

        // Rigidbody + WeArtTouchableObject si COPIANO da un oggetto gia' funzionante della
        // scena (stesso trucco di Level1SetupTool): cosi' non si dipende dai nomi dei campi
        // interni del SDK, che cambiano fra le versioni. Se il template manca si crea da
        // zero con i valori minimi documentati in CLAUDE.md.
        private static void AddHaptics(GameObject go, WeArtTouchableObject template)
        {
            if (template != null)
            {
                var srcBody = template.GetComponent<Rigidbody>();
                if (srcBody != null) { UnityEditorInternal.ComponentUtility.CopyComponent(srcBody); UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go); }
                UnityEditorInternal.ComponentUtility.CopyComponent(template);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);
            }

            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.mass = 1f;

            if (go.GetComponent<WeArtTouchableObject>() == null) go.AddComponent<WeArtTouchableObject>();
        }

        // I campi interni del SDK si scrivono con SerializedObject e non con le proprieta'
        // pubbliche: quelle chiamano UpdateTouchedHaptics(), che in edit mode non ha senso.
        private static void ConfigureTouchable(WeArtTouchableObject touchable, float stiffness,
                                               WeArt.Core.TextureType texture, float volume)
        {
            if (touchable == null) return;
            var so = new SerializedObject(touchable);

            SetFloat(so, "_stiffness._value", stiffness);
            SetBool(so, "_stiffness._active", true);

            SetEnum(so, "_texture._textureType", (int)texture);
            SetBool(so, "_texture._active", true);
            SetFloat(so, "_texture._volume", volume);
            SetBool(so, "_texture._forcedVelocity", false);

            // La temperatura dei muri e delle piastrelle NON si attua per contatto: la
            // pilota il ThermalJunctionCue, che arma un bivio per volta.
            SetBool(so, "_temperature._active", false);

            SetBool(so, "_disableDynamicForce", true); // senza, la forza arriva sbagliata sulle dita
            SetBool(so, "_graspable", false);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(SerializedObject so, string path, float v)
        { var p = so.FindProperty(path); if (p != null) p.floatValue = v; }

        private static void SetBool(SerializedObject so, string path, bool v)
        { var p = so.FindProperty(path); if (p != null) p.boolValue = v; }

        private static void SetEnum(SerializedObject so, string path, int v)
        { var p = so.FindProperty(path); if (p != null) p.enumValueIndex = v; }

        // --- Vecchio labirinto -------------------------------------------------------------

        // Criterio identico a quello che LabyrinthSetupTool usa per raccogliere i muri:
        // root di nome Cube*/Cylinder*, con WeArtTouchableObject, alla quota dei muri.
        // Cosi' non si toccano i cubi di calibrazione dei tracker (che non sono root) ne'
        // le forme di Level 1.
        private static int ArchiveOldGeometry(Scene scene, Transform anchor)
        {
            var victims = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == ArchiveRootName || root.name == MazeRootName) continue;
                if (!root.name.StartsWith("Cube") && !root.name.StartsWith("Cylinder")) continue;
                if (root.GetComponent<WeArtTouchableObject>() == null) continue;
                float y = root.transform.position.y;
                if (y < 0.84f || y > 0.96f) continue;
                victims.Add(root);
            }
            if (victims.Count == 0) return 0;

            var archive = EnsureRoot(scene, ArchiveRootName, null);
            foreach (var v in victims)
            {
                Undo.SetTransformParent(v.transform, archive.transform, "Archivia vecchio labirinto");
            }
            archive.SetActive(false);
            return victims.Count;
        }

        private static WeArtTouchableObject FindTouchableTemplate(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("Cube")) continue;
                var t = root.GetComponent<WeArtTouchableObject>();
                if (t != null && root.transform.position.y > 0.84f && root.transform.position.y < 0.96f) return t;
            }
            // Ripiego: un qualunque touchable della scena (anche archiviato).
            return Object.FindFirstObjectByType<WeArtTouchableObject>(FindObjectsInactive.Include);
        }

        // --- Cablaggio ----------------------------------------------------------------------

        private static void WireManager(Scene scene, MazeRuntime runtime, List<Collider> walls,
                                        List<MazeZone> zones, MazeMap map)
        {
            var manager = Object.FindFirstObjectByType<LabyrinthManager>(FindObjectsInactive.Include);
            if (manager == null) return;

            var so = new SerializedObject(manager);

            var zoneList = so.FindProperty("zones");
            zoneList.ClearArray();
            for (int i = 0; i < zones.Count; i++)
            {
                zoneList.InsertArrayElementAtIndex(i);
                zoneList.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            }

            var wallList = so.FindProperty("walls");
            wallList.ClearArray();
            for (int i = 0; i < walls.Count; i++)
            {
                wallList.InsertArrayElementAtIndex(i);
                wallList.GetArrayElementAtIndex(i).objectReferenceValue = walls[i];
            }

            var prefix = so.FindProperty("wallNamePrefix");
            if (prefix != null) prefix.stringValue = "Muro";

            // Il faro deve coprire il tavolo, non il vecchio labirinto largo 1.5 m.
            var far = so.FindProperty("beaconFarDistance");
            if (far != null) far.floatValue = Mathf.Max(0.4f, map.TotalWidth);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        // --- Asset e root -----------------------------------------------------------------------

        private static MazeLayoutAsset EnsureLayout()
        {
            var a = AssetDatabase.LoadAssetAtPath<MazeLayoutAsset>(LayoutPath);
            if (a != null) return a;
            EnsureFolder(SettingsDir);
            a = ScriptableObject.CreateInstance<MazeLayoutAsset>();
            AssetDatabase.CreateAsset(a, LayoutPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MazeBuilder] Creato {LayoutPath} con il layout di default.");
            return a;
        }

        private static HapticProfile EnsureProfile()
        {
            var a = AssetDatabase.LoadAssetAtPath<HapticProfile>(ProfilePath);
            if (a != null) return a;
            EnsureFolder(SettingsDir);
            a = ScriptableObject.CreateInstance<HapticProfile>();
            AssetDatabase.CreateAsset(a, ProfilePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MazeBuilder] Creato {ProfilePath} con i valori di default.");
            return a;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash), leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static Material EnsureMaterial(string path, Color color)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            return m;
        }

        private static Transform FindAnchor(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Table") return root.transform;
            return null;
        }

        // Cerca in TUTTA la gerarchia, non solo fra i root: "Maze" viene appeso al tavolo
        // per restare ancorato, quindi al secondo lancio non sarebbe piu' un root e il
        // tool ne creerebbe un duplicato.
        private static GameObject EnsureRoot(Scene scene, string name, Transform parent)
        {
            var found = FindByName(scene, name);
            if (found != null)
            {
                // Se e' finito nel posto sbagliato (tool vecchio, spostato a mano) si rimette
                // sotto l'ancora, conservando la posizione locale: le coordinate del
                // labirinto sono relative al tavolo.
                if (parent != null && found.transform.parent != parent)
                {
                    Undo.SetTransformParent(found.transform, parent, "Riancora il labirinto");
                    found.transform.localPosition = Vector3.zero;
                    found.transform.localRotation = Quaternion.identity;
                    found.transform.localScale = Vector3.one;
                }
                return found;
            }

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            if (parent != null) go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Crea root labirinto");
            return go;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }

        private static GameObject ResetChildRoot(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Crea root labirinto");
            return go;
        }

        private static MazeRuntime EnsureRuntime(GameObject mazeRoot, MazeLayoutAsset layout)
        {
            var runtime = mazeRoot.GetComponent<MazeRuntime>();
            if (runtime == null) runtime = Undo.AddComponent<MazeRuntime>(mazeRoot);

            var so = new SerializedObject(runtime);
            so.FindProperty("layout").objectReferenceValue = layout;
            so.ApplyModifiedPropertiesWithoutUndo();
            return runtime;
        }

        private static Vector3 ToWorld(Transform frame, Vector3 local) =>
            frame == null ? local : frame.position + frame.rotation * local;
    }
}
