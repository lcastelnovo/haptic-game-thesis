using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
            Scene scene;
            MainMenuSceneController controller;

            if (System.IO.File.Exists(MenuScenePath))
            {
                scene = EditorSceneManager.OpenScene(MenuScenePath);
                controller = Object.FindFirstObjectByType<MainMenuSceneController>();
                if (controller == null)
                {
                    Debug.LogWarning("[MenuSetup] MainMenu.unity esiste ma senza MenuController: lo ricreo.");
                    controller = CreateController();
                }
            }
            else
            {
                // Scena nuova con camera + luce di default.
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                controller = CreateController();
            }

            BuildUi(controller);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            AssetDatabase.SaveAssets();
            UpdateBuildSceneList();
            Debug.Log("[MenuSetup] Fatto: scena menu (UI + voce) pronta e Scene List = MainMenu, Level1, Labyrinth.");
        }

        private static MainMenuSceneController CreateController()
        {
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
            return controller;
        }

        // --- UI operatore ---------------------------------------------------------------
        // Pagina visiva per l'operatore VEDENTE: sfondo blu UniBS, logo, titolo, bottoni
        // cliccabili col mouse (stesse azioni di voce/tasti) e crediti. Per il
        // partecipante non vedente non cambia nulla: il percorso principale resta audio.

        private static readonly Color UnibsBlue = new Color32(2, 40, 78, 255);
        private static readonly Color UnibsBlueLight = new Color32(13, 71, 122, 255);

        private static void BuildUi(MainMenuSceneController controller)
        {
            // Ricostruzione pulita: se il canvas esiste gia' (run precedente) lo rifaccio.
            var oldCanvas = GameObject.Find("MenuCanvas");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>(); // input handling "Both": il modulo classico va bene
            }

            var canvasGo = new GameObject("MenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Sfondo pieno blu UniBS.
            var bg = NewRect("Background", canvasGo.transform);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = UnibsBlue;

            // Logo (RawImage: usa il PNG cosi' com'e', senza cambiare l'import a Sprite).
            var logoTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UnibsLogo.png");
            if (logoTex != null)
            {
                var logo = NewRect("Logo", canvasGo.transform);
                TopCenter(logo, -60f, new Vector2(240f, 240f));
                var raw = logo.gameObject.AddComponent<RawImage>();
                raw.texture = logoTex;
            }

            AddText(canvasGo.transform, "Title", "Gioco delle Forme Tattili", 64, FontStyle.Bold,
                Color.white, -350f, new Vector2(1400f, 90f));
            AddText(canvasGo.transform, "Subtitle", "Università degli Studi di Brescia", 32, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.85f), -420f, new Vector2(1400f, 50f));

            // Bottoni livello: stessa azione di voce e tasti (SelectLevelByIndex).
            AddLevelButton(canvasGo.transform, controller, 0, "1. Riconoscimento delle forme", -540f);
            AddLevelButton(canvasGo.transform, controller, 1, "2. Labirinto", -650f);

            // Bottone secondario: ripete l'annuncio vocale delle opzioni.
            var repeatBtn = AddButton(canvasGo.transform, "RepeatButton", "Ripeti annuncio vocale  (R)", 26,
                -755f, new Vector2(520f, 64f), UnibsBlueLight);
            UnityEventTools.AddVoidPersistentListener(repeatBtn.onClick, controller.AnnounceOptions);

            // Suggerimento per l'operatore su cosa puo' dire il partecipante.
            AddText(canvasGo.transform, "VoiceHint",
                "Il partecipante può dire:  «uno», «due» oppure «ripeti»", 24,
                FontStyle.Italic, new Color(1f, 1f, 1f, 0.7f), -840f, new Vector2(1400f, 40f));

            // Crediti in fondo alla pagina.
            var credits = NewRect("Credits", canvasGo.transform);
            credits.anchorMin = new Vector2(0.5f, 0f);
            credits.anchorMax = new Vector2(0.5f, 0f);
            credits.pivot = new Vector2(0.5f, 0f);
            credits.anchoredPosition = new Vector2(0f, 30f);
            credits.sizeDelta = new Vector2(1600f, 110f);
            var creditsText = credits.gameObject.AddComponent<Text>();
            SetupText(creditsText,
                "Progetto di tesi - Laboratorio UniBS\n" +
                "Supervisione: Prof.ssa Anna Richelli - Ricerca: Lorenzo Ghiro\n" +
                "Sviluppo: Luca Castelnovo (tesista) - Simone Saleri (stagista)",
                22, FontStyle.Normal, new Color(1f, 1f, 1f, 0.6f));
        }

        private static void AddLevelButton(Transform parent, MainMenuSceneController controller, int index, string label, float y)
        {
            var btn = AddButton(parent, $"LevelButton{index + 1}", label, 36, y, new Vector2(720f, 88f), Color.white);
            UnityEventTools.AddIntPersistentListener(btn.onClick, controller.SelectLevelByIndex, index);
        }

        private static Button AddButton(Transform parent, string name, string label, int fontSize, float y, Vector2 size, Color bgColor)
        {
            var rect = NewRect(name, parent);
            TopCenter(rect, y, size);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = bgColor;

            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.92f, 1f);
            colors.pressedColor = new Color(0.7f, 0.85f, 1f);
            button.colors = colors;

            var textRect = NewRect("Label", rect);
            Stretch(textRect);
            var text = textRect.gameObject.AddComponent<Text>();
            // Testo blu su bottone chiaro, bianco su bottone scuro.
            bool darkBg = bgColor.r + bgColor.g + bgColor.b < 1.5f;
            SetupText(text, label, fontSize, FontStyle.Bold, darkBg ? Color.white : UnibsBlue);
            return button;
        }

        private static void AddText(Transform parent, string name, string content, int fontSize, FontStyle style, Color color, float y, Vector2 size)
        {
            var rect = NewRect(name, parent);
            TopCenter(rect, y, size);
            SetupText(rect.gameObject.AddComponent<Text>(), content, fontSize, style, color);
        }

        private static void SetupText(Text text, string content, int fontSize, FontStyle style, Color color)
        {
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        // Ancoraggio in alto al centro, y negativa scende (coordinate riferite a 1920x1080).
        private static void TopCenter(RectTransform rect, float y, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
