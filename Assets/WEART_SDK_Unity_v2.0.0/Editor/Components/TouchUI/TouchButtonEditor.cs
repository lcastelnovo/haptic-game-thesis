using UnityEditor;
using UnityEngine.UIElements;
using WeArt.TouchUI;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="TouchButton"/>.
    /// </summary>
    [CustomEditor(typeof(TouchButton), true), CanEditMultipleObjects]
    public class TouchButtonEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();

            return editor;
        }
    }
}
