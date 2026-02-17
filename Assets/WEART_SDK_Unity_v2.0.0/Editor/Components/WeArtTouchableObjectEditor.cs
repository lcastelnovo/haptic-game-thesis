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
    /// A custom inspector for components of type <see cref="WeArtTouchableObjectEditor"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtTouchableObject), true), CanEditMultipleObjects]
    public class WeArtTouchableObjectEditor : WeArtComponentEditor
    {
        private WeArtTouchableObject Touchable => serializedObject.targetObject as WeArtTouchableObject;

        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("Haptic feeling");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Temperature
            {
                var property = serializedObject.FindProperty(nameof(WeArtTouchableObject._temperature));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "It will trigger a thermic effect on touching haptic objects"
                };
                propertyField.RegisterCallback<ChangeEvent<Temperature>>(evt =>
                {
                    Touchable._temperature = evt.previousValue;
                    Touchable.Temperature = evt.newValue;
                });
                editor.Add(propertyField);
            }

            // Stiffness
            {
                var property = serializedObject.FindProperty(nameof(WeArtTouchableObject._stiffness));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "It will trigger a pressure force effect on touching haptic objects"
                };
                propertyField.RegisterCallback<ChangeEvent<Force>>(evt =>
                {
                    Touchable._stiffness = evt.previousValue;
                    Touchable.Stiffness = evt.newValue;
                });
                editor.Add(propertyField);
            }

            // Texture
            {
                var property = serializedObject.FindProperty(nameof(WeArtTouchableObject._texture));

                var propertyField = new PropertyField(property)
                {
                    tooltip = "It will trigger a haptic texture effect on touching haptic objects"
                };

                propertyField.RegisterCallback<ChangeEvent<Texture>>(evt =>
                {
                    Touchable._texture = evt.previousValue;
                    Touchable.Texture = evt.newValue;
                });
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Interaction");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Graspable
            {
                var property = serializedObject.FindProperty(nameof(WeArtTouchableObject._graspable));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Enable interaction with WEART Hands to manage grasping"
                };
              
                editor.Add(propertyField);
            }

            // Surface Exploration
            {
                var property = serializedObject.FindProperty(nameof(WeArtTouchableObject._surfaceExploration));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Flag the object for the surface exploration"
                };

                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Grasping settings");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Grasping type
            {
                var property = serializedObject.FindProperty(nameof(WeArtTouchableObject._graspingType));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Defines which system will be used for grasping this object"
                };

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

                // Affected haptic objectss
                {
                    var affectedHapticsContainer = new VisualElement();
                    affectedHapticsContainer.AddToClassList("propertyRows");
                    editor.Add(affectedHapticsContainer);

                    updateHaptics();
                    Touchable.OnAffectedHapticObjectsUpdate += updateHaptics;

                    void updateHaptics()
                    {
                        const string hapticsLabel = "Affected haptic objects";
                        affectedHapticsContainer.Clear();
                        if (Touchable.AffectedHapticObjects.Count > 0)
                        {
                            foreach (var haptic in Touchable.AffectedHapticObjects)
                            {
                                string label = affectedHapticsContainer.childCount == 0 ? hapticsLabel : " ";
                                var hapticField = createHapticField(label, haptic);
                                affectedHapticsContainer.Add(hapticField);
                            }
                        }
                        else
                        {
                            var objectField = createHapticField(hapticsLabel, null);
                            affectedHapticsContainer.Add(objectField);
                        }
                    }

                    VisualElement createHapticField(string label, WeArtHapticObject haptic)
                    {
                        var objectField = new ObjectField(label);
                        objectField.AddToClassList("propertyRow");
                        objectField.objectType = typeof(WeArtHapticObject);
                        objectField.Q(className: ObjectField.inputUssClassName).SetEnabled(false);
                        objectField.SetValueWithoutNotify(haptic);
                        return objectField;
                    }
                }
            }

            return editor;
        }
    }
}