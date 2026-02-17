using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// Base class that implements a custom inspector for <see cref="WeArt"/> components
    /// </summary>
    public abstract class WeArtComponentEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = new VisualElement
            {
                name = nameof(WeArtHapticObjectEditor)
            };

            var style = Resources.Load<StyleSheet>("Styles/WeArtStyle");
            editor.styleSheets.Add(style);

            var logo = new Image
            {
                name = "Logo",
                image = Resources.Load<Texture>($"Images/WeArtLogoHeader{(EditorGUIUtility.isProSkin ? "_Dark" : "_Light")}"),
                scaleMode = ScaleMode.ScaleToFit
            };
            editor.Add(logo);

            return editor;
        }
    }
}