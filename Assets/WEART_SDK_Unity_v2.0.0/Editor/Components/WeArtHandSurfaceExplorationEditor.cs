using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtHandSurfaceExploration"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtHandSurfaceExploration), true), CanEditMultipleObjects]
    public class WeArtSurfaceExplorationEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("Surface Exploration Origins");
                header.AddToClassList("header");
                editor.Add(header);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._thumbOrigin));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the origin for the surface exploration of the thumb"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._indexOrigin));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the origin for the surface exploration of the index"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._middleOrigin));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the origin for the surface exploration for the middle"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._annularOrigin));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the origin for the surface exploration for the annular"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._pinkyOrigin));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the origin for the surface exploration for the pinky"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._palmOrigin));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the origin for the surface exploration for the palm"
                };
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Surface Exploration Offsets");
                header.AddToClassList("header");
                editor.Add(header);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._thumbOffset));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the offset for the surface exploration for the thumb"
                };
                editor.Add(propertyField);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._indexOffset));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the offset of the surface exploration for the index"
                };
                editor.Add(propertyField);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._middleOffset));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the offset of the surface exploration for the middle"
                };
                editor.Add(propertyField);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._annularOffset));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the offset of the surface exploration for the annular"
                };
                editor.Add(propertyField);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._pinkyOffset));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the offset of the surface exploration for the pinky"
                };
                editor.Add(propertyField);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtHandSurfaceExploration._palmOffset));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the offset of the surface exploration for the palm"
                };
                editor.Add(propertyField);
            }

            return editor;
        }
    }
}