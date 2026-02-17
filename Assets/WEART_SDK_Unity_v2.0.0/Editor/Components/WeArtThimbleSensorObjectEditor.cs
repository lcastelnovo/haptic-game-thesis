using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WeArt.Components;
using WeArt.Core;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtThimbleTrackingObject"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtThimbleSensorObject), true), CanEditMultipleObjects]
    public class WeArtThimbleSensorObjectEditor : WeArtComponentEditor
    {
        private WeArtThimbleSensorObject ThimbleTracking => serializedObject.targetObject as WeArtThimbleSensorObject;

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

                // Accelerometer
                {
                    var propertyField = new Vector3Field("Accelerometer");
                    propertyField.SetEnabled(false);
                    propertyField.RegisterCallback<AttachToPanelEvent>(evt => EditorApplication.update += updateCallback);
                    propertyField.RegisterCallback<DetachFromPanelEvent>(evt => EditorApplication.update -= updateCallback);
                    void updateCallback() => propertyField.SetValueWithoutNotify(ThimbleTracking.Accelerometer);

                    propertyField.tooltip = "The accelerometer values of this thimble";
                    editor.Add(propertyField);
                }

                // Gyroscope
                {
                    var propertyField = new Vector3Field("Gyroscope");
                    propertyField.SetEnabled(false);
                    propertyField.RegisterCallback<AttachToPanelEvent>(evt => EditorApplication.update += updateCallback);
                    propertyField.RegisterCallback<DetachFromPanelEvent>(evt => EditorApplication.update -= updateCallback);
                    void updateCallback() => propertyField.SetValueWithoutNotify(ThimbleTracking.Gyroscope);

                    propertyField.tooltip = "The gyroscope values of this thimble";
                    editor.Add(propertyField);
                }

                // Time Of Flight
                {
                    var container = new VisualElement();
                    container.AddToClassList("propertyRow");

                    // Label
                    var label = new Label("Time of Flight");
                    label.SetEnabled(false);
                    label.AddToClassList("unity-base-field__label");
                    label.AddToClassList("unity-property-field__label");
                    container.Add(label);

                    var valueSlider = new SliderWithInputField(0, 255);
                    valueSlider.SetEnabled(false);
                    valueSlider.SetValueWithoutNotify(255);
                    valueSlider.RegisterCallback<AttachToPanelEvent>(evt => EditorApplication.update += updateCallback);
                    valueSlider.RegisterCallback<DetachFromPanelEvent>(evt => EditorApplication.update -= updateCallback);
                    void updateCallback() => valueSlider.SetValueWithoutNotify(ThimbleTracking.TimeOfFlightDistance);
                    container.Add(valueSlider);

                    container.tooltip = "The Distance recorded of the ToF sensor of this thimble, between 0 and 255";

                    editor.Add(container);
                }
            }

            return editor;
        }
    }
}