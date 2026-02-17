using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using WeArt.Utils;

namespace WeArt.UnityEditor
{
    [CustomEditor(typeof(WeArtBillboard), true), CanEditMultipleObjects]
    public class WeArtBillboardEditor : WeArtComponentEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            editor.Bind(serializedObject);

            serializedObject.Update();
            
            {
                var header = new Label("Billboard Behaviour Type");
                header.AddToClassList("header");
                editor.Add(header);
            }
            
            var behaviourProperty = serializedObject.FindProperty(nameof(WeArtBillboard.behaviour));

            var behaviourField = new PropertyField(behaviourProperty)
            {
                tooltip = "Select the Billboard Tracking Behaviour"
            };
            editor.Add(behaviourField);
            
            var followContainer = new VisualElement();
            var callContainer = new VisualElement();

            editor.Add(followContainer);
            editor.Add(callContainer);
            
            var xOffsetProperty = serializedObject.FindProperty(nameof(WeArtBillboard.xOffset));
            var propertyField = new PropertyField(xOffsetProperty)
            {
                tooltip = "How far it should be from the camera" 
            };
            followContainer.Add(propertyField);
            
            var keepHeightProperty = serializedObject.FindProperty(nameof(WeArtBillboard.keepInitialHeight));
            var keepHeightField = new PropertyField(keepHeightProperty)
            {
                tooltip = "If enabled it keeps the initial height of the object." 
            };
            followContainer.Add(keepHeightField);
            
            var yOffsetProperty = serializedObject.FindProperty(nameof(WeArtBillboard.yOffset));
            var yOffsetField = new PropertyField(yOffsetProperty)
            {
                tooltip = "Set the offset from center of camera view." 
            };
            followContainer.Add(yOffsetField);
            
            var callProperty = serializedObject.FindProperty(nameof(WeArtBillboard.callBillboardKey));
            propertyField = new PropertyField(callProperty)
            {
                tooltip = "Set the key to call the object" 
            };
            callContainer.Add(propertyField);
            
            void UpdateFieldVisibility()
            {
                var behaviour = (BillboardEnum)behaviourProperty.enumValueIndex;

                switch (behaviour)
                {
                    case BillboardEnum.Static:
                    {
                        followContainer.style.display = DisplayStyle.None;
                        callContainer.style.display = DisplayStyle.None;
                        break;
                    }
                    case BillboardEnum.RotatedToCamera:
                    {
                        followContainer.style.display = DisplayStyle.None;
                        callContainer.style.display = DisplayStyle.None;
                        break;
                    }
                    case BillboardEnum.FollowingCameraView:
                    {
                        followContainer.style.display = DisplayStyle.Flex;
                        callContainer.style.display = DisplayStyle.None;
                        UpdateYOffsetVisibility();
                        break;
                    }
                    case BillboardEnum.CalledByKey:
                    {
                        followContainer.style.display = DisplayStyle.None;
                        callContainer.style.display = DisplayStyle.Flex;
                        break;
                    }
                }
            }

            void UpdateYOffsetVisibility()
            {
                yOffsetField.style.display = keepHeightProperty.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
            }
            
            UpdateFieldVisibility();
            
            behaviourField.RegisterCallback<ChangeEvent<string>>(evt => UpdateFieldVisibility());
            keepHeightField.RegisterCallback<ChangeEvent<bool>>(evt => UpdateYOffsetVisibility());
            
            return editor;
        }
    }
}