using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WeArt.TouchUI;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="StartPanel"/>.
    /// </summary>
    [CustomEditor(typeof(StartPanel), true), CanEditMultipleObjects]
    public class StartPanelEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();

            var mainHeader = new Label("COMMON COMPONENTS");
            mainHeader.AddToClassList("main-header");
            mainHeader.style.color = Color.black;
            mainHeader.style.fontSize = 12;
            mainHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            mainHeader.style.unityTextAlign = TextAnchor.MiddleCenter;

            mainHeader.style.marginTop = 15;

            if (ColorUtility.TryParseHtmlString("#CBFF00", out Color headerColor))
            {
                mainHeader.style.backgroundColor = new StyleColor(headerColor);
            }
            else
            {
                Debug.LogError("Failed to parse color.");
            }

            editor.Add(mainHeader);

            var header = new Label("Wrist Panel");
            header.AddToClassList("header");
            editor.Add(header);

            var property = serializedObject.FindProperty(nameof(StartPanel.wristPanel));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Wrist Panel."
            });

            header = new Label("Panels to show at start");
            header.AddToClassList("header");
            editor.Add(header);
            
            
            
            property = serializedObject.FindProperty(nameof(StartPanel.StatusDisplay));
            editor.Add(new PropertyField(property)
            {
                tooltip = "When enabled it shows WeArt Middleware Status Display at start of session."
            });
            
            property = serializedObject.FindProperty(nameof(StartPanel.ActuationPanel));
            editor.Add(new PropertyField(property)
            {
                tooltip = "When enabled it shows We Art Actuation Debug Panel at start of session."
            });
            
            property = serializedObject.FindProperty(nameof(StartPanel.WristPanel));
            editor.Add(new PropertyField(property)
            {
                tooltip = "When enabled it shows We Art Wrist Panel within session."
            });
            

            var mainHeaderStandalone = new Label("STANDALONE COMPONENTS");
            mainHeaderStandalone.AddToClassList("main-header");
            mainHeaderStandalone.style.color = Color.black;
            mainHeaderStandalone.style.fontSize = 12;
            mainHeaderStandalone.style.unityFontStyleAndWeight = FontStyle.Bold;
            mainHeaderStandalone.style.unityTextAlign = TextAnchor.MiddleCenter;

            mainHeaderStandalone.style.marginTop = 15;

            if (ColorUtility.TryParseHtmlString("#CBFF00", out Color headerColorStandaloneComp))
            {
                mainHeaderStandalone.style.backgroundColor = new StyleColor(headerColorStandaloneComp);
            }
            else
            {
                Debug.LogError("Failed to parse color.");
            }

            editor.Add(mainHeaderStandalone);

            var headerStandalone = new Label("Panels to show at start");
            headerStandalone.AddToClassList("header");
            editor.Add(headerStandalone);

            property = serializedObject.FindProperty(nameof(StartPanel.BluetoothPanel));
            editor.Add(new PropertyField(property)
            {
                tooltip = "When enabled it shows WeArt Bluetooth Connection Panel at start of session."
            });

            return editor;
        }
    }
    
}