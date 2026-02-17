using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    [CustomEditor(typeof(WeArtStatusTracker), true), CanEditMultipleObjects]
    public class WeArtStatusTrackerEditor : WeArtComponentEditor
    {
        private WeArtStatusTracker StatusTracker => serializedObject.targetObject as WeArtStatusTracker;

        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);
            
            {
                var header = new Label("Events");
                header.AddToClassList("header");
                editor.Add(header);
            }
            {
                var property = serializedObject.FindProperty(nameof(WeArtStatusTracker.OnMiddlewareStatus));
                editor.Add(new PropertyField(property)
                {
                    tooltip = "Event invoked when the middleware status or the status of the connected devices changes"
                });
            }

            {
                var property = serializedObject.FindProperty(nameof(WeArtStatusTracker.OnWeartAppStatus));
                editor.Add(new PropertyField(property)
                {
                    tooltip = "Event invoked when the weart application status changes"
                });
            }
            
            {
                var property = serializedObject.FindProperty(nameof(WeArtStatusTracker.OnTouchDiverProStatus));
                editor.Add(new PropertyField(property)
                {
                    tooltip = "Event invoked when the weart application updates TD_Pro status"
                });
            }
            
            return editor;
        }
        
    }
}