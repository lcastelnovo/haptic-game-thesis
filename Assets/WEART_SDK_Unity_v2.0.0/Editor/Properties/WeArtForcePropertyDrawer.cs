using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WeArt.Core;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom property drawer for properties of type <see cref="Force"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(Force))]
    public class ForcePropertyDrawer : WeArtPropertyDrawer
    {

        // The gradient indicating the possible slider's dragger colors
        private static readonly Gradient _forceSliderGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(Color.yellow, 1.0f)
            }
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var activeProp = property.FindPropertyRelative(nameof(Force._active));
            var xValueProp = property.FindPropertyRelative(nameof(Force._value));

            var container = new VisualElement();
            container.AddToClassList("propertyRow");
            container.AddToClassList("toggled");

            // Toggle
            var toggle = new Toggle();
            toggle.SetValueWithoutNotify(activeProp.boolValue);
            toggle.BindProperty(activeProp);
            container.Add(toggle);

            // Label
            var label = new Label(property.displayName);
            label.AddToClassList("unity-base-field__label");
            label.AddToClassList("unity-property-field__label");
            container.Add(label);

            // Value slider
            var valueSlider = new SliderWithInputField(WeArtConstants.minForce, WeArtConstants.maxForce);
            valueSlider.SetValueWithoutNotify(xValueProp.floatValue);
            valueSlider.BindProperty(xValueProp);
            valueSlider.SetEnabled(toggle.value);
            updateDraggerColor();
            container.Add(valueSlider);


            // Changes
            toggle.RegisterCallback<ChangeEvent<bool>>(evt => valueSlider.SetEnabled(evt.newValue));
            toggle.RegisterCallback<ChangeEvent<bool>>(evt => sendChange(evt.newValue, null));
            toggle.RegisterCallback<ChangeEvent<bool>>(evt => updateDraggerColor());
            valueSlider.RegisterCallback<ChangeEvent<float>>(evt => sendChange(null, evt.newValue));
            valueSlider.RegisterCallback<ChangeEvent<float>>(evt => updateDraggerColor());

            return container;


            // Local function that updates the slider dragger color
            void updateDraggerColor()
            {
                var dragger = valueSlider.Q("unity-dragger");
                if (dragger == null)
                    return;

                var draggerColor = valueSlider.enabledSelf ? (StyleColor)_forceSliderGradient.Evaluate(valueSlider.value) : StyleKeyword.Null;
                dragger.style.backgroundColor = draggerColor;
                dragger.style.borderRightColor = draggerColor;
                dragger.style.borderLeftColor = draggerColor;
                dragger.style.borderTopColor = draggerColor;
                dragger.style.borderBottomColor = draggerColor;
            }

            // Local function that creates and send a ChangeEvent<Force> event
            void sendChange(bool? active, float? value)
            {
                var previousValue = new Force()
                {
                    Active = activeProp.boolValue,
                    Value = xValueProp.floatValue
                };

                var newValue = new Force()
                {
                    Active = active ?? previousValue.Active,
                    Value = value ?? previousValue.Value
                };

                if (!previousValue.Equals(newValue))
                {
                    using (var tempChangedEvt = ChangeEvent<Force>.GetPooled(previousValue, newValue))
                    {
                        tempChangedEvt.target = container;
                        container.SendEvent(tempChangedEvt);
                    }
                }
            }
        }

    }
}