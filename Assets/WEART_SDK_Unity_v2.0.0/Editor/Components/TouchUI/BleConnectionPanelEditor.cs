using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.TouchUI;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="BleConnectionPanel"/>.
    /// </summary>
    [CustomEditor(typeof(BleConnectionPanel), true), CanEditMultipleObjects]
    public class BleConnectionPanelEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();

            var property = serializedObject.FindProperty(nameof(BleConnectionPanel.touchDiverInfoPrefab));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Prefab for Touch Diver Info panels."
            });
            
            property = serializedObject.FindProperty(nameof(BleConnectionPanel.deviceContainer));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Container where will be instantiated the info panels for each device."
            });

            property = serializedObject.FindProperty(nameof(BleConnectionPanel.scrollUpButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button scrolls the list up."
            });
            
            property = serializedObject.FindProperty(nameof(BleConnectionPanel.scrollDownButton));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Button scrolls the list down."
            });
            
            property = serializedObject.FindProperty(nameof(BleConnectionPanel.scrollBar));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Side scroll bar."
            });
            
            property = serializedObject.FindProperty(nameof(BleConnectionPanel.slidingArea));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Sliding area."
            });
            
            property = serializedObject.FindProperty(nameof(BleConnectionPanel.autoConnectionText));
            editor.Add(new PropertyField(property)
            {
                tooltip = "Auto Connection Text."
            });

            return editor;
        }
    }
    
}