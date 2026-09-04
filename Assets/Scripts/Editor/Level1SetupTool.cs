using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using WeArt.Components;
using HapticResearch.Levels;

namespace HapticResearch.EditorTools
{
    // Tool editor per completare la scena Level1 SENZA cablaggi a mano:
    //   1. crea le forme "Shape_sfera" (cupola) e "Shape_prisma" (tetto) sul tavolo,
    //      copiando Rigidbody + WeArtTouchableObject da una forma gia' registrata (stessa
    //      ricetta CLAUDE.md: collider convex+trigger, kinematic, Disable Dynamic Force ON);
    //   2. le registra in ShapeRecognitionManager.shapes[] (id + sceneInstance + clip);
    //   3. crea il GameObject "MainMenu" con MainMenuManager (benvenuto parlato).
    //
    // Uso: aprire Level1_ShapeRecognition.unity -> menu "HapticResearch/Level 1/Configura
    // forme e menu" -> controllare in Scene view -> salvare la scena. Rilanciarlo e'
    // sicuro: cio' che esiste gia' viene saltato.
    public static class Level1SetupTool
    {
        // La mesh del prisma viene GENERATA dal tool e salvata qui: il vecchio Prism.prefab
        // ha la mesh nulla (la creava uno script ProBuilder ormai mancante dal progetto).
        private const string PrismMeshPath = "Assets/Models/PrismShape.asset";
        private const float TableTopY = 0.85f; // piano del tavolo (vedi TableTop in scena)

        // Entry point per esecuzione da riga di comando (batchmode):
        //   Unity -batchmode -quit -projectPath . -executeMethod
        //     HapticResearch.EditorTools.Level1SetupTool.ConfigureHeadless
        // Apre Level1, esegue Configure e salva scena + asset.
        public static void ConfigureHeadless()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level1_ShapeRecognition.unity");
            Configure();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("HapticResearch/Level 1/Configura forme e menu (sfera, prisma)")]
        public static void Configure()
        {
            var manager = Object.FindFirstObjectByType<ShapeRecognitionManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Level 1 Setup",
                    "Nessuno ShapeRecognitionManager in scena.\nApri Assets/Scenes/Level1_ShapeRecognition.unity e riprova.", "OK");
                return;
            }

            // Template dei componenti aptici: una forma GIA' registrata nel manager (in scena
            // ci sono piu' oggetti chiamati "Cube"/"Cylinder"/"Prism": cercare per nome
            // prenderebbe quello sbagliato, la lista shapes[] e' l'unica fonte di verita').
            var template = FindShapeTemplate(manager);
            if (template == null)
            {
                EditorUtility.DisplayDialog("Level 1 Setup",
                    "Nessuna forma con WeArtTouchableObject registrata in ShapeRecognitionManager.shapes[]: serve come modello dei componenti.", "OK");
                return;
            }

            int added = 0;

            // Sfera: mezza affondata nel tavolo -> al tatto e' una cupola liscia, bump basso
            // come da principi del progetto (niente oggetti alti).
            var sphere = EnsureSphere(template);
            if (sphere != null && RegisterShape(manager, "sfera", sphere)) added++;

            // Prisma: a tetto, spigolo in alto -> al tatto due facce inclinate, ben
            // distinguibile dal top piatto di cubo/cilindro e dalla cupola della sfera.
            var prism = EnsurePrism(template);
            if (prism != null && RegisterShape(manager, "prisma", prism)) added++;

            // Materiale sempre allineato al template, anche per forme gia' esistenti: se il
            // team cambia il materiale di cubo/cilindro basta rilanciare questo tool.
            SyncMaterial(sphere, template);
            SyncMaterial(prism, template);

            bool menuCreated = EnsureMainMenu();
            bool flowCreated = EnsureLevelFlow();

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log($"[Level1Setup] Fatto: {added} forme registrate, menu {(menuCreated ? "creato" : "gia' presente")}, " +
                      $"level flow {(flowCreated ? "creato" : "gia' presente")}. " +
                      "Controlla posizioni/rotazioni in Scene view e SALVA la scena.");
        }

        // --- Forme ---------------------------------------------------------------------

        // Prima sceneInstance valida (con WeArtTouchableObject) tra le forme registrate.
        private static GameObject FindShapeTemplate(ShapeRecognitionManager manager)
        {
            var so = new SerializedObject(manager);
            var shapes = so.FindProperty("shapes");
            for (int i = 0; i < shapes.arraySize; i++)
            {
                var inst = shapes.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("sceneInstance").objectReferenceValue as GameObject;
                if (inst != null && inst.GetComponent<WeArtTouchableObject>() != null) return inst;
            }
            return null;
        }

        // Cerca una forma creata da un run precedente: per nome tra gli oggetti con
        // WeArtTouchableObject (GameObject.Find da solo pescherebbe gli omonimi in scena).
        private static GameObject FindExistingShape(string name)
        {
            foreach (var t in Object.FindObjectsByType<WeArtTouchableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.gameObject.name == name) return t.gameObject;
            return null;
        }

        private static GameObject EnsureSphere(GameObject template)
        {
            var existing = FindExistingShape("Shape_sfera");
            if (existing != null) return existing;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Shape_sfera";
            // Centro esattamente sul piano: emerge solo la mezza sfera (cupola alta 7.5 cm).
            go.transform.position = new Vector3(0.304f, TableTopY, 0.077f);
            go.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            // Lo SphereCollider di default non segue la scala non uniforme e non e' trigger:
            // sostituito con MeshCollider convex+trigger come il Cylinder.
            Object.DestroyImmediate(go.GetComponent<SphereCollider>());
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            mc.isTrigger = true;

            FinishShape(go, template);
            return go;
        }

        private static GameObject EnsurePrism(GameObject template)
        {
            var existing = FindExistingShape("Shape_prisma");
            if (existing != null) return existing;

            var mesh = GetOrCreatePrismMesh();

            var go = new GameObject("Shape_prisma");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
            mc.isTrigger = true;

            // Mesh unitaria (1x1x1, base sul piano y=0): ingombro 15x15 cm, altezza 10 cm.
            go.transform.localScale = new Vector3(0.15f, 0.10f, 0.15f);
            go.transform.position = new Vector3(0.612f, TableTopY, 0.077f);

            FinishShape(go, template);
            return go;
        }

        // Prisma triangolare "a tetto": base 1x1 sul piano y=0, spigolo in alto a y=1.
        // Vertici duplicati per faccia -> normali piatte, spigoli netti anche al tatto visivo.
        private static Mesh GetOrCreatePrismMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PrismMeshPath);
            if (mesh != null) return mesh;

            // Sezione triangolare in XY, estrusa lungo Z.
            var a = new Vector3(-0.5f, 0f, -0.5f); // base davanti-sinistra
            var b = new Vector3(0.5f, 0f, -0.5f);  // base davanti-destra
            var c = new Vector3(0f, 1f, -0.5f);    // colmo davanti
            var d = new Vector3(-0.5f, 0f, 0.5f);  // base dietro-sinistra
            var e = new Vector3(0.5f, 0f, 0.5f);   // base dietro-destra
            var f = new Vector3(0f, 1f, 0.5f);     // colmo dietro

            var vertices = new[]
            {
                a, c, b,          // faccia davanti (triangolo)
                d, e, f,          // faccia dietro (triangolo)
                a, b, e, d,       // fondo (quad)
                a, d, f, c,       // falda sinistra (quad)
                b, c, f, e        // falda destra (quad)
            };
            var triangles = new[]
            {
                0, 1, 2,
                3, 4, 5,
                6, 7, 8, 6, 8, 9,
                10, 11, 12, 10, 12, 13,
                14, 15, 16, 14, 16, 17
            };

            mesh = new Mesh { name = "PrismShape", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            AssetDatabase.CreateAsset(mesh, PrismMeshPath);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static void SyncMaterial(GameObject go, GameObject template)
        {
            if (go == null || template == null) return;
            var templateRenderer = template.GetComponent<Renderer>();
            var renderer = go.GetComponent<Renderer>();
            if (templateRenderer == null || renderer == null) return;
            if (renderer.sharedMaterial != templateRenderer.sharedMaterial)
            {
                renderer.sharedMaterial = templateRenderer.sharedMaterial;
                Debug.Log($"[Level1Setup] Materiale di '{go.name}' allineato a '{template.name}'.");
            }
        }

        // Materiale + Rigidbody + WeArtTouchableObject copiati dal template: config identica
        // alle forme che gia' funzionano, senza dipendere dai nomi dei campi del SDK.
        private static void FinishShape(GameObject go, GameObject template)
        {
            SyncMaterial(go, template);

            ComponentUtility.CopyComponent(template.GetComponent<Rigidbody>());
            ComponentUtility.PasteComponentAsNew(go);
            ComponentUtility.CopyComponent(template.GetComponent<WeArtTouchableObject>());
            ComponentUtility.PasteComponentAsNew(go);

            Undo.RegisterCreatedObjectUndo(go, "Crea forma Level1");
        }

        // Aggiunge la def in shapes[] (se l'id non c'e' gia') via SerializedObject:
        // stesso effetto di compilare l'Inspector a mano.
        private static bool RegisterShape(ShapeRecognitionManager manager, string id, GameObject sceneInstance)
        {
            var so = new SerializedObject(manager);
            var shapes = so.FindProperty("shapes");

            for (int i = 0; i < shapes.arraySize; i++)
            {
                var el = shapes.GetArrayElementAtIndex(i);
                if (el.FindPropertyRelative("id").stringValue == id)
                {
                    Debug.Log($"[Level1Setup] Forma '{id}' gia' registrata: salto.");
                    return false;
                }
            }

            int idx = shapes.arraySize;
            shapes.InsertArrayElementAtIndex(idx);
            var def = shapes.GetArrayElementAtIndex(idx);
            def.FindPropertyRelative("id").stringValue = id;
            def.FindPropertyRelative("sceneInstance").objectReferenceValue = sceneInstance;
            def.FindPropertyRelative("prefab").objectReferenceValue = null;
            // Clip di riserva: in gioco parla prima la traccia find_<id> del NarrationManager.
            def.FindPropertyRelative("announceClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Resources/Voice/find_{id}.mp3");

            so.ApplyModifiedProperties();
            return true;
        }

        // --- Menu ----------------------------------------------------------------------

        private static bool EnsureMainMenu()
        {
            if (Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include) != null)
                return false;

            var go = new GameObject("MainMenu");
            var menu = go.AddComponent<MainMenuManager>();
            var mso = new SerializedObject(menu);
            mso.FindProperty("welcomeFallbackClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Voice/menu_welcome.mp3");
            mso.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(go, "Crea MainMenu Level1");
            return true;
        }

        // --- Passaggio al livello 2 ------------------------------------------------------

        private static bool EnsureLevelFlow()
        {
            if (Object.FindFirstObjectByType<LevelFlowController>(FindObjectsInactive.Include) != null)
                return false;

            var go = new GameObject("LevelFlow");
            go.AddComponent<LevelFlowController>(); // manager e scena successiva: default/auto-find

            Undo.RegisterCreatedObjectUndo(go, "Crea LevelFlow Level1");
            return true;
        }
    }
}
