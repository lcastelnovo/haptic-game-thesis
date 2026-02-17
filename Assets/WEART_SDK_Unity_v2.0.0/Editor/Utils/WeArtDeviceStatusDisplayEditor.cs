using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace WeArt.UnityEditor
{
    [CustomEditor(typeof(WeArtDeviceStatusDisplay), true), CanEditMultipleObjects]
    public class WeArtDeviceStatusDisplayEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // External tracking objects
            {
                var header = new Label("Status Tracking");
                header.AddToClassList("header");
                editor.Add(header);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.tracker));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the game object containing the middleware status tracker"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay._handSide));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Select here the hand side this device belongs"
                };
                editor.Add(propertyField);
            }

            // Thimbles status visualization
            {
                var header = new Label("Hand/Thimbles Status Visualization");
                header.AddToClassList("header");
                editor.Add(header);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.HandImage));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image representing the hand"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.ThumbStatusLight));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image representing the thumb thimble status"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.IndexStatusLight));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image representing the index thimble status"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.MiddleStatusLight));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image representing the middle thimble status"
                };
                editor.Add(propertyField);
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.AnnularStatusLight));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image representing the annular thimble status"
                };
                editor.Add(propertyField);
            }
            
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.PinkyStatusLight));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image representing the pinky thimble status"
                };
                editor.Add(propertyField);
            }
            
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.PalmStatusLight));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image representing the palm thimble status"
                };
                editor.Add(propertyField);
            }
            
            // Status colors
            {
                var header = new Label("Thimbles Status Colors");
                header.AddToClassList("header");
                editor.Add(header);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.OkColor));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Color used when the thimble is connected and ok"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.ErrorColor));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Color used when the thimble is connected but with errors"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.DisconnectedColor));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Color used when the thimble is not connected"
                };
                editor.Add(propertyField);
            }

            // Device information
            {
                var header = new Label("Device Information");
                header.AddToClassList("header");
                editor.Add(header);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.NormalColor));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Color to indicate the normal state of UI elements"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.InfoPanel));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Panel containing the device information"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.MacAddressText));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Text for the device mac address"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.BatteryLevelText));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Text for the device battery level percentage"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.ChargingImage));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image used to show that the device is charging"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.NoBatteryImage));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Image used to show that the device does not have the battery connected"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.BatteryPercentagePanel));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Panel that displays the battery left in percentages"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.NoBatteryPanel));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Panel that displays when the battery is not connected"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.BatteryImage));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The default battery image"
                };
                editor.Add(propertyField);
            }

            // Device calibration status
            {
                var header = new Label("Device Calibration Status");
                header.AddToClassList("header");
                editor.Add(header);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.CalibrationText));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Text for showing when the device is calibrating"
                };
                editor.Add(propertyField);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtDeviceStatusDisplay.CalibrationFadeOutTimeSeconds));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "For how much time to to show the calibration completed text (0 = never)"
                };
                editor.Add(propertyField);
            }

            return editor;
        }
    }
}