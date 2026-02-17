#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using WeArt.Components;
using UnityEditor.SceneManagement;


namespace WeArt.UnityEditor
{
    public class WeArtPrefabMenuItem : Editor
    {
        [MenuItem("WEART/Add Weart Startup Components", false, -1)]
        public static void AddWeartVR()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath("Packages/com.weart.sdk/Runtime/Prefabs/WEART.prefab", typeof(GameObject)) as GameObject;
            PrefabUtility.InstantiatePrefab(prefab);

            EditorUtility.SetDirty(prefab);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("Weart Startup components created succesfully");             
        }        
    }
}
#endif