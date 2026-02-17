using System.ComponentModel;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Components;
using WeArt.Core;
using WeArt.Utils;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtChildCollider"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtChildCollider), true), CanEditMultipleObjects]
    public class WeArtChildColliderEditor : WeArtComponentEditor
    {

        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var header = new Label("This component is binding its collider to the parent touchable object");
                header.AddToClassList("header");
                editor.Add(header);
            }

            return editor;
        }
    }
}