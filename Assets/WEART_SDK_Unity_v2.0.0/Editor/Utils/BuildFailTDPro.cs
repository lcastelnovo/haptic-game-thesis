using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
public class BuildFailTDPro : MonoBehaviour
{
    //Make build fail for Android TD Pro
#if PLATFORM_ANDROID
    public class CustomBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        private bool isTDProFound = false;

        public void OnPreprocessBuild(BuildReport report)
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            string currentScene = SceneManager.GetActiveScene().path;

            isTDProFound = false;

            var buildScenes = EditorBuildSettings.scenes;
            List<string> scenePaths = new List<string>();

            foreach (var scene in buildScenes)
            {
                if (scene.enabled)
                {
                    scenePaths.Add(scene.path);
                }
            }

            foreach (var scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                List<WeArtController> controllersInScene = new List<WeArtController>();

                foreach (GameObject rootGameObject in scene.GetRootGameObjects())
                {
                    FindWeArtControllers(rootGameObject,scenePath);
                }
            }

            //bool isConditionValid = GameObject.FindObjectOfType<WeArtController>().DeviceGeneration == DeviceGeneration.TD; // Replace with your condition
            bool isConditionValid = !isTDProFound;

            EditorSceneManager.OpenScene(currentScene, OpenSceneMode.Single);
            if (!isConditionValid)
            {
                throw new BuildFailedException("TD Pro does not support Android build yet.");
            }
        }

        private void FindWeArtControllers(GameObject gameObject, string scenePath)
        {
            var controller = gameObject.GetComponent<WeArtController>();
            if (controller != null)
            {
                if (controller.DeviceGeneration == DeviceGeneration.TD_Pro)
                {
                    isTDProFound = true;
                    Debug.LogError("Build failed: WeArtController from scene " + scenePath + " must not be set to TD Pro.");
                }
            }

            foreach (Transform child in gameObject.transform)
            {
                FindWeArtControllers(child.gameObject, scenePath);
            }
        }
    }
#endif
}
