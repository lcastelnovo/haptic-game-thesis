using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using HapticResearch.Exploration;
using HapticResearch.Labyrinth;
using HapticResearch.Memory;

namespace HapticResearch.EditorTools
{
    // Genera il dato e la scena del memory tattile. Stessa filosofia di MazeGeometryBuilder:
    // il layout e' l'unica fonte, la griglia si RIGENERA, non si sposta a mano.
    //
    // Come Level3SceneBuilder: "Genera scena" crea Level3_Memory.unity copiando
    // ViveTrackerScene.unity e si ferma se la scena esiste gia'; "Rigenera griglia" aggiorna
    // solo le tessere di una scena aperta.
    //
    // Gli asset dei layout vengono SOVRASCRITTI coi dati incorporati qui: un ritocco a mano
    // nell'Inspector sparisce alla generazione successiva (lo dice un warning).
    public static class MemorySceneBuilder
    {
        private const string TemplateScenePath = "Assets/Scenes/ViveTrackerScene.unity";
        private const string AssetDir = "Assets/Settings/Memory";
        private const string LayoutPath = AssetDir + "/MemoryLayout_v1.asset";
        private const string ThermalLayoutPath = AssetDir + "/MemoryLayout_Termico_v1.asset";

        private const string ProfilePath = "Assets/Settings/Labyrinth/HapticProfile_Default.asset";
        private const string ContactClipPath = "Assets/Audio/Level2/wall_bump.mp3";
        private const string DwellToneClipPath = "Assets/Audio/Level2/dwell_tone.mp3";
        private const string ReadyClipPath = "Assets/Audio/Level2/reading_ready.mp3";
        private const string PairClipPath = "Assets/Audio/Level2/checkpoint_chime.mp3";
        private const string MismatchClipPath = "Assets/Audio/Level2/branch_down.mp3";

        private const string TableRootName = "Table";
        private const string GridRootName = "Memory";
        private const string ManagerName = "Level3MemoryManager";

        // --- Menu ------------------------------------------------------------------------

        [MenuItem("HapticResearch/Level 3 Memory/Genera asset layout")]
        public static void GenerateAssetsFromMenu()
        {
            if (EnsureLayouts(out string report)) Debug.Log("[MemoryBuilder] " + report);
            else Debug.LogError("[MemoryBuilder] " + report);
        }

        [MenuItem("HapticResearch/Level 3 Memory/Genera scena memory")]
        public static void GenerateSceneFromMenu()
        {
            if (TryGenerateScene(out string report)) Debug.Log("[MemoryBuilder] " + report);
            else Debug.LogError("[MemoryBuilder] " + report);
        }

        [MenuItem("HapticResearch/Level 3 Memory/Rigenera griglia")]
        public static void RegenerateGridFromMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MemorySetupTool.ScenePath)
            {
                Debug.LogError($"[MemoryBuilder] Apri {MemorySetupTool.ScenePath} prima (scena attiva: {scene.path}).");
                return;
            }
            if (!UpdateSceneContent(scene, out string report)) { Debug.LogError("[MemoryBuilder] " + report); return; }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MemoryBuilder] " + report);
        }

        // Asset + scena, da riga di comando:
        //   Unity -batchmode -quit -projectPath . -executeMethod HapticResearch.EditorTools.MemorySceneBuilder.GenerateHeadless
        // TryGenerateScene scrive gia' gli asset: qui non si richiama EnsureLayouts.
        public static void GenerateHeadless()
        {
            if (!TryGenerateScene(out string report)) { Debug.LogError("[MemoryBuilder] " + report); EditorApplication.Exit(1); return; }
            Debug.Log("[MemoryBuilder] " + report);
        }

        // Scena gia' esistente, da riga di comando: come "Rigenera griglia", senza aprire l'editor.
        // Gli asset dei layout NON si toccano (per quelli c'e' "Genera asset layout").
        //   Unity -batchmode -quit -projectPath . -executeMethod HapticResearch.EditorTools.MemorySceneBuilder.RegenerateHeadless
        public static void RegenerateHeadless()
        {
            string path = MemorySetupTool.ScenePath;
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
            {
                Debug.LogError($"[MemoryBuilder] '{path}' non esiste: 'Genera scena memory' prima.");
                EditorApplication.Exit(1);
                return;
            }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!UpdateSceneContent(scene, out string report)) { Debug.LogError("[MemoryBuilder] " + report); EditorApplication.Exit(1); return; }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MemoryBuilder] " + report);
        }

        // --- Orchestrazione ---------------------------------------------------------------

        private static bool TryGenerateScene(out string report)
        {
            // Prima il controllo sulla scena, poi gli asset: se la scena c'e' gia' si esce
            // senza aver riscritto i layout (magari ritoccati a mano dopo il primo test).
            string path = MemorySetupTool.ScenePath;
            bool exists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));
            if (exists)
            {
                report = $"'{path}' esiste gia': NON la tocco, e non tocco nemmeno gli asset dei layout. " +
                         "Aprila e lancia 'HapticResearch/Level 3 Memory/Rigenera griglia'.";
                return false;
            }

            if (!EnsureLayouts(out report)) return false;
            if (!AssetDatabase.CopyAsset(TemplateScenePath, path))
            {
                report = $"Copia da '{TemplateScenePath}' a '{path}' fallita.";
                return false;
            }
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!UpdateSceneContent(scene, out report)) return false;
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        // Rigenera il root 'Memory' con le tessere, il manager, poi il cablaggio comune.
        private static bool UpdateSceneContent(Scene scene, out string report)
        {
            var tableRoot = LevelSceneWiring.FindRoot(scene, TableRootName);
            if (tableRoot == null)
            {
                report = $"Nella scena non c'e' un root '{TableRootName}': la scena non e' partita da ViveTrackerScene.unity?";
                return false;
            }

            // Si rigenera col layout gia' assegnato al manager, se c'e': chi ha messo la
            // variante termica non se la vede sostituire con la v1.
            var existing = LevelSceneWiring.FindInScene<MemoryManager>(scene);
            var layout = existing != null && existing.Layout != null
                ? existing.Layout
                : AssetDatabase.LoadAssetAtPath<MemoryLayoutAsset>(LayoutPath);
            if (layout == null) { report = $"Manca '{LayoutPath}': 'Genera asset layout' prima."; return false; }
            if (!layout.Validate(out string error)) { report = $"Layout '{layout.LayoutId}' non valido: {error}"; return false; }

            string disabled = DisableTemplateProps(scene, tableRoot);

            var root = ResetChildRoot(tableRoot.transform, GridRootName);
            // Il root E' il sistema del partecipante: centro della griglia sul piano delle
            // tessere, ruotato di participantYaw. MemoryManager ragiona nelle sue coordinate.
            root.transform.localPosition = new Vector3(layout.CenterX, layout.TileTopY, layout.CenterZ);
            root.transform.localRotation = Quaternion.Euler(0f, layout.ParticipantYaw, 0f);

            var grid = layout.CreateGrid();
            for (int t = 0; t < grid.TileCount; t++) BuildTile(root.transform, grid, t, layout);

            EnsureManager(scene, layout, root.transform);
            EditorSceneManager.MarkSceneDirty(scene);

            SceneManager.SetActiveScene(scene);
            if (!MemorySetupTool.Configure())
            {
                report = "MemorySetupTool.Configure() ha segnalato un cablaggio fallito: controlla la Console.";
                return false;
            }
            bool tilesOk = MemorySetupTool.ValidateTiles(asWarnings: false);
            report = $"{grid.TileCount} tessere generate sotto '{TableRootName}/{GridRootName}' dal layout '{layout.LayoutId}'. " +
                     $"Validazione tessere: {(tilesOk ? "OK" : "PROBLEMI, vedi Console")}. " +
                     $"Forme demo del template spente: {disabled}.";
            return true;
        }

        // Il template ViveTrackerScene porta sul tavolo le forme del Level 1 (Cube, Cylinder,
        // Prism, Star): alte ~10 cm, touchable con texture e durezza accese, e il Cylinder
        // copre proprio le tessere 9 e 10 del riscaldamento. Il dito le sentirebbe insieme
        // alle tessere, e in fase 2 due coperte non sarebbero piu' neutre. Si SPENGONO, non
        // si cancellano: gli oggetti del template restano recuperabili (niente guid persi).
        // Criterio: ogni root diverso da 'Table' che contiene un WeArtTouchableObject.
        private static string DisableTemplateProps(Scene scene, GameObject tableRoot)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == tableRoot || !go.activeSelf) continue;
                if (go.GetComponentsInChildren<WeArtTouchableObject>(true).Length == 0) continue;
                Undo.RecordObject(go, "Spegni forme demo del template");
                go.SetActive(false);
                names.Add(go.name);
            }
            string list = names.Count == 0 ? "nessuna" : string.Join(", ", names);
            if (names.Count > 0) Debug.Log($"[MemoryBuilder] Spente le forme demo del template: {list}.");
            return list;
        }

        // --- Tessere ----------------------------------------------------------------------

        // Stesso schema delle piastrelle del labirinto (MazeGeometryBuilder.BuildTiles +
        // MazeMap.CellVolume): il VOLUME trigger va dal piano del tavolo (0.85) a 10 cm sopra
        // (0.95, la cima dei muri del labirinto), perche' la punta dell'indice sta a ~0.94 e
        // col tracker puo' anche scendere sotto il piano; la parte VISIBILE resta la lastra
        // piatta di sempre (top a 0.86). Con una lastra trigger di 4 mm sospesa a 0.856 il
        // guanto non sentiva niente mentre la logica diceva "sulla tessera".
        private const float TableTopY = 0.85f;
        private const float TriggerHeight = 0.10f;
        // Soglia della logica (MemoryManager.maxTipHeight): cima del trigger + ~1 cm di raggio
        // del polpastrello. Sotto, il collider del dito tocca il trigger e il guanto sente la
        // tessera; sopra, la mano e' sollevata e non gira niente. Il labirinto usa 0.97 con muri
        // a 0.95; qui 1 cm basta: logica e guanto vedono il contatto nello stesso volume.
        private const float MaxTipHeight = TableTopY + TriggerHeight + 0.01f;

        private static void BuildTile(Transform root, MemoryGridMap grid, int index, MemoryLayoutAsset layout)
        {
            var go = new GameObject($"Tessera_{index:00}");
            go.transform.SetParent(root, false);
            grid.Center(index, out float x, out float z);
            // Il root sta alla quota del TOP delle tessere: la tessera si appoggia li'.
            go.transform.localPosition = new Vector3(x, 0f, z);

            // Il dito attraversa la tessera: la sensazione la danno i pad aptici.
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            float bottom = TableTopY - layout.TileTopY;   // -0.01: dal piano del tavolo
            col.center = new Vector3(0f, bottom + TriggerHeight * 0.5f, 0f);
            col.size = new Vector3(layout.TileSize, TriggerHeight, layout.TileSize);

            // Lastra visibile, senza collider: il cubo scende di mezzo spessore dal top.
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Lastra";
            UnityEngine.Object.DestroyImmediate(slab.GetComponent<Collider>());
            slab.transform.SetParent(go.transform, false);
            slab.transform.localPosition = new Vector3(0f, -layout.TileThickness * 0.5f, 0f);
            slab.transform.localScale = new Vector3(layout.TileSize, layout.TileThickness, layout.TileSize);

            // Rigidbody obbligatorio: il sistema aptico reagisce solo a oggetti che ce l'hanno.
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.mass = 1f;

            var touchable = go.AddComponent<WeArtTouchableObject>();
            var tso = new SerializedObject(touchable);
            SetBool(tso, "_disableDynamicForce", true);   // senza, la forza arriva sbagliata sulle dita
            SetBool(tso, "_graspable", false);
            SetBool(tso, "_temperature._active", false);  // la temperatura passa solo da ThermalObjectCue
            tso.ApplyModifiedPropertiesWithoutUndo();

            var tile = go.AddComponent<MemoryTile>();
            var so = new SerializedObject(tile);
            so.FindProperty("index").intValue = index;
            so.FindProperty("touchable").objectReferenceValue = touchable;
            so.FindProperty("tileRenderer").objectReferenceValue = slab.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(go, "Genera tessera memory");
        }

        // --- Manager ----------------------------------------------------------------------

        private static void EnsureManager(Scene scene, MemoryLayoutAsset layout, Transform gridRoot)
        {
            var manager = LevelSceneWiring.FindInScene<MemoryManager>(scene);
            if (manager == null)
            {
                var go = LevelSceneWiring.NewInScene(ManagerName, scene);
                manager = go.AddComponent<MemoryManager>();
            }

            var so = new SerializedObject(manager);
            so.FindProperty("layout").objectReferenceValue = layout;
            so.FindProperty("gridRoot").objectReferenceValue = gridRoot;
            so.FindProperty("tiles").arraySize = 0;   // si riempie da sola in Awake
            so.FindProperty("maxTipHeight").floatValue = MaxTipHeight;   // coerente col trigger delle tessere
            LevelSceneWiring.SetIfEmpty(so, "profile", AssetDatabase.LoadAssetAtPath<HapticProfile>(ProfilePath));
            so.FindProperty("contactClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ContactClipPath);
            so.FindProperty("dwellToneClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DwellToneClipPath);
            so.FindProperty("readyClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ReadyClipPath);
            so.FindProperty("pairClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PairClipPath);
            so.FindProperty("mismatchClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MismatchClipPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Dato -------------------------------------------------------------------------

        private readonly struct SigData
        {
            public readonly string Id, Label;
            public readonly WeArt.Core.TextureType Texture;
            public readonly float Stiffness;
            public readonly ThermalRole Role;
            public SigData(string id, string label, WeArt.Core.TextureType texture, float stiffness, ThermalRole role)
            { Id = id; Label = label; Texture = texture; Stiffness = stiffness; Role = role; }
        }

        // Tre texture lontane fra loro x due durezze. Da confermare in hardware: la fase 1
        // serve proprio a dire quali di queste il TouchDIVER sa separare.
        private static readonly SigData[] V1 =
        {
            new SigData("roccia_dura", "Roccia dura", WeArt.Core.TextureType.CrushedRock, 0.9f, ThermalRole.Neutral),
            new SigData("roccia_morbida", "Roccia morbida", WeArt.Core.TextureType.CrushedRock, 0.2f, ThermalRole.Neutral),
            new SigData("tessuto_duro", "Tessuto duro", WeArt.Core.TextureType.TextileMedium, 0.9f, ThermalRole.Neutral),
            new SigData("tessuto_morbido", "Tessuto morbido", WeArt.Core.TextureType.TextileMedium, 0.2f, ThermalRole.Neutral),
            new SigData("metallo_duro", "Metallo duro", WeArt.Core.TextureType.ProfiledAluminiumMedium, 0.9f, ThermalRole.Neutral),
            new SigData("metallo_morbido", "Metallo morbido", WeArt.Core.TextureType.ProfiledAluminiumMedium, 0.2f, ThermalRole.Neutral),
        };

        // La prima e la seconda differiscono solo in durezza, la prima e la terza solo in
        // texture: una confusione nel riscaldamento dice quale canale non passa.
        private static readonly string[] Warmup = { "roccia_dura", "roccia_morbida", "tessuto_duro" };

        private static bool EnsureLayouts(out string report)
        {
            EnsureFolder(AssetDir);
            var v1 = WriteLayout(LayoutPath, "memory_v1", 1f, 1.5f, thermalMetal: false);
            // Variante termica: la temperatura si arma solo a tessera GIRATA (armarla durante
            // la sosta rivelerebbe la firma prima del flip). Una seconda tessera sbagliata resta
            // girata mismatchDelay secondi: 3.5 s coprono la salita del Peltier (2-3 s) e i 3 s
            // di tenuta minima di ThermalObjectCue, cosi' il freddo non arriva a tessera gia'
            // ricoperta. Su una coppia giusta la seconda tessera esce subito: li' la temperatura
            // non arriva comunque (limite scritto nella spec, "Variante termica").
            var thermal = WriteLayout(ThermalLayoutPath, "memory_termico_v1", 3f, 3.5f, thermalMetal: true);
            AssetDatabase.SaveAssets();

            if (!v1.Validate(out string e1)) { report = $"'{LayoutPath}' non valido: {e1}"; return false; }
            if (!thermal.Validate(out string e2)) { report = $"'{ThermalLayoutPath}' non valido: {e2}"; return false; }
            report = $"Layout scritti e validi: {LayoutPath}, {ThermalLayoutPath}.";
            return true;
        }

        private static MemoryLayoutAsset WriteLayout(string path, string layoutId, float dwellSeconds,
                                                     float mismatchDelay, bool thermalMetal)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MemoryLayoutAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MemoryLayoutAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                Debug.LogWarning($"[MemoryBuilder] '{path}' esisteva gia': firme e tempi vengono sovrascritti " +
                                 "coi dati incorporati nel builder. Ritocchi fatti a mano nell'Inspector sono persi.", asset);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("layoutId").stringValue = layoutId;
            so.FindProperty("dwellSeconds").floatValue = dwellSeconds;
            so.FindProperty("mismatchDelay").floatValue = mismatchDelay;

            var sigs = so.FindProperty("signatures");
            sigs.arraySize = V1.Length;
            for (int i = 0; i < V1.Length; i++)
            {
                var d = V1[i];
                var e = sigs.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = d.Id;
                e.FindPropertyRelative("label").stringValue = d.Label;
                SetEnumByName(e.FindPropertyRelative("texture"), d.Texture.ToString());
                e.FindPropertyRelative("textureVolume").floatValue = 100f;
                e.FindPropertyRelative("stiffness").floatValue = d.Stiffness;
                var role = thermalMetal && d.Texture == WeArt.Core.TextureType.ProfiledAluminiumMedium ? ThermalRole.Cool : d.Role;
                SetEnumByName(e.FindPropertyRelative("role"), role.ToString());
            }

            var warmup = so.FindProperty("warmupSignatureIds");
            warmup.arraySize = Warmup.Length;
            for (int i = 0; i < Warmup.Length; i++) warmup.GetArrayElementAtIndex(i).stringValue = Warmup[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Util -------------------------------------------------------------------------

        private static void SetBool(SerializedObject so, string path, bool v)
        {
            var p = so.FindProperty(path);
            if (p != null) p.boolValue = v;
        }

        // Per nome, non per intero: vedi il commento gemello in Level3SceneBuilder.
        private static void SetEnumByName(SerializedProperty property, string valueName)
        {
            int idx = Array.IndexOf(property.enumNames, valueName);
            if (idx < 0)
            {
                Debug.LogError($"[MemoryBuilder] '{valueName}' non e' un valore noto per '{property.propertyPath}': valore NON scritto.");
                return;
            }
            property.enumValueIndex = idx;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash), leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static GameObject ResetChildRoot(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, $"Crea {name}");
            return go;
        }
    }
}
