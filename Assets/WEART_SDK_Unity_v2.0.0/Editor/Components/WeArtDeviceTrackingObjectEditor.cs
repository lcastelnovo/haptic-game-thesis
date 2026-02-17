using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WeArt.Components;
using WeArt.Core;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtDeviceTrackingObject"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtDeviceTrackingObject), true), CanEditMultipleObjects]
    public class WeArtDeviceTrackingObjectEditor : WeArtComponentEditor
    {
        private WeArtDeviceTrackingObject DeviceTrackingObject => serializedObject.targetObject as WeArtDeviceTrackingObject;

        public Dictionary<string, (Vector3, Vector3)> _offsetPresets = new Dictionary<string, (Vector3, Vector3)>()
        {
           { "Custom", (
                Vector3.zero,
                Vector3.zero
            )},
            { "TD/HTC/XR Elite Controller/Right", (
                new Vector3(0.05f, -0.055f, -0.0f),
                new Vector3(114.7f, -148.0f, -59.6f)
            )},
            { "TD/HTC/XR Elite Controller/Left", (
                new Vector3(-0.05f, 0.055f, -0.01f),
                new Vector3(67.4f, 0.31f, -74.5f)
            )},
            { "TD/HTC/Wrist tracker/Right", (
                new Vector3(-0.05f, -0.04f, -0.09f),
                new Vector3(16.4f, -105, -11.98f)
            )},
            { "TD/HTC/Wrist tracker/Left", (
                new Vector3(0.06f, -0.04f, -0.09f),
                new Vector3(8.96f, 103.3f, 2.23f)
            )},
            { "TD/Meta Quest/OculusXR/Right", (
                new Vector3(0.09f,-0.07f, -0.02f),
                new Vector3(149f, 193.5f, -96.2f)
            )},
            { "TD/Meta Quest/OculusXR/Left", (
                new Vector3(-0.07f, -0.06f, -0.02f),
                new Vector3(30f, -2.1f, -82.1f)
            )},
            { "TD/Meta Quest/OpenXR/Right", (
                new Vector3(0.04f, -0.08f, -0.02f),
                new Vector3(72.2f, 244.4f, -30.6f)
            )},
            { "TD/Meta Quest/OpenXR/Left", (
                new Vector3(-0.03f, -0.07f, 0.0f),
                new Vector3(106.8f, -40.97f, -121.9f)
            )},
            { "TD/PicoXR/Right", (
                new Vector3(0.04f, -0.08f, -0.02f),
                new Vector3(100.1f, 191.5f, -89.0f)
            )},
            { "TD/PicoXR/Left", (
                new Vector3(-0.03f, -0.07f, 0.0f),
                new Vector3(73.8f, -8.4f, -81.8f)
            )},
             { "TD/SteamVR/Quest/Left", (
                new Vector3(-0.04f, -0.07f, -0.08f),
                new Vector3(64.1f, 27.8f, -67.5f)
            )},
            { "TD/SteamVR/Quest/Right", (
                new Vector3(0.06f, -0.06f, -0.08f),
                new Vector3(62.3f, -5.65f, -274.4f)
            )},
            { "TDPro/SteamVR/Quest/Left", (
                new Vector3(-0.04f, -0.1f, -0.07f),
                new Vector3(61.8f, 20.9f, -60.8f)
            )},
            { "TDPro/SteamVR/Quest/Right", (
                new Vector3(0.06f, -0.105f, -0.1f),
                new Vector3(70.6f, -4.81f, -274.4f)
            )},
            { "TDPro/SteamVR/HTC-Wrist/Left", (
                new Vector3(0.06f, -0.04f, -0.04f),
                new Vector3(6.3f, 24.43f, 25.5f)
            )},
            { "TDPro/SteamVR/HTC-Wrist/Right", (
                new Vector3(-0.06f, -0.04f, -0.04f),
                new Vector3(6.3f, -24.43f, -25.5f)
            )},
            { "TDPro/Meta Quest/OpenXR/Right", (
                new Vector3(0.04f, -0.08f, -0.02f),
                new Vector3(72.2f, 244.4f, -30.6f)
            )},
            { "TDPro/Meta Quest/OpenXR/Left", (
                new Vector3(-0.03f, -0.07f, 0.0f),
                new Vector3(106.8f, -40.97f, -121.9f)
            )},
            { "TDPro/Meta Quest/OculusXR/Right", (
                new Vector3(0.09f,-0.07f, -0.020001f),
                new Vector3(149f, 193.5f, -96.2f)
            )},
            { "TDPro/Meta Quest/OculusXR/Left", (
                new Vector3(-0.07f, -0.06f, -0.020001f),
                new Vector3(30f, -2.1f, -82.1f)
            )}
        };



        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            var offsetHeader = new Label("Offset");
            offsetHeader.AddToClassList("header");

            // Position offset
            var posOffsetProp = serializedObject.FindProperty(nameof(WeArtDeviceTrackingObject._positionOffset));
            var posOffsetPropField = new PropertyField(posOffsetProp);

            // Rotation offset
            var rotOffsetProp = serializedObject.FindProperty(nameof(WeArtDeviceTrackingObject._rotationOffset));
            var rotOffsetPropField = new PropertyField(rotOffsetProp);

            // Offset preset
            var presetsKeys = _offsetPresets.Keys.ToList();
            int selectedIndex = 0;
            for (int i = 0; i < presetsKeys.Count; i++)
            {
                var preset = _offsetPresets[presetsKeys[i]];
                if (Approximately(preset.Item1, posOffsetProp.vector3Value) && Approximately(preset.Item2, rotOffsetProp.vector3Value))
                    selectedIndex = i;
            }
            var presetSelector = new PopupField<string>("Offset presets", presetsKeys, selectedIndex);


            onPresetChange();
            presetSelector.RegisterCallback<ChangeEvent<string>>(evt => onPresetChange());

            void onPresetChange()
            {
                bool isCustom = presetSelector.index == 0;

                posOffsetPropField.SetEnabled(isCustom);
                rotOffsetPropField.SetEnabled(isCustom);

                if (!isCustom)
                {
                    var preset = _offsetPresets[presetSelector.value];
                    posOffsetProp.vector3Value = preset.Item1;
                    rotOffsetProp.vector3Value = preset.Item2;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            // Label
            var trackingHeader = new Label("Tracking");
            trackingHeader.AddToClassList("header");

            // Update method
            var updateProperty = serializedObject.FindProperty(nameof(WeArtDeviceTrackingObject._updateMethod));
            var updatePropertyField = new PropertyField(updateProperty)
            {
                tooltip = "The method to use in order to update the position and the rotation of this device"
            };

            // Tracking Source
            var propertyTrackingSource = serializedObject.FindProperty(nameof(WeArtDeviceTrackingObject._trackingSource));
            var propertyFieldTrackingSource = new PropertyField(propertyTrackingSource)
            {
                tooltip = "Set the tracking source to follow"
            };

            // Show Ghost Hand
            var propertyShowGhostHand = serializedObject.FindProperty(nameof(WeArtDeviceTrackingObject._showGhostHand));
            var propertyFieldShowGhostHand = new PropertyField(propertyShowGhostHand)
            {
                tooltip = "Set if you want the ghost hand to appear when far from the physical hand"
            };

            // Show Ghost Hand
            var propertyGhostHandShowDistance = serializedObject.FindProperty(nameof(WeArtDeviceTrackingObject._ghostHandShowDistance));
            var propertyFieldGhostHandShowDistance = new PropertyField(propertyGhostHandShowDistance)
            {
                tooltip = "Set the distance that makes the ghost hand to appear"
            };

            // Show Ghost Hand
            var propertyHandOffset = serializedObject.FindProperty(nameof(WeArtDeviceTrackingObject._handAnchoredOffsetOrigin));
            var propertyFieldHandOffset = new PropertyField(propertyHandOffset)
            {
                tooltip = "Sets the physical hand's offset origin for the screwing interaction"
            };

            // Show Ghost Hand
            var propertyGhostHandOffset = serializedObject.FindProperty(nameof(WeArtDeviceTrackingObject._ghostHandAnchoredOffsetOrigin));
            var propertyFieldGhostHandOffset = new PropertyField(propertyGhostHandOffset)
            {
                tooltip = "Sets the ghost hand's offset origin for the screwing interaction"
            };
            
            editor.Add(trackingHeader);
            editor.Add(updatePropertyField);

            editor.Add(offsetHeader);
            editor.Add(posOffsetPropField);
            editor.Add(rotOffsetPropField);
            editor.Add(presetSelector);

            editor.Add(propertyFieldTrackingSource);
            editor.Add(propertyFieldShowGhostHand);
            editor.Add(propertyFieldGhostHandShowDistance);

            editor.Add(propertyFieldHandOffset);
            editor.Add(propertyFieldGhostHandOffset);

            return editor;
        }

        private static bool Approximately(Vector3 v1, Vector3 v2)
        {
            return Mathf.Approximately(v1.x, v2.x) &&
                Mathf.Approximately(v1.y, v2.y) &&
                Mathf.Approximately(v1.z, v2.z);
        }
    }
}