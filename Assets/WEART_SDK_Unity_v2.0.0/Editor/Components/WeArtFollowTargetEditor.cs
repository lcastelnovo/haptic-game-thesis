using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WeArt.Components;
using WeArt.Core;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtFollowTarget"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtFollowTarget), true), CanEditMultipleObjects]
    public class WeArtFollowTargetEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            // Label
            {
                var trackingHeader = new Label("Tracking");
                trackingHeader.AddToClassList("header");
            }

            // RigidBody
            var updateProperty = serializedObject.FindProperty(nameof(WeArtFollowTarget._rigidBody));
            var updatePropertyField = new PropertyField(updateProperty)
            {
                tooltip = "The rigid body that will receive velocity changes"
            };
            editor.Add(updatePropertyField);

            // Follow target
            var propertyTrackingSource = serializedObject.FindProperty(nameof(WeArtFollowTarget._followTarget));
            var propertyFieldTrackingSource = new PropertyField(propertyTrackingSource)
            {
                tooltip = "Set the tracking source to follow"
            };
            editor.Add(propertyFieldTrackingSource);

            // Follow power
            var propertyFollowPower = serializedObject.FindProperty(nameof(WeArtFollowTarget._followPower));
            var propertyFieldFollowPower = new PropertyField(propertyFollowPower)
            {
                tooltip = "The power of velocity used to get to the target"
            };
            editor.Add(propertyFieldFollowPower);

            // Position offset
            var propertyPositionOffset = serializedObject.FindProperty(nameof(WeArtFollowTarget._positionOffset));
            var propertyFieldPositionOffset = new PropertyField(propertyPositionOffset)
            {
                tooltip = "Set the position offset"
            };
            editor.Add(propertyFieldPositionOffset);

            // Rotation offset
            var propertyRotationOffset = serializedObject.FindProperty(nameof(WeArtFollowTarget._rotationOffset));
            var propertyFieldRotationOffset = new PropertyField(propertyRotationOffset)
            {
                tooltip = "Set the rotation offset"
            };
            editor.Add(propertyFieldRotationOffset);

            return editor;
        }
    }
}