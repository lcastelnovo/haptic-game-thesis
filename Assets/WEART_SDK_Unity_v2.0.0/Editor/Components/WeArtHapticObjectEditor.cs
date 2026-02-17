using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;
using WeArt.Core;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtHapticObject"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtHapticObject), true), CanEditMultipleObjects]
    public class WeArtHapticObjectEditor : WeArtComponentEditor
    {
        private WeArtHapticObject Haptic => serializedObject.targetObject as WeArtHapticObject;

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
                var property = serializedObject.FindProperty(nameof(WeArtHapticObject._handSides));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the hand side to which this thimble belongs"
                };
                propertyField.RegisterCallback<ChangeEvent<string>>(evt =>
                {
                    Haptic._handSides = (HandSideFlags)Enum.Parse(typeof(HandSideFlags), evt.previousValue, true);
                    Haptic.HandSides = (HandSideFlags)Enum.Parse(typeof(HandSideFlags), evt.newValue, true);

                });
                editor.Add(propertyField);
            }

            // Actuation point
            {
                var property = serializedObject.FindProperty(nameof(WeArtHapticObject._actuationPoints));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the actuation point of this thimble"
                };
                propertyField.RegisterCallback<ChangeEvent<string>>(evt =>
                {
                    Haptic._actuationPoints = (ActuationPointFlags)Enum.Parse(typeof(ActuationPointFlags), evt.previousValue, true);
                    Haptic.ActuationPoints = (ActuationPointFlags)Enum.Parse(typeof(ActuationPointFlags), evt.newValue, true);
                });
                editor.Add(propertyField);
            }

            // Velocity target
            {
                var property = serializedObject.FindProperty(nameof(WeArtHapticObject._distanceForceTarget));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the transform that will decide the distance for the dynamic force computation"
                };
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Control");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Temperature
            {
                var property = serializedObject.FindProperty(nameof(WeArtHapticObject._temperature));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The temperature applied on the thermal actuator of this thimble"
                };
                propertyField.RegisterCallback<ChangeEvent<Temperature>>(evt =>
                {
                    Haptic._temperature = evt.previousValue;
                    Haptic.Temperature = evt.newValue;
                });
                editor.Add(propertyField);
            }

            // Force
            {
                var property = serializedObject.FindProperty(nameof(WeArtHapticObject._force));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The force applied on the pressure actuator of this thimble"
                };
                propertyField.RegisterCallback<ChangeEvent<Force>>(evt =>
                {
                    Haptic._force = evt.previousValue;
                    Haptic.Force = evt.newValue;
                });
                editor.Add(propertyField);
            }

            // Texture
            {
                var property = serializedObject.FindProperty(nameof(WeArtHapticObject._texture));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The transform used for calculating dynamic force, leaving it empty will remove the dynamic calculation and will give the exact value"
                };
                propertyField.RegisterCallback<ChangeEvent<Texture>>(evt =>
                {
                    Haptic._texture = evt.previousValue;
                    Haptic.Texture = evt.newValue;
                });
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

                // Effects
                {
                    var effectsContainer = new VisualElement();
                    effectsContainer.AddToClassList("propertyRows");
                    editor.Add(effectsContainer);

                    updateEffects();
                    Haptic.OnActiveEffectsUpdate += updateEffects;

                    void updateEffects()
                    {
                        const string effectsLabel = "Effects";
                        effectsContainer.Clear();
                        if (Haptic.ActiveEffect != null)
                        {
                            if (Haptic.ActiveEffect is UnityEngine.Object effectObject)
                            {
                                string label = effectsContainer.childCount == 0 ? effectsLabel : " ";
                                var effectField = createEffectField(label, effectObject);
                                effectsContainer.Add(effectField);
                            }
                        }
                        else
                        {
                            var objectField = createEffectField(effectsLabel, null);
                            effectsContainer.Add(objectField);
                        }
                    }

                    VisualElement createEffectField(string label, UnityEngine.Object effectObject)
                    {
                        var objectField = new ObjectField(label);
                        objectField.AddToClassList("propertyRow");
                        objectField.objectType = typeof(UnityEngine.Object);
                        objectField.Q(className: ObjectField.inputUssClassName).SetEnabled(false);
                        objectField.SetValueWithoutNotify(effectObject);
                        return objectField;
                    }
                }
            }

            return editor;
        }
    }
}