using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HapticResearch.Audio;
using HapticResearch.Menu;

namespace HapticResearch.EditorTools
{
    // Genera la scena del MENU PRINCIPALE (Assets/Scenes/MainMenu.unity) e sistema la
    // Scene List della build nell'ordine: MainMenu -> Level1 -> Labyrinth.
    //
    // Il menu e' una scena MINIMALE (camera + luce + controller): niente rig WEART/mani,
    // cosi' la calibrazione del TouchDIVER parte una sola volta, entrando nel livello.
    // NOTA: la vecchia Scene List partiva da old.unity (scena deprecata): viene rimossa
    // dalla lista (il file resta nel progetto).
    //
    // Uso: menu "HapticResearch/Menu principale/Crea scena e Scene List", oppure da CLI:
    //   Unity -batchmode -quit -projectPath . -executeMethod
    //     HapticResearch.EditorTools.MenuSceneSetupTool.ConfigureHeadless
    public static class MenuSceneSetupTool
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string Level1ScenePath = "Assets/Scenes/Level1_ShapeRecognition.unity";
        private const string LabyrinthScenePath = "Assets/Scenes/Labyrinth.unity";

        public static void ConfigureHeadless() => Configure();

        [MenuItem("HapticResearch/Menu principale/Crea scena e Scene List")]
        public static void Configure()
        {
            if (System.IO.File.Exists(MenuScenePath))
            {
                Debug.Log("[MenuSetup] MainMenu.unity esiste gia': aggiorno solo la Scene List.");
            }
            else
            {
                CreateMenuScene();
            }

            UpdateBuildSceneList();
            Debug.Log("[MenuSetup] Fatto: scena menu pronta e Scene List = MainMenu, Level1, Labyrinth.");
        }

        private static void CreateMenuScene()
        {
            // Scena nuova con camera + luce di default (il menu e' solo audio, ma una
            // camera serve comunque per non avere schermo di errore).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("MenuController");
            go.AddComponent<NarrationManager>();
            var controller = go.AddComponent<MainMenuSceneController>();

            // Config livelli via SerializedObject (= compilare l'Inspector a mano).
            var so = new SerializedObject(controller);
            var levels = so.FindProperty("levels");
            levels.arraySize = 2;

            SetLevel(levels.GetArrayElementAtIndex(0), "level1", "Level1_ShapeRecognition", KeyCode.Alpha1,
                new[] { "uno", "livello uno", "forme", "riconoscimento forme", "riconoscimento delle forme" });
            SetLevel(levels.GetArrayElementAtIndex(1), "level2", "Labyrinth", KeyCode.Alpha2,
                new[] { "due", "livello due", "labirinto" });

            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void SetLevel(SerializedProperty entry, string id, string sceneName, KeyCode key, string[] phrases)
        {
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("sceneName").stringValue = sceneName;
            entry.FindPropertyRelative("key").intValue = (int)key;
            var phrasesProp = entry.FindPropertyRelative("phrases");
            phrasesProp.arraySize = phrases.Length;
            for (int i = 0; i < phrases.Length; i++)
                phrasesProp.GetArrayElementAtIndex(i).stringValue = phrases[i];
        }

        private static void UpdateBuildSceneList()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(Level1ScenePath, true),
                new EditorBuildSettingsScene(LabyrinthScenePath, true),
            };
        }
    }
}
