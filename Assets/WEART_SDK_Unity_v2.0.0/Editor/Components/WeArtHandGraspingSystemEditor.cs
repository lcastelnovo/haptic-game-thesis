using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtHandGraspingSysyem"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtHandGraspingSystem), true), CanEditMultipleObjects]
    public class WeArtHandGraspingSystemEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Grasper
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandGraspingSystem._grasper));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Trasform position Grasper"
                };
                editor.Add(propertyField);
            }

            // Grasper
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandGraspingSystem._snapGraspProximityTransform));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Trasform for snap grasp proximity checking"
                };
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Proximity coliders");
                header.AddToClassList("header");
                editor.Add(header);
            }

            //  Proximity coliders
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandGraspingSystem._thumbProximityColider));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the proximity collider for the thumb"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandGraspingSystem._indexProximityColider));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the proximity collider for the index"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandGraspingSystem._middleProximityColider));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the proximity collider for the middle"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandGraspingSystem._annularProximityColider));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the proximity collider for the annular"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtHandGraspingSystem._pinkyProximityColider));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the proximity collider for the pinky"
                };
                editor.Add(propertyField);
            }

            return editor;
        }
    }
}