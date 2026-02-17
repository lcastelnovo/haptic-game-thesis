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
    /// A custom inspector for components of type <see cref="WeArtDevice"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtDevice), true), CanEditMultipleObjects]
    public class WeArtDeviceEditor : WeArtComponentEditor
    {
        private WeArtDevice device => serializedObject.targetObject as WeArtDevice;

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

            // Device ID
            {
                var property = serializedObject.FindProperty(nameof(WeArtDevice._deviceID));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select Device ID"
                };
                propertyField.RegisterCallback<ChangeEvent<string>>(evt => device._deviceID = (DeviceID)property.intValue);
                editor.Add(propertyField);
            }

            // Hand side
            {
                var property = serializedObject.FindProperty(nameof(WeArtDevice._handSide));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the hand side this thimble belongs"
                };
                propertyField.RegisterCallback<ChangeEvent<string>>(evt => device._handSide = (HandSide)property.intValue);
                editor.Add(propertyField);
            }

            return editor;
        }
    }
}
