using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtWristOrientationObject"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtWristOrientationObject), true), CanEditMultipleObjects]
    public class WeArtWristTrackingObjectEditor : WeArtComponentEditor
    {
        private WeArtWristOrientationObject WristTracking => serializedObject.targetObject as WeArtWristOrientationObject;

        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("Wrist orientation configurator");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Hand side
            {
                var property = serializedObject.FindProperty(nameof(WeArtWristOrientationObject._handSide));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the hand side this wrist belongs"
                };
           
                editor.Add(propertyField);
            }
            
            {
                var property = serializedObject.FindProperty(nameof(WeArtWristOrientationObject._initialRotationTarget));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Set object which initial rotation will be kept for further wrist orientation calculations. If it is not set, the initial rotation will be given from current gameObject"
                };
           
                editor.Add(propertyField);
            }
            
            {
                var property = serializedObject.FindProperty(nameof(WeArtWristOrientationObject._freezeXRotation));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Enable to freeze the x axis rotation"
                };
           
                editor.Add(propertyField);
            }
            
            {
                var property = serializedObject.FindProperty(nameof(WeArtWristOrientationObject._freezeYRotation));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Enable to freeze the y axis rotation"
                };
           
                editor.Add(propertyField);
            }
            
            {
                var property = serializedObject.FindProperty(nameof(WeArtWristOrientationObject._freezeZRotation));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Enable to freeze the z axis rotation"
                };
           
                editor.Add(propertyField);
            }
            
            return editor;
        }
    }
}