using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtTrackingCalibration"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtTrackingCalibration), true), CanEditMultipleObjects]
    public class WeArtTrackingCalibrationEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();

            // OnCalibrationStart
            {
                var property = serializedObject.FindProperty(nameof(WeArtTrackingCalibration._OnCalibrationStart));
                editor.Add(new PropertyField(property)
                {
                    tooltip = "The event related to the finger tracking algorithm when it started"
                });
            }

            // OnCalibrationFinish
            {
                var property = serializedObject.FindProperty(nameof(WeArtTrackingCalibration._OnCalibrationFinish));
                editor.Add(new PropertyField(property)
                {
                    tooltip = "The event related to the finger tracking algorithm when it finished"
                });
            }


            // OnCalibrationResultSuccess
            {
                var property = serializedObject.FindProperty(nameof(WeArtTrackingCalibration._OnCalibrationResultSuccess));
                editor.Add(new PropertyField(property)
                {
                    tooltip = "The event related to the finger tracking algorithm when it finished with success"
                });
            }

            // OnCalibrationResultFail
            {
                var property = serializedObject.FindProperty(nameof(WeArtTrackingCalibration._OnCalibrationResultFail));
                editor.Add(new PropertyField(property)
                {
                    tooltip = "The event related to the finger tracking algorithm when it finished with fail"
                });
            }

            return editor;
        }
    }
}
