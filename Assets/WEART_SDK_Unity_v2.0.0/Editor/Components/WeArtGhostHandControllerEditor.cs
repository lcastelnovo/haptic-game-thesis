using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtGhostHandController"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtGhostHandController), true), CanEditMultipleObjects]
    public class WeArtGhostHandControllerEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("Hand states");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Open hand state
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._openedHandState));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the animation clip containing the open (relaxed) hand pose"
                };
                editor.Add(propertyField);
            }

            // Closed hand state
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._closedHandState));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the animation clip containing the closed hand pose"
                };
                editor.Add(propertyField);
            }

            // Abduction hand state
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._abductionHandState));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the animation clip containing the abduction hand pose"
                };
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Fingers avatar masks");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Fingers avatar masks
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._thumbMask));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the avatar mask for the thumb"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._indexMask));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the avatar mask for the index"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._middleMask));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the avatar mask for the middle"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._ringMask));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the avatar mask for the ring"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._pinkyMask));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the avatar mask for the pinky"
                };
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Thimbles Tracking");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Thimbles Tracking
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._thumbThimbleTracking));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the thimble for the thumb"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._indexThimbleTracking));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the thimble for the index"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._middleThimbleTracking));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the thimble for the middle"
                };
                editor.Add(propertyField);
            }


            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._annularThimbleTracking));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the thimble for the annular"
                };
                editor.Add(propertyField);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtGhostHandController._pinkyThimbleTracking));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the thimble for the pinky"
                };
                editor.Add(propertyField);
            }

            return editor;
        }
    }
}