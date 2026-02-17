using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtHandGraspingSysyem"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtVolumeEffects), true), CanEditMultipleObjects]
    public class WeArtVolumeEffectsEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Override Effects
            {
                var header = new Label("Override the effects of touchable objects inside this volume");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Override Temperature
            {
                var property = serializedObject.FindProperty(nameof(WeArtVolumeEffects._overrideTemperature));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Defines if the touchable objects that enter this object will receive its temperature"
                };
                editor.Add(propertyField);
            }

            // Override Force
            {
                var property = serializedObject.FindProperty(nameof(WeArtVolumeEffects._overrideForce));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Defines if the touchable objects that enter this object will receive its force"
                };
                editor.Add(propertyField);
            }

            // Override Texture
            {
                var property = serializedObject.FindProperty(nameof(WeArtVolumeEffects._overrideTexture));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Defines if the touchable objects that enter this object will receive its texture"
                };
                editor.Add(propertyField);
            }

            // Label Volume
            {
                var header = new Label("Ignore behaviour");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Ignored touchable objects
            {
                var property = serializedObject.FindProperty(nameof(WeArtVolumeEffects._ignoreTouchableObjects));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select the objects that need to be ignored by this volume"
                };
                editor.Add(propertyField);
            }

            return editor;
        }
    }
}