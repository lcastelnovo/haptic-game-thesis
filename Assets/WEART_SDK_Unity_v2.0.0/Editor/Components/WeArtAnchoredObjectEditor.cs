using System.ComponentModel;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;
using WeArt.Core;
using WeArt.Utils;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtAnchoredObject"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtAnchoredObject), true), CanEditMultipleObjects]
    public class WeArtAnchoredObjectEditor : WeArtComponentEditor
    {

        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("Interaction");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Anchored Velocity
            {
                var property = serializedObject.FindProperty(nameof(WeArtAnchoredObject._graspingVelocity));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The amount of force that will be applyed during grasping to the anchored object"
                };

                editor.Add(propertyField);
            }

            // Anchored Velocity
            {
                var property = serializedObject.FindProperty(nameof(WeArtAnchoredObject._isUsingScrewingOrigin));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The amount of force that will be applyed during grasping to the anchored object"
                };

                editor.Add(propertyField);
            }

            return editor;
        }
    }
}