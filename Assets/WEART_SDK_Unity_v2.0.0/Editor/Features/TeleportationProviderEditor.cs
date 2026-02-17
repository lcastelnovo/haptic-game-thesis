using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.GestureInteractions;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="TeleportationProvider"/>.
    /// </summary>
    [CustomEditor(typeof(TeleportationProvider), true), CanEditMultipleObjects]
    public class TeleportationProviderEditor : WeArtComponentEditor
    {
        private TeleportationProvider Provider => serializedObject.targetObject as TeleportationProvider;

        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            var teleportTargetProperty = serializedObject.FindProperty(nameof(TeleportationProvider.teleportTarget));
            
            // Label
            {
                var header = new Label("Teleport settings");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Hand Controller
            {
                var property = serializedObject.FindProperty(nameof(TeleportationProvider.teleportHandSide));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Hand Side to monitor gestures"
                };
                
                editor.Add(propertyField);
            }

            // Hand Gesture
            {
                var property = serializedObject.FindProperty(nameof(TeleportationProvider.prepareGesture));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Gesture that prepares teleportation feature"
                };
                editor.Add(propertyField);
            }

            // Hand Gesture
            {
                var property = serializedObject.FindProperty(nameof(TeleportationProvider.launchGesture));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Gesture that launches teleportation"
                };
                editor.Add(propertyField);
            }
            
            // Teleport target OK
            {
                var property = serializedObject.FindProperty(nameof(TeleportationProvider.teleportTargetOk));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Teleportation target where possible to teleport"
                };
                editor.Add(propertyField);
            }
            
            // Teleport target Obstacles
            {
                var property = serializedObject.FindProperty(nameof(TeleportationProvider.teleportTargetObstacles));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "(Optional) Teleportation target when obstacles are found."
                };
                editor.Add(propertyField);
            }
            
            // XR Rig
            {
                var property = serializedObject.FindProperty(nameof(TeleportationProvider.playerController));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Player controller attached to XR Rig that should be teleported"
                };
                editor.Add(propertyField);
            }
            
            // Teleport laser origin right
            {
                var property = serializedObject.FindProperty(nameof(TeleportationProvider.laserOriginRight));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Laser origin at right hand"
                };
                editor.Add(propertyField);
            }
            
            // Teleport laser origin left
            {
                var property = serializedObject.FindProperty(nameof(TeleportationProvider.laserOriginLeft));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Laser origin at left hand"
                };
                editor.Add(propertyField);
            }
            
            // Label
            {
                var header = new Label("Way to Detect Teleportation Target");
                header.AddToClassList("header");
                editor.Add(header);
            }
            
            // Detection method of teleportation target
            
            var detectionProperty = serializedObject.FindProperty(nameof(TeleportationProvider.teleportTarget));
            var detectionPropertyField = new PropertyField(detectionProperty)
            {
                tooltip = "Select a method to detect the teleportation target"
            };
            editor.Add(detectionPropertyField);
            
            
            // Name of teleportation target
            
                var targetNameProperty = serializedObject.FindProperty(nameof(TeleportationProvider.teleportName));
                var targetNamePropertyField = new PropertyField(targetNameProperty)
                {
                    tooltip = "Name of the surface allowed for teleportation"
                };
                
                editor.Add(targetNamePropertyField);
            
            
            // Layer of teleportation target
            
               var targetLayerProperty = serializedObject.FindProperty(nameof(TeleportationProvider.teleportLayer));
               var targetLayerPropertyField = new PropertyField(targetLayerProperty)
               {
                   tooltip = "Layer of the surface allowed for teleportation"
               };
               editor.Add(targetLayerPropertyField);
               
            void UpdateFieldVisibility()
            {
                var target = (TeleportTarget)teleportTargetProperty.enumValueIndex;
                
                switch (target)
                {
                    case TeleportTarget.LayerMask:
                    {
                        targetNamePropertyField.style.display = DisplayStyle.None;
                        targetLayerPropertyField.style.display = DisplayStyle.Flex;
                        break;
                    }
                    case TeleportTarget.TextName:
                    {
                        targetNamePropertyField.style.display = DisplayStyle.Flex;
                        targetLayerPropertyField.style.display = DisplayStyle.None;
                        break;
                    }
                }
            }

            UpdateFieldVisibility();
            
            detectionPropertyField.RegisterCallback<ChangeEvent<string>>(evt => UpdateFieldVisibility());;
            
            return editor;
        }
    }
}