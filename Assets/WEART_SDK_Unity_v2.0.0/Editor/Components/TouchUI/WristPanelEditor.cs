using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.TouchUI;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WristPanel"/>.
    /// </summary>
    [CustomEditor(typeof(WristPanel), true), CanEditMultipleObjects]
    public class WristPanelEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            
            var header = new Label("Buttons");
            header.AddToClassList("header");
            editor.Add(header);
            
            var property = serializedObject.FindProperty(nameof(WristPanel.bluetoothButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button to Hide/Show Ble Connection Panel."
            });
            
            property = serializedObject.FindProperty(nameof(WristPanel.statusButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button to Hide/Show Middleware Status Panel."
            });
            
            property = serializedObject.FindProperty(nameof(WristPanel.calibrationButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button to Reset Calibration Process."
            });
            
            property = serializedObject.FindProperty(nameof(WristPanel.actuationButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button to Hide/Show Actuation Debug Panel."
            });
            
            header = new Label("Panels");
            header.AddToClassList("header");
            editor.Add(header);
            
            property = serializedObject.FindProperty(nameof(WristPanel.blePanel));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Ble Connection Panel"
            });
            
            property = serializedObject.FindProperty(nameof(WristPanel.statusPanel));
            editor.Add(new PropertyField(property)
            {
                tooltip = "WeArt Status Tracker."
            });
            
            property = serializedObject.FindProperty(nameof(WristPanel.calibrationManager));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Calibration Manager."
            });
            
            property = serializedObject.FindProperty(nameof(WristPanel.actuationPanel));
            editor.Add(new PropertyField(property)
            {
                tooltip = "WeArt Haptic Actuation Panel."
            });
            
            return editor;
        }
    }
    
}