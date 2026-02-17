using UnityEditor;
using UnityEngine.UIElements;
using WeArt.TouchUI;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="TouchToggle"/>.
    /// </summary>
    [CustomEditor(typeof(TouchToggle), true), CanEditMultipleObjects]
    public class TouchToggleEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();

            return editor;
        }
    }
}
