using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using WeArt.Components;
using WeArt.UnityEditor;

/// <summary>
/// A custom inspector for components of type <see cref="WeArtSpawnPoint"/>.
/// </summary>
[CustomEditor(typeof(WeArtSpawnPoint), true), CanEditMultipleObjects]
public class WeArtSpawnPointEditor : WeArtComponentEditor
{
    public override VisualElement CreateInspectorGUI()
    {
        var editor = base.CreateInspectorGUI();
        editor.Bind(serializedObject);
        
        // Label
        var label = new Label("This object is the spawn location of the VR camera rig");
        label.AddToClassList("header");

        editor.Add(label);
        
        return editor;
    }

}
