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
    // Cosa cambia da un livello all'altro nel cablaggio comune.
    public sealed class LevelWiringOptions
    {
        public string LogTag = "[LevelWiring]";   // prefisso dei messaggi in Console
        public string VoiceLevelId;               // levelId del VoiceCommandController
        public string WelcomeKey;                 // battuta di benvenuto in-level (Resources/Voice)
    }

    // Il cablaggio che ogni livello nato da ViveTrackerScene deve avere e che Level 1 ha
    // gia': mani (rig mouse/demo + componenti nostri sulle mani WEART), modalita' demo,
    // branding UniBS, comandi vocali, menu in-level, flusso di fine livello verso il menu,
    // logger di sessione, pannello F1.
    //
    // Estratto da Level3SetupTool quando e' arrivato il secondo livello che ne aveva
    // bisogno (il memory tattile): due copie di queste righe avrebbero dovuto restare
    // allineate a ogni ritocco di Level 1.
    //
    // Level 1 e' la SOURCE OF TRUTH: i componenti vengono COPIATI (valori Inspector
    // compresi) aprendo Level1_ShapeRecognition.unity in additiva e rimappando i
    // riferimenti per percorso. Idempotente: ogni Ensure* aggiunge solo cio' che manca.
    public static class LevelSceneWiring
    {
        public const string Level1Path = "Assets/Scenes/Level1_ShapeRecognition.unity";
        private const string LeftHandPrefab = "Packages/com.weart.sdk/Runtime/Prefabs/WEARTLeftHand.prefab";
        private const string RightHandPrefab = "Packages/com.weart.sdk/Runtime/Prefabs/WEARTRightHand.prefab";

        // I root che "Ricostruisci da Level 1" cancella e ricrea.
        public static readonly string[] RebuildRoots = { "HandManager", "DemoModeManager", "UnibsBranding", "VoiceControlManager" };

        // Ritorna quanti blocchi ha aggiunto. 'ok' diventa false SOLO per un cablaggio
        // fallito (prefab mancanti, copia non riuscita).
        public static int Wire(Scene scene, LevelWiringOptions o, ref bool ok)
        {
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
            try
            {
                if (EnsureHands(scene, level1, o, ref ok)) changes++;
                if (EnsureCopied<HandDemoModeController>(scene, level1, "DemoModeManager", o)) changes++;
                if (EnsureBranding(scene, level1, o)) changes++;
                if (EnsureVoice(scene, level1, o)) changes++;
                if (EnsureMainMenu(scene, o)) changes++;
                if (EnsureLevelFlow(scene)) changes++;
                if (EnsureSessionLogger(scene)) changes++;
                if (EnsureGraspDebugPanel(scene)) changes++;
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(level1, true); // Level 1 NON viene mai salvata da qui
            }
            return changes;
        }

        // --- Mani: prefab WEART sotto HandManager + componenti nostri copiati da Level 1 ----

        private static bool EnsureHands(Scene scene, Scene level1, LevelWiringOptions o, ref bool ok)
        {
            if (FindInScene<HandInputManager>(scene) != null) return false;

            var srcInput = FindInScene<HandInputManager>(level1);
            if (srcInput == null || srcInput.leftHand == null || srcInput.rightHand == null)
            {
                Debug.LogError($"{o.LogTag} In Level 1 manca HandManager/HandInputManager con le due mani: impossibile copiare il rig.");
                ok = false;
                return false;
            }

            var manager = new GameObject(srcInput.gameObject.name);
            SceneManager.MoveGameObjectToScene(manager, scene);
            Undo.RegisterCreatedObjectUndo(manager, $"Crea HandManager {o.LogTag}");

            var left = InstantiateHand(LeftHandPrefab, srcInput.leftHand.gameObject, manager.transform, scene);
            var right = InstantiateHand(RightHandPrefab, srcInput.rightHand.gameObject, manager.transform, scene);

            // HandInputManager sul contenitore, poi rimappa leftHand/rightHand sulle nuove mani.
            var dstInput = manager.AddComponent<HandInputManager>();
            EditorUtility.CopySerialized(srcInput, dstInput);
            RemapReferences(dstInput, level1, scene);

            if (left == null || right == null)
            {
                Debug.LogError($"{o.LogTag} Una delle mani non e' stata creata: controlla i prefab WEART nel pacchetto.");
                ok = false;
            }

            // Nei livelli di esplorazione la scena e' una sandbox, quindi il log di GloveGraspDetector a ogni
            // chiusura potrebbe essere rumore. Lo disabilitiamo.
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
                Debug.LogError($"[LevelWiring] Prefab non trovato: {prefabPath}");
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
                Debug.LogWarning($"[LevelWiring] '{src.name}' in Level 1 non e' un'istanza di {prefabPath} " +
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
                if (dc == null) { Debug.LogWarning($"[LevelWiring] Impossibile aggiungere {sc.GetType().Name} su {dst.name}"); continue; }
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

        private static bool EnsureCopied<T>(Scene scene, Scene level1, string goName, LevelWiringOptions o) where T : Component
        {
            if (FindInScene<T>(scene) != null) return false;
            var src = FindInScene<T>(level1);
            if (src == null)
            {
                Debug.LogWarning($"{o.LogTag} {typeof(T).Name} non trovato in Level 1: saltato.");
                return false;
            }
            var go = new GameObject(goName);
            SceneManager.MoveGameObjectToScene(go, scene);
            var dst = go.AddComponent<T>();
            EditorUtility.CopySerialized(src, dst);
            RemapReferences(dst, level1, scene);
            Undo.RegisterCreatedObjectUndo(go, $"Crea {goName} {o.LogTag}");
            return true;
        }

        private static bool EnsureBranding(Scene scene, Scene level1, LevelWiringOptions o)
        {
            if (!EnsureCopied<UnibsBranding>(scene, level1, "UnibsBranding", o)) return false;
            var b = FindInScene<UnibsBranding>(scene);
            var so = new SerializedObject(b);
            so.FindProperty("logoOnTable").boolValue = false; // il tavolo e' occupato dal contenuto del livello
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool EnsureVoice(Scene scene, Scene level1, LevelWiringOptions o)
        {
            if (!EnsureCopied<VoiceCommandController>(scene, level1, "VoiceControlManager", o)) return false;
            var vc = FindInScene<VoiceCommandController>(scene);
            var so = new SerializedObject(vc);
            so.FindProperty("levelId").stringValue = o.VoiceLevelId;
            so.FindProperty("manager").objectReferenceValue = null; // auto-find del LevelController
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // --- Blocchi propri del Level 3 --------------------------------------------------

        private static bool EnsureMainMenu(Scene scene, LevelWiringOptions o)
        {
            if (FindInScene<MainMenuManager>(scene) != null) return false;
            var go = NewInScene("MainMenu", scene);
            var menu = go.AddComponent<MainMenuManager>();
            var so = new SerializedObject(menu);
            so.FindProperty("welcomeKey").stringValue = o.WelcomeKey;
            so.FindProperty("welcomeFallbackClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Resources/Voice/{o.WelcomeKey}.mp3");
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
            // Niente suggerimento proprio del flusso: a livello finito parla gia' Finish()
            // con level3_end_hint, che dice la stessa cosa. Con una chiave qui, il giorno in
            // cui qualcuno la generasse il partecipante si troverebbe due battute di fine
            // livello quasi identiche una dietro l'altra.
            so.FindProperty("hintKey").stringValue = "";
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

        public static void SetIfEmpty(SerializedObject so, string path, UnityEngine.Object value)
        {
            var p = so.FindProperty(path);
            if (p != null && p.objectReferenceValue == null && value != null) p.objectReferenceValue = value;
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
                    Debug.LogWarning($"[LevelWiring] {target.GetType().Name}.{it.propertyPath}: '{path}' non esiste nella scena di destinazione, lasciato vuoto.");
                    it.objectReferenceValue = null;
                    changed = true;
                    continue;
                }
                UnityEngine.Object dstObj = obj is Component ? dstGo.GetComponent(obj.GetType()) : dstGo;
                if (dstObj == null)
                {
                    Debug.LogWarning($"[LevelWiring] {target.GetType().Name}.{it.propertyPath}: su '{path}' manca {obj.GetType().Name}, lasciato vuoto.");
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

        public static GameObject NewInScene(string name, Scene scene)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, $"Crea {name}");
            return go;
        }

        public static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == name) return r;
            return null;
        }

        public static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var r in scene.GetRootGameObjects())
            {
                var c = r.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }
    }
}
