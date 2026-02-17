using System.ComponentModel;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UI;
using UnityEngine.UIElements;
using WeArt.Components;
using WeArt.Core;
using WeArt.Utils;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom inspector for components of type <see cref="WeArtEasyGraspPose"/>.
    /// </summary>
    [CustomEditor(typeof(WeArtEasyGraspPose), true), CanEditMultipleObjects]
    public class WeArtEasyGraspPoseEditor : WeArtComponentEditor
    {
        private WeArtEasyGraspPose easyGraspPose => serializedObject.targetObject as WeArtEasyGraspPose;

        public override VisualElement CreateInspectorGUI()
        {
            var editor = base.CreateInspectorGUI();
            // Label
            {
                var header = new Label("Properties");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Interpolate
            {
                var property = serializedObject.FindProperty(nameof(WeArtEasyGraspPose._interpolate));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "Defines if you want to interpolate to rotaion and position of the easy grasp pose"
                };
                editor.Add(propertyField);
            }

            // Hand Side
            {
                var property = serializedObject.FindProperty(nameof(WeArtEasyGraspPose._handSide));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The hand side of the pose"
                };
                propertyField.SetEnabled(false);
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Touchable Object Transform");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Touchable Position Pose
            {
                var property = serializedObject.FindProperty(nameof(WeArtEasyGraspPose._touchablePosition));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The touchable position of the pose"
                };
                editor.Add(propertyField);
            }

            // Touchable Rotation Pose
            {
                var property = serializedObject.FindProperty(nameof(WeArtEasyGraspPose._touchableRotation));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The touchable rotation of the pose"
                };
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Anchored Hand Transform");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Hand Position Pose
            {
                var property = serializedObject.FindProperty(nameof(WeArtEasyGraspPose._handPosition));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The hand position of the pose"
                };
                editor.Add(propertyField);
            }

            // Hand Rotation Pose
            {
                var property = serializedObject.FindProperty(nameof(WeArtEasyGraspPose._handRotation));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The hand rotation of the pose"
                };
                editor.Add(propertyField);
            }

            // Label
            {
                var header = new Label("Fingers Closure Poses");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Thumb Closure
            {
                var horizontalContainerThumbClosure = new VisualElement();
                horizontalContainerThumbClosure.AddToClassList("horizontal");
                horizontalContainerThumbClosure.AddToClassList("unity-property-field");
                editor.Add(horizontalContainerThumbClosure);

                // Label
                {
                    var thumbClosurePropertyName = new Label("Thumb Closure");
                    thumbClosurePropertyName.AddToClassList("unity-base-field__label");
                    horizontalContainerThumbClosure.Add(thumbClosurePropertyName);
                }

                // Slider
                {
                    var valueSlider = new SliderWithInputField(WeArtConstants.minClosure, WeArtConstants.maxClosure);
                    valueSlider.SetValueWithoutNotify(easyGraspPose._thumbClosure);
                    valueSlider.BindProperty(serializedObject.FindProperty("_thumbClosure"));
                    horizontalContainerThumbClosure.Add(valueSlider);
                }
            }

            // Thumb Abduction
            {
                var horizontalContainerThumbAbduction = new VisualElement();
                horizontalContainerThumbAbduction.AddToClassList("horizontal");
                horizontalContainerThumbAbduction.AddToClassList("unity-property-field");
                editor.Add(horizontalContainerThumbAbduction);

                // Label
                {
                    var thumbAbductionPropertyName = new Label("Thumb Abduction");
                    thumbAbductionPropertyName.AddToClassList("unity-base-field__label");
                    horizontalContainerThumbAbduction.Add(thumbAbductionPropertyName);
                }

                // Slider
                {
                    var valueSlider = new SliderWithInputField(WeArtConstants.minClosure, WeArtConstants.maxClosure);
                    valueSlider.SetValueWithoutNotify(easyGraspPose._thumbAbduction);
                    valueSlider.BindProperty(serializedObject.FindProperty("_thumbAbduction"));
                    horizontalContainerThumbAbduction.Add(valueSlider);
                }
            }

            // Index Closure
            {
                var horizontalContainerIndexClosure = new VisualElement();
                horizontalContainerIndexClosure.AddToClassList("horizontal");
                horizontalContainerIndexClosure.AddToClassList("unity-property-field");
                editor.Add(horizontalContainerIndexClosure);

                // Label
                {
                    var IndexClosurePropertyName = new Label("Index Closure");
                    IndexClosurePropertyName.AddToClassList("unity-base-field__label");
                    horizontalContainerIndexClosure.Add(IndexClosurePropertyName);
                }

                // Slider
                {
                    var valueSlider = new SliderWithInputField(WeArtConstants.minClosure, WeArtConstants.maxClosure);
                    valueSlider.SetValueWithoutNotify(easyGraspPose._indexClosure);
                    valueSlider.BindProperty(serializedObject.FindProperty("_indexClosure"));
                    horizontalContainerIndexClosure.Add(valueSlider);
                }
            }

            // Middle Closure
            {
                var horizontalContainerMiddleClosure = new VisualElement();
                horizontalContainerMiddleClosure.AddToClassList("horizontal");
                horizontalContainerMiddleClosure.AddToClassList("unity-property-field");
                editor.Add(horizontalContainerMiddleClosure);

                // Label
                {
                    var MiddleClosurePropertyName = new Label("Middle Closure");
                    MiddleClosurePropertyName.AddToClassList("unity-base-field__label");
                    horizontalContainerMiddleClosure.Add(MiddleClosurePropertyName);
                }

                // Slider
                {
                    var valueSlider = new SliderWithInputField(WeArtConstants.minClosure, WeArtConstants.maxClosure);
                    valueSlider.SetValueWithoutNotify(easyGraspPose._middleClosure);
                    valueSlider.BindProperty(serializedObject.FindProperty("_middleClosure"));
                    horizontalContainerMiddleClosure.Add(valueSlider);
                }
            }

            // Annular Closure
            {
                var horizontalContainerAnnularClosure = new VisualElement();
                horizontalContainerAnnularClosure.AddToClassList("horizontal");
                horizontalContainerAnnularClosure.AddToClassList("unity-property-field");
                editor.Add(horizontalContainerAnnularClosure);

                // Label
                {
                    var AnnularClosurePropertyName = new Label("Annular Closure");
                    AnnularClosurePropertyName.AddToClassList("unity-base-field__label");
                    horizontalContainerAnnularClosure.Add(AnnularClosurePropertyName);
                }

                // Slider
                {
                    var valueSlider = new SliderWithInputField(WeArtConstants.minClosure, WeArtConstants.maxClosure);
                    valueSlider.SetValueWithoutNotify(easyGraspPose._annularClosure);
                    valueSlider.BindProperty(serializedObject.FindProperty("_annularClosure"));
                    horizontalContainerAnnularClosure.Add(valueSlider);
                }
            }

            // Pinky Closure
            {
                var horizontalContainerPinkyClosure = new VisualElement();
                horizontalContainerPinkyClosure.AddToClassList("horizontal");
                horizontalContainerPinkyClosure.AddToClassList("unity-property-field");
                editor.Add(horizontalContainerPinkyClosure);

                // Label
                {
                    var PinkyClosurePropertyName = new Label("Pinky Closure");
                    PinkyClosurePropertyName.AddToClassList("unity-base-field__label");
                    horizontalContainerPinkyClosure.Add(PinkyClosurePropertyName);
                }

                // Slider
                {
                    var valueSlider = new SliderWithInputField(WeArtConstants.minClosure, WeArtConstants.maxClosure);
                    valueSlider.SetValueWithoutNotify(easyGraspPose._pinkyClosure);
                    valueSlider.BindProperty(serializedObject.FindProperty("_pinkyClosure"));
                    horizontalContainerPinkyClosure.Add(valueSlider);
                }
            }

            // Label
            {
                var header = new Label("Grasp Origin Properties");
                header.AddToClassList("header");
                editor.Add(header);
            }

            // Grasp Origin Offset
            {
                var property = serializedObject.FindProperty(nameof(WeArtEasyGraspPose._graspOriginOffset));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The location on which the hand can snap grasp"
                };
                editor.Add(propertyField);
            }

            // Grasp Origin Radius
            {
                var property = serializedObject.FindProperty(nameof(WeArtEasyGraspPose._graspOriginRadius));
                var propertyField = new PropertyField(property)
                {
                    tooltip = "The radius on which the hand can snap grasp fron the GraspOriginOffset"
                };
                editor.Add(propertyField);
            }

            return editor;
        }
    }
}