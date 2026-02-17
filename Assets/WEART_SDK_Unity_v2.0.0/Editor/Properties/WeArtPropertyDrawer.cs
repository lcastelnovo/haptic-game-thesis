using UnityEditor;
using UnityEngine;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// Base class that implements a custom inspector drawer for <see cref="WeArt"/> properties
    /// </summary>
    public abstract class WeArtPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property);
        }
    }
}