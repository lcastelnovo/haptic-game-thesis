using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Utils;

namespace WeArt.UnityEditor
{
    [CustomEditor(typeof(WeArtPlayerController), true), CanEditMultipleObjects]
    public class WeArtPlayerControllerEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("Player Controller");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Properties
            {
                var property = serializedObject.FindProperty(nameof(WeArtPlayerController.speed));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Set the horizontal movement speed value."
                };
                editor.Add(propertyField);
            }

            // Properties
            {
                var property = serializedObject.FindProperty(nameof(WeArtPlayerController.stepPosY));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Set the vertical movement speed value."
                };
                editor.Add(propertyField);
            }
           
            // Properties
            {
                var property = serializedObject.FindProperty(nameof(WeArtPlayerController.cameraTransform));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "(Optional) Set camera view object. If null it will take the active camera at scene"
                };
                editor.Add(propertyField);
            }

            return editor;
        }
    }
}