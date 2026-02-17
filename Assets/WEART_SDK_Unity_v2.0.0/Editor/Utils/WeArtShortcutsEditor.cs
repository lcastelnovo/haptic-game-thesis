using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Utils;

namespace WeArt.UnityEditor
{
    [CustomEditor(typeof(WeArtShortcuts), true), CanEditMultipleObjects]
    public class WeArtShortcutsEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("Key Shortcuts");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Properties
            {
                var property = serializedObject.FindProperty(nameof(WeArtShortcuts.resetCalibration));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Set the key to call the calibration reset in runtime."
                };
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Components");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Properties
            {
                var property = serializedObject.FindProperty(nameof(WeArtShortcuts.calibrationManager));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "(Optional) Assign the Calibration Manager component if it is at the scene."
                };
                editor.Add(propertyField);
            }
            
            
            return editor;
        }
    }
}