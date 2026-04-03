using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using WeArt.Components;
using WeArt.Core;

/// <summary>
/// Editor utility per passare dal sistema custom al sistema nativo WEART.
/// Eseguire da menu: WEART > Configura Sistema Nativo
/// </summary>
public static class WeArtNativeSetup
{
    [MenuItem("WEART/Configura Sistema Nativo")]
    public static void ConfigureNativeSystem()
    {
        Undo.SetCurrentGroupName("Configura Sistema Nativo WEART");
        int undoGroup = Undo.GetCurrentGroup();

        ConfigureHands();
        ConfigureWeArtController();
        ConfigurePrefabs();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[WeArtNativeSetup] Configurazione completata. Salvare la scena (Ctrl+S).");
    }

    static void ConfigureHands()
    {
        var handControllers = Object.FindObjectsByType<WeArtHandController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (handControllers.Length == 0)
        {
            Debug.LogWarning("[WeArtNativeSetup] Nessun WeArtHandController trovato in scena!");
            return;
        }

        foreach (var hc in handControllers)
        {
            GameObject handRoot = hc.gameObject;
            Debug.Log($"[WeArtNativeSetup] Configurazione mano: {handRoot.name}");

            // --- ABILITARE ---
            SetEnabled(hc, true, "WeArtHandController");

            var graspSystem = handRoot.GetComponent<WeArtHandGraspingSystem>();
            if (graspSystem != null)
                SetEnabled(graspSystem, true, "WeArtHandGraspingSystem");
            else
                Debug.LogWarning($"[WeArtNativeSetup] WeArtHandGraspingSystem non trovato su {handRoot.name}");

            // --- DISABILITARE ---

            // WeArtDeviceTrackingObject (posizione via mouse, non da device)
            var deviceTracking = handRoot.GetComponent<WeArtDeviceTrackingObject>();
            if (deviceTracking != null)
                SetEnabled(deviceTracking, false, "WeArtDeviceTrackingObject");

            // Script custom da disabilitare
            DisableAllInChildren<HandCloseController>(handRoot);
            DisableAllInChildren<FingerController>(handRoot);
            DisableAllInChildren<ThumbController>(handRoot);
            DisableAllInChildren<HandGrabController>(handRoot);
            DisableAllInChildren<HandColliderPart>(handRoot);
            DisableAllInChildren<HandCollisionController>(handRoot);
            DisableAllInChildren<HapticFingerSetup>(handRoot);
            DisableAllInChildren<WeArtTrackingSetup>(handRoot);
            DisableAllInChildren<WeArtHapticBridge>(handRoot);

            // GloveGrabController (potrebbe non esistere nel progetto)
            DisableByTypeName(handRoot, "GloveGrabController");
        }
    }

    static void ConfigureWeArtController()
    {
        var controller = Object.FindFirstObjectByType<WeArtController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogWarning("[WeArtNativeSetup] WeArtController non trovato in scena!");
            return;
        }

        // Uso SerializedObject per accedere ai campi internal del SDK
        var so = new SerializedObject(controller);
        so.FindProperty("_startAutomatically").boolValue = true;
        so.FindProperty("_startCalibrationAutomatically").boolValue = true;
        so.FindProperty("_useExternalGraspSystem").boolValue = true;
        so.ApplyModifiedProperties();

        Debug.Log("[WeArtNativeSetup] WeArtController: startAutomatically=true, startCalibrationAutomatically=true, useExternalGraspSystem=true");
    }

    static void ConfigurePrefabs()
    {
        string[] prefabPaths = {
            "Assets/Prefabs/Cube.prefab",
            "Assets/Prefabs/Cylinder.prefab",
            "Assets/Prefabs/Prism.prefab",
        };

        var configs = new (string name, float temperature, float stiffness)[] {
            ("Cube",     0.2f, 0.7f),
            ("Cylinder", 0.5f, 0.5f),
            ("Prism",    0.8f, 0.3f),
        };

        for (int i = 0; i < prefabPaths.Length; i++)
        {
            string path = prefabPaths[i];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[WeArtNativeSetup] Prefab non trovato: {path}");
                continue;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(path);

            var touchable = prefabRoot.GetComponent<WeArtTouchableObject>();
            if (touchable == null)
            {
                touchable = prefabRoot.AddComponent<WeArtTouchableObject>();
                Debug.Log($"[WeArtNativeSetup] Aggiunto WeArtTouchableObject a {path}");
            }

            // Uso SerializedObject per accedere ai campi internal del SDK
            var (name, temp, stiffness) = configs[i];
            var so = new SerializedObject(touchable);

            // Temperatura
            var tempProp = so.FindProperty("_temperature");
            tempProp.FindPropertyRelative("_value").floatValue = temp;
            tempProp.FindPropertyRelative("_active").boolValue = true;

            // Forza (stiffness)
            var forceProp = so.FindProperty("_stiffness");
            forceProp.FindPropertyRelative("_value").floatValue = stiffness;
            forceProp.FindPropertyRelative("_active").boolValue = true;

            // Graspable
            so.FindProperty("_graspable").boolValue = true;
            so.FindProperty("_graspingType").enumValueIndex = 0; // Physical = 0

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[WeArtNativeSetup] {name}: temperature={temp}, stiffness={stiffness}, graspable=true, graspingType=Physical");

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // Configura anche oggetti WeArtTouchableObject gia' presenti in scena
        var sceneObjects = Object.FindObjectsByType<WeArtTouchableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var touchable in sceneObjects)
        {
            var so = new SerializedObject(touchable);
            so.FindProperty("_graspable").boolValue = true;
            so.FindProperty("_graspingType").enumValueIndex = 0; // Physical
            so.ApplyModifiedProperties();
            Debug.Log($"[WeArtNativeSetup] Oggetto in scena '{touchable.gameObject.name}': graspable=true");
        }
    }

    static void SetEnabled(Behaviour component, bool enabled, string label)
    {
        if (component.enabled != enabled)
        {
            Undo.RecordObject(component, $"{(enabled ? "Abilita" : "Disabilita")} {label}");
            component.enabled = enabled;
            EditorUtility.SetDirty(component);
            Debug.Log($"[WeArtNativeSetup] {label} -> enabled={enabled}");
        }
    }

    static void DisableAllInChildren<T>(GameObject root) where T : Behaviour
    {
        var components = root.GetComponentsInChildren<T>(true);
        int count = 0;
        foreach (var c in components)
        {
            if (c.enabled)
            {
                Undo.RecordObject(c, $"Disabilita {typeof(T).Name}");
                c.enabled = false;
                EditorUtility.SetDirty(c);
                count++;
            }
        }
        if (count > 0)
            Debug.Log($"[WeArtNativeSetup] Disabilitati {count}x {typeof(T).Name}");
    }

    static void DisableByTypeName(GameObject root, string typeName)
    {
        var allBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in allBehaviours)
        {
            if (mb != null && mb.GetType().Name == typeName && mb.enabled)
            {
                Undo.RecordObject(mb, $"Disabilita {typeName}");
                mb.enabled = false;
                EditorUtility.SetDirty(mb);
                Debug.Log($"[WeArtNativeSetup] Disabilitato {typeName}");
            }
        }
    }
}
