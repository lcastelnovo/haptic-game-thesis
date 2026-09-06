using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using HapticResearch.Branding;
using HapticResearch.Debugging;
using HapticResearch.Experiment;
using HapticResearch.Hands;
using HapticResearch.Levels;
using HapticResearch.Voice;

namespace HapticResearch.EditorTools
{
    // Cabla la scena del labirinto (Level 2) con tutto quello che Level 1 ha e lei no:
    // mani (rig mouse/demo + componenti nostri sulle mani WEART), modalita' demo,
    // branding UniBS, comandi vocali, menu in-level, flusso di fine livello, logger di
    // sessione, pannello F1 e il LabyrinthManager con le tappe (ingresso/meta/uscita).
    //
    // Level 1 e' la SOURCE OF TRUTH: i componenti che esistono gia' li' vengono COPIATI
    // (valori Inspector compresi) aprendo Level1_ShapeRecognition.unity in additiva e
    // rimappando i riferimenti per percorso (es. Table/TableTop, TopCamera). Cosi' se
    // qualcuno ritocca una mano in Level 1, basta rilanciare questo tool.
    //
    // Idempotente: ogni Ensure* aggiunge solo cio' che manca e NON tocca cio' che c'e' gia'
    // (tappe spostate a mano, clip cambiati...). Per riallineare mani e singleton a una
    // Level 1 modificata c'e' la voce di menu "Ricostruisci da Level 1", che li cancella e
    // li ricrea. Da menu (con Labyrinth aperta) o headless:
    //   Unity -batchmode -quit -projectPath . -executeMethod HapticResearch.EditorTools.LabyrinthSetupTool.ConfigureHeadless
    public static class LabyrinthSetupTool
    {
        private const string ScenePath = "Assets/Scenes/Labyrinth.unity";
        private const string Level1Path = "Assets/Scenes/Level1_ShapeRecognition.unity";
        private const string LeftHandPrefab = "Packages/com.weart.sdk/Runtime/Prefabs/WEARTLeftHand.prefab";
        private const string RightHandPrefab = "Packages/com.weart.sdk/Runtime/Prefabs/WEARTRightHand.prefab";

        // Tappe provvisorie nel corridoio in basso (tra i muri 26/27 sopra e 16/15 sotto):
        // ingresso nella stanzetta a sinistra (pad freddo), meta sotto il pad caldo, uscita
        // verso destra. Si spostano dall'Inspector quando il labirinto cambia.
        private static readonly (string name, MazeZone.Kind kind, string label, Vector3 pos)[] DefaultZones =
        {
            ("Zona_Ingresso", MazeZone.Kind.Entrance, "ingresso", new Vector3(-0.60f, 0.851f, -0.23f)),
            ("Zona_Meta", MazeZone.Kind.Checkpoint, "meta", new Vector3(-0.30f, 0.851f, -0.175f)),
            ("Zona_Uscita", MazeZone.Kind.Exit, "uscita", new Vector3(0.30f, 0.851f, -0.175f)),
        };

        public static void ConfigureHeadless()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!Configure())
            {
                Debug.LogError("[Level2Setup] Configurazione fallita: scena NON salvata.");
                EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Level2Setup] Scena salvata.");
        }

        [MenuItem("HapticResearch/Level 2/Configura labirinto (mani, HUD, voce, tappe)")]
        public static void ConfigureFromMenu() => Configure();

        // Cancella i blocchi copiati da Level 1 (mani, demo, branding, voce) e li ricrea:
        // da usare quando Level 1 cambia e si vuole riallineare il labirinto.
        [MenuItem("HapticResearch/Level 2/Ricostruisci da Level 1 (mani, demo, branding, voce)")]
        public static void RebuildFromLevel1()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError($"[Level2Setup] Apri {ScenePath} prima."); return; }
            foreach (var name in new[] { "HandManager", "DemoModeManager", "UnibsBranding", "VoiceControlManager" })
            {
                var go = FindRoot(scene, name);
                if (go != null) Undo.DestroyObjectImmediate(go);
            }
            Configure();
        }

        public static bool Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError($"[Level2Setup] Apri {ScenePath} prima di lanciare il tool (scena attiva: {scene.path}).");
                return false;
            }

            // Level 1 in additiva SOLO se non e' gia' aperta (in quel caso non va chiusa da qui).
            var level1 = SceneManager.GetSceneByPath(Level1Path);
            bool openedHere = false;
            if (!level1.isLoaded)
            {
                level1 = EditorSceneManager.OpenScene(Level1Path, OpenSceneMode.Additive);
                openedHere = true;
            }
            SceneManager.SetActiveScene(scene); // i nuovi GameObject nascono nella scena giusta
            int changes = 0;
            bool ok = true;
            try
            {
                if (EnsureHands(scene, level1, ref ok)) changes++;
                if (EnsureCopied<HandDemoModeController>(scene, level1, "DemoModeManager")) changes++;
                if (EnsureBranding(scene, level1)) changes++;
                if (EnsureVoice(scene, level1)) changes++;
                if (EnsureMainMenu(scene)) changes++;
                if (EnsureLevelFlow(scene)) changes++;
                if (EnsureSessionLogger(scene)) changes++;
                if (EnsureGraspDebugPanel(scene)) changes++;
                if (EnsureLevelManager(scene)) changes++;
                DisableTrackerDebugger(scene);
                EnsureInBuild();
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(level1, true); // Level 1 NON viene mai salvata da qui
            }

            if (changes > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Level2Setup] Fatto: {changes} blocchi aggiunti/aggiornati.");
            return ok;
        }

        // --- Mani: prefab WEART sotto HandManager + componenti nostri copiati da Level 1 ----

        private static bool EnsureHands(Scene scene, Scene level1, ref bool ok)
        {
            if (FindInScene<HandInputManager>(scene) != null) return false;

            var srcInput = FindInScene<HandInputManager>(level1);
            if (srcInput == null || srcInput.leftHand == null || srcInput.rightHand == null)
            {
                Debug.LogError("[Level2Setup] In Level 1 manca HandManager/HandInputManager con le due mani: impossibile copiare il rig.");
                ok = false;
                return false;
            }

            var manager = new GameObject(srcInput.gameObject.name);
            SceneManager.MoveGameObjectToScene(manager, scene);
            Undo.RegisterCreatedObjectUndo(manager, "Crea HandManager Level2");

            var left = InstantiateHand(LeftHandPrefab, srcInput.leftHand.gameObject, manager.transform, scene);
            var right = InstantiateHand(RightHandPrefab, srcInput.rightHand.gameObject, manager.transform, scene);

            // HandInputManager sul contenitore, poi rimappa leftHand/rightHand sulle nuove mani.
            var dstInput = manager.AddComponent<HandInputManager>();
            EditorUtility.CopySerialized(srcInput, dstInput);
            RemapReferences(dstInput, level1, scene);

            if (left == null || right == null)
            {
                Debug.LogError("[Level2Setup] Una delle mani non e' stata creata: controlla i prefab WEART nel pacchetto.");
                ok = false;
            }

            // Nel labirinto non ci sono forme da afferrare: il log di GloveGraspDetector a ogni
            // chiusura sarebbe solo rumore in Console.
            foreach (var g in manager.GetComponentsInChildren<GloveGraspDetector>(true))
            {
                var gso = new SerializedObject(g);
                var prop = gso.FindProperty("debugLog");
                if (prop != null) { prop.boolValue = false; gso.ApplyModifiedPropertiesWithoutUndo(); }
            }
            return true;
        }

        // Istanzia il prefab WEART della mano e vi copia sopra i componenti AGGIUNTI in Level 1
        // (HandPhysicsController, GloveGraspDetector...) e lo stato enabled di quelli del prefab
        // (es. WeArtDeviceTrackingObject spento: la mano demo non deve seguire i tracker).
        private static GameObject InstantiateHand(string prefabPath, GameObject src, Transform parent, Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Level2Setup] Prefab non trovato: {prefabPath}");
                return null;
            }

            var dst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            dst.transform.SetParent(parent, false);

            // 1) Override di proprieta' dell'istanza di Level 1 (collider delle dita solidi,
            //    tracking spento, palmo senza forza, posizione...). Valgono solo se la mano di
            //    Level 1 e' un'istanza dello STESSO prefab: le modifiche puntano agli oggetti
            //    dell'asset e si applicano pari pari.
            var srcAsset = PrefabUtility.GetCorrespondingObjectFromSource(src);
            if (srcAsset == prefab) CopyPrefabOverrides(src, dst, scene);
            else
            {
                Debug.LogWarning($"[Level2Setup] '{src.name}' in Level 1 non e' un'istanza di {prefabPath} " +
                                 $"(sorgente: {AssetDatabase.GetAssetPath(srcAsset)}): override del prefab NON copiati, allinea a mano.");
                dst.transform.localPosition = src.transform.localPosition;
                dst.transform.localRotation = src.transform.localRotation;
                dst.transform.localScale = src.transform.localScale;
            }
            dst.name = src.name;
            dst.SetActive(src.activeSelf);

            // 2) Componenti AGGIUNTI in Level 1 (anche sulle ossa e sul ghost).
            //    Primo passaggio: aggiunge e copia (i riferimenti puntano ancora a Level 1).
            var copied = new List<Component>();
            CopyAddedComponentsRecursive(src.transform, dst.transform, copied);
            // Secondo passaggio: rimappa TUTTI i riferimenti, ora che i bersagli esistono.
            foreach (var c in copied) RemapReferences(c, src.scene, scene);
            return dst;
        }

        // Replica sull'istanza nuova le modifiche di prefab dell'istanza di Level 1
        // (m_Modifications) e i componenti che Level 1 ha rimosso dal prefab.
        private static void CopyPrefabOverrides(GameObject src, GameObject dst, Scene scene)
        {
            var mods = PrefabUtility.GetPropertyModifications(src);
            if (mods != null)
            {
                var list = new List<PropertyModification>(mods.Length);
                foreach (var m in mods)
                {
                    if (m == null || m.target == null) continue;
                    var copy = new PropertyModification { target = m.target, propertyPath = m.propertyPath, value = m.value, objectReference = m.objectReference };
                    // Riferimento a un oggetto di Level 1: rimappa per percorso, altrimenti vuoto.
                    var refGo = copy.objectReference is Component rc ? rc.gameObject : copy.objectReference as GameObject;
                    if (refGo != null && refGo.scene == src.scene)
                    {
                        var target = FindByPath(scene, HierarchyPath(refGo.transform));
                        copy.objectReference = target == null ? null
                            : copy.objectReference is Component ? target.GetComponent(copy.objectReference.GetType()) : target;
                    }
                    list.Add(copy);
                }
                PrefabUtility.SetPropertyModifications(dst, list.ToArray());
            }

            // Componenti rimossi in Level 1 (es. MeshCollider del palmo sinistro).
            foreach (var removed in PrefabUtility.GetRemovedComponents(src))
            {
                if (removed == null || removed.assetComponent == null) continue;
                foreach (var c in dst.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform) continue;
                    if (PrefabUtility.GetCorrespondingObjectFromSource(c) == removed.assetComponent)
                    {
                        UnityEngine.Object.DestroyImmediate(c);
                        break;
                    }
                }
            }
        }

        private static void CopyAddedComponentsRecursive(Transform src, Transform dst, List<Component> copied)
        {
            foreach (var sc in src.GetComponents<Component>())
            {
                if (sc == null || sc is Transform) continue;

                bool fromPrefab = PrefabUtility.GetCorrespondingObjectFromSource(sc) != null;
                if (fromPrefab)
                {
                    // Stesso componente del prefab nella copia: allinea solo lo stato enabled.
                    if (sc is Behaviour sb)
                    {
                        var db = dst.GetComponent(sc.GetType()) as Behaviour;
                        if (db != null && db.enabled != sb.enabled) db.enabled = sb.enabled;
                    }
                    continue;
                }

                var dc = dst.gameObject.AddComponent(sc.GetType());
                if (dc == null) { Debug.LogWarning($"[Level2Setup] Impossibile aggiungere {sc.GetType().Name} su {dst.name}"); continue; }
                EditorUtility.CopySerialized(sc, dc);
                copied.Add(dc);
            }

            // Figli: stessa struttura del prefab, quindi si accoppiano per indice+nome.
            for (int i = 0; i < src.childCount; i++)
            {
                var sChild = src.GetChild(i);
                var dChild = i < dst.childCount && dst.GetChild(i).name == sChild.name ? dst.GetChild(i) : dst.Find(sChild.name);
                if (dChild == null) continue;
                CopyAddedComponentsRecursive(sChild, dChild, copied);
            }
        }

        // --- Singoli componenti copiati da Level 1 ------------------------------------------

        private static bool EnsureCopied<T>(Scene scene, Scene level1, string goName) where T : Component
        {
            if (FindInScene<T>(scene) != null) return false;
            var src = FindInScene<T>(level1);
            if (src == null)
            {
                Debug.LogWarning($"[Level2Setup] {typeof(T).Name} non trovato in Level 1: saltato.");
                return false;
            }
            var go = new GameObject(goName);
            SceneManager.MoveGameObjectToScene(go, scene);
            var dst = go.AddComponent<T>();
            EditorUtility.CopySerialized(src, dst);
            RemapReferences(dst, level1, scene);
            Undo.RegisterCreatedObjectUndo(go, $"Crea {goName} Level2");
            return true;
        }

        private static bool EnsureBranding(Scene scene, Scene level1)
        {
            if (!EnsureCopied<UnibsBranding>(scene, level1, "UnibsBranding")) return false;
            var b = FindInScene<UnibsBranding>(scene);
            var so = new SerializedObject(b);
            so.FindProperty("logoOnTable").boolValue = false; // il tavolo e' occupato dal labirinto
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool EnsureVoice(Scene scene, Scene level1)
        {
            if (!EnsureCopied<VoiceCommandController>(scene, level1, "VoiceControlManager")) return false;
            var vc = FindInScene<VoiceCommandController>(scene);
            var so = new SerializedObject(vc);
            so.FindProperty("levelId").stringValue = "level2_labyrinth";
            so.FindProperty("manager").objectReferenceValue = null; // auto-find del LevelController
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // --- Blocchi propri del livello 2 --------------------------------------------------

        private static bool EnsureMainMenu(Scene scene)
        {
            if (FindInScene<MainMenuManager>(scene) != null) return false;
            var go = NewInScene("MainMenu", scene);
            var menu = go.AddComponent<MainMenuManager>();
            var so = new SerializedObject(menu);
            so.FindProperty("welcomeKey").stringValue = "level2_welcome";
            so.FindProperty("welcomeFallbackClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Voice/level2_welcome.mp3");
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool EnsureLevelFlow(Scene scene)
        {
            if (FindInScene<LevelFlowController>(scene) != null) return false;
            var go = NewInScene("LevelFlow", scene);
            var flow = go.AddComponent<LevelFlowController>();
            var so = new SerializedObject(flow);
            so.FindProperty("nextSceneName").stringValue = "MainMenu";
            so.FindProperty("nextButtonLabel").stringValue = "Torna al menu";
            so.FindProperty("hintKey").stringValue = "level2_next_hint";
            so.FindProperty("confirmKey").stringValue = "menu_back";
            var phrases = so.FindProperty("nextPhrases");
            string[] p = { "menu", "torna al menu", "menu principale", "vai al menu" };
            phrases.arraySize = p.Length;
            for (int i = 0; i < p.Length; i++) phrases.GetArrayElementAtIndex(i).stringValue = p[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool EnsureSessionLogger(Scene scene)
        {
            if (FindInScene<SessionLogger>(scene) != null) return false;
            NewInScene("SessionLogger", scene).AddComponent<SessionLogger>(); // participantId da Inspector prima della sessione
            return true;
        }

        private static bool EnsureGraspDebugPanel(Scene scene)
        {
            if (FindInScene<GraspDebugPanel>(scene) != null) return false;
            NewInScene("GraspDebugPanel", scene).AddComponent<GraspDebugPanel>();
            return true;
        }

        private static bool EnsureLevelManager(Scene scene)
        {
            var existing = FindInScene<LabyrinthManager>(scene);
            bool created = existing == null;
            var go = created ? NewInScene("Level2Manager", scene) : existing.gameObject;
            var manager = created ? go.AddComponent<LabyrinthManager>() : existing;
            var so = new SerializedObject(manager);

            // Muri: i cubi tattili radice di Luca (a y=0.9). I cubi di calibrazione dei
            // tracker stanno sotto ViveTrackerManager e non sono radice: esclusi.
            var walls = new List<Collider>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("Cube")) continue;
                if (root.GetComponent<WeArtTouchableObject>() == null) continue;
                if (Mathf.Abs(root.transform.position.y - 0.9f) > 0.02f) continue;
                var col = root.GetComponent<Collider>();
                if (col != null) walls.Add(col);
            }
            var wallsProp = so.FindProperty("walls");
            if (created || wallsProp.arraySize == 0)
            {
                wallsProp.arraySize = walls.Count;
                for (int i = 0; i < walls.Count; i++) wallsProp.GetArrayElementAtIndex(i).objectReferenceValue = walls[i];
            }

            // Tappe: contenitore "MazeZones" con ingresso / meta / uscita, create solo se
            // mancano; una lista gia' compilata a mano (piu' tappe, posizioni diverse) resta.
            var zonesProp = so.FindProperty("zones");
            var zonesRoot = FindRoot(scene, "MazeZones") ?? NewInScene("MazeZones", scene);
            var zones = new List<MazeZone>();
            foreach (var (name, kind, label, pos) in DefaultZones)
            {
                var t = zonesRoot.transform.Find(name);
                MazeZone z;
                if (t == null)
                {
                    var zgo = new GameObject(name);
                    zgo.transform.SetParent(zonesRoot.transform, false);
                    zgo.transform.position = pos;
                    z = zgo.AddComponent<MazeZone>();
                    var zso = new SerializedObject(z);
                    zso.FindProperty("kind").enumValueIndex = (int)kind;
                    zso.FindProperty("label").stringValue = label;
                    zso.FindProperty("radius").floatValue = 0.06f;
                    zso.ApplyModifiedPropertiesWithoutUndo();
                }
                else z = t.GetComponent<MazeZone>() ?? t.gameObject.AddComponent<MazeZone>();
                zones.Add(z);
            }
            if (created || zonesProp.arraySize == 0)
            {
                zonesProp.arraySize = zones.Count;
                for (int i = 0; i < zones.Count; i++) zonesProp.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            }

            // Suoni (i clip sintetici di Assets/Audio/Level2 + "corretto" di Level 1 come
            // riserva), solo alla creazione: da li' in poi comanda l'Inspector.
            if (created)
            {
                so.FindProperty("beaconClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Level2/beacon_beep.mp3");
                so.FindProperty("wallBumpClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Level2/wall_bump.mp3");
                so.FindProperty("checkpointClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Level2/checkpoint_chime.mp3");
                so.FindProperty("exitClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Level2/exit_fanfare.mp3");
                so.FindProperty("levelCompleteClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Level1/corretto.mp3");
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[Level2Setup] LabyrinthManager: {walls.Count} muri, {zones.Count} tappe{(created ? "" : " (esistente, non toccato)")}.");
            return created;
        }

        private static void DisableTrackerDebugger(Scene scene)
        {
            foreach (var td in FindAllInScene<TrackerDebugger>(scene))
            {
                if (!td.enabled) continue;
                td.enabled = false;
                Debug.Log("[Level2Setup] TrackerDebugger disabilitato (Console pulita).");
            }
        }

        private static void EnsureInBuild()
        {
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path == ScenePath) return;
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes) { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Level2Setup] Labyrinth aggiunta alla Scene List.");
        }

        // --- Riferimenti tra scene ----------------------------------------------------------

        // Dopo CopySerialized i riferimenti puntano ancora agli oggetti di Level 1: li si
        // risolve per PERCORSO nella gerarchia della scena di destinazione (stesso nome).
        private static void RemapReferences(Component target, Scene from, Scene to)
        {
            var so = new SerializedObject(target);
            var it = so.GetIterator();
            bool changed = false;
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var obj = it.objectReferenceValue;
                GameObject srcGo = obj is Component oc ? oc.gameObject : obj as GameObject;
                if (srcGo == null || srcGo.scene != from) continue;

                string path = HierarchyPath(srcGo.transform);
                var dstGo = FindByPath(to, path);
                if (dstGo == null)
                {
                    Debug.LogWarning($"[Level2Setup] {target.GetType().Name}.{it.propertyPath}: '{path}' non esiste nel labirinto, lasciato vuoto.");
                    it.objectReferenceValue = null;
                    changed = true;
                    continue;
                }
                UnityEngine.Object dstObj = obj is Component ? dstGo.GetComponent(obj.GetType()) : dstGo;
                if (dstObj == null)
                {
                    Debug.LogWarning($"[Level2Setup] {target.GetType().Name}.{it.propertyPath}: su '{path}' manca {obj.GetType().Name}, lasciato vuoto.");
                }
                it.objectReferenceValue = dstObj;
                changed = true;
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string HierarchyPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private static GameObject FindByPath(Scene scene, string path)
        {
            var parts = path.Split('/');
            var root = FindRoot(scene, parts[0]);
            if (root == null) return null;
            var t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            return t != null ? t.gameObject : null;
        }

        // --- Util scena --------------------------------------------------------------------

        private static GameObject NewInScene(string name, Scene scene)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, $"Crea {name} Level2");
            return go;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == name) return r;
            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var r in scene.GetRootGameObjects())
            {
                var c = r.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (var r in scene.GetRootGameObjects())
                list.AddRange(r.GetComponentsInChildren<T>(true));
            return list;
        }
    }
}
