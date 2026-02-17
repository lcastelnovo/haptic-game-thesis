using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using WeArt.Components;
using WeArt.UnityEditor;

/// <summary>
/// A custom inspector for components of type <see cref="WeArtSceneManager"/>.
/// </summary>
[CustomEditor(typeof(WeArtSceneManager), true), CanEditMultipleObjects]
public class WeArtSceneManagerEditor : WeArtComponentEditor
{
    public override VisualElement CreateInspectorGUI()
    {
        var editor = base.CreateInspectorGUI();
        editor.Bind(serializedObject);

        // Label
        var label = new Label("Properties");
        label.AddToClassList("header");

        // Prefab Root
        var mainPrefabTransformProperty = serializedObject.FindProperty(nameof(WeArtSceneManager._mainPrefabTransform));
        var mainPrefabTransformField = new PropertyField(mainPrefabTransformProperty)
        {
            tooltip = "This camera rig will be used to teleport the player when a new scene is loaded"
        };

        // Camera rig
        var cameraRigProperty = serializedObject.FindProperty(nameof(WeArtSceneManager._cameraRigTransform));
        var cameraRigField = new PropertyField(cameraRigProperty)
        {
            tooltip = "This camera rig will be used to teleport the player when a new scene is loaded"
        };

        editor.Add(label);
        editor.Add(mainPrefabTransformField);
        editor.Add(cameraRigField);

        return editor;
    }

}
