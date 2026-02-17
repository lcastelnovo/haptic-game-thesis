using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;
using WeArt.Core;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtThimbleTrackingObject"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtThimbleTrackingObject), true), CanEditMultipleObjects]
    public class WeArtThimbleTrackingObjectEditor : WeArtComponentEditor
    {
        private WeArtThimbleTrackingObject ThimbleTracking => serializedObject.targetObject as WeArtThimbleTrackingObject;

        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("Hand configuration");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Hand side
            {
                var property = serializedObject.FindProperty(nameof(WeArtThimbleTrackingObject._handSide));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the hand side this thimble belongs"
                };
                propertyField.RegisterCallback<ChangeEvent<string>>(evt => ThimbleTracking.HandSide = (HandSide)property.intValue);
                editor.Add(propertyField);
            }

            // Actuation point
            {
                var property = serializedObject.FindProperty(nameof(WeArtThimbleTrackingObject._actuationPoint));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the actuation point of this thimble"
                };
                propertyField.RegisterCallback<ChangeEvent<string>>(evt => ThimbleTracking.ActuationPoint = (ActuationPoint)property.intValue);
                editor.Add(propertyField);
            }


            if (EditorApplication.isPlaying)
            {
                // Label
                {
                    var header = new Label("Runtime");
                    header.AddToClassList("header");
                    editor.Add(header);
                }

                // Closure
                {
                    var propertyField = ClosurePropertyDrawer.CreatePropertyGUI(
                        name: ObjectNames.NicifyVariableName(nameof(WeArtThimbleTrackingObject.Closure)),
                        getter: () => ThimbleTracking.Closure.Value
                    );
                    propertyField.tooltip = "The closure amount of this thimble";
                    editor.Add(propertyField);
                }
            }

            return editor;
        }
    }
}