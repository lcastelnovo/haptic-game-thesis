using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.TouchUI;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="TouchDIVERInfo"/>.
    /// </summary>
    [CustomEditor(typeof(TouchDIVERInfo), true), CanEditMultipleObjects]
    public class TouchDIVERInfoEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();

            var property = serializedObject.FindProperty(nameof(TouchDIVERInfo.deviceMacAddress));
            editor.Add(new PropertyField(property)
            {
                tooltip = "TD MAC Address filed."
            });
            
            property = serializedObject.FindProperty(nameof(TouchDIVERInfo.deviceStatus));
            editor.Add(new PropertyField(property)
            {
                tooltip = "TD Status Field."
            });
            
            property = serializedObject.FindProperty(nameof(TouchDIVERInfo.handSideText));
            editor.Add(new PropertyField(property)
            {
                tooltip = "TD Hand Side field."
            });
            
            property = serializedObject.FindProperty(nameof(TouchDIVERInfo.connectButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button to connect with device."
            });
            
            property = serializedObject.FindProperty(nameof(TouchDIVERInfo.disconnectButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button to disconnect device."
            });
            
            property = serializedObject.FindProperty(nameof(TouchDIVERInfo.changeHandButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button to change the hand side."
            });
            
            property = serializedObject.FindProperty(nameof(TouchDIVERInfo.rememberToggle));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Toggle to remember the TD device for auto connection within next sessions."
            });
            
            property = serializedObject.FindProperty(nameof(TouchDIVERInfo.sleepModeNotice));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Text notification that device is in the sleep mode."
            });
            
            return editor;
        }
    }
    
}