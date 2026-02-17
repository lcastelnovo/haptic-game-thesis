#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using WeArt.Components;


namespace WeArt.UnityEditor
{
    public class WeArtTouchableObjectMenuItem : Editor
    {
        [MenuItem("WEART/Current object to TouchableObject", false, 0)]
        public static void CurrentObjectToHaptic()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("Operaton Failed", "Select a gameobject in the scene that you want to convert to Touchable Object", "Ok");
                return;
            }

            for (int i = 0; i < Selection.gameObjects.Length; i++)
            {
                GameObject currentObj = Selection.gameObjects[i];

                if (currentObj.GetComponent<WeArt.Components.WeArtTouchableObject>() == null)
                {
                    currentObj.AddComponent<WeArt.Components.WeArtTouchableObject>().Graspable = true;
                }

                if (currentObj.GetComponent<Collider>() == null)
                {
                    currentObj.AddComponent<MeshCollider>().convex = true;
                }

                if (currentObj.GetComponent<Rigidbody>() == null)
                {
                    currentObj.AddComponent<Rigidbody>();
                }

                EditorUtility.SetDirty(currentObj);
            }
            //currentObj.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePosition;
            //currentObj.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation;
            //currentObj.GetComponent<Rigidbody>().isKinematic = true;
            //currentObj.GetComponent<Rigidbody>().useGravity = false;
            EditorUtility.DisplayDialog("WeArt. Operation Succesfully", "The object has been converted to Touchable Object succesfully", "Ok");
        }
        
    }
}
#endif