using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WeArt.Core;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom property drawer for properties of type <see cref="Temperature"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(Temperature))]
    public class TemperaturePropertyDrawer : WeArtPropertyDrawer
    {

        // The gradient indicating the possible slider's dragger colors
        private static readonly Gradient _temperatureSliderGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(Color.blue, 0.0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(Color.red, 1.0f)
            }
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var activeProp = property.FindPropertyRelative(nameof(Temperature._active));
            var valueProp = property.FindPropertyRelative(nameof(Temperature._value));

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
            var valueSlider = new SliderWithInputField(WeArtConstants.minTemperature, WeArtConstants.maxTemperature);
            valueSlider.SetValueWithoutNotify(valueProp.floatValue);
            valueSlider.BindProperty(valueProp);
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

                var draggerColor = valueSlider.enabledSelf ? (StyleColor)_temperatureSliderGradient.Evaluate(valueSlider.value) : StyleKeyword.Null;
                dragger.style.backgroundColor = draggerColor;
                dragger.style.borderRightColor = draggerColor;
                dragger.style.borderLeftColor = draggerColor;
                dragger.style.borderTopColor = draggerColor;
                dragger.style.borderBottomColor = draggerColor;
            }

            // Local function that creates and send a ChangeEvent<Temperature> event
            void sendChange(bool? active, float? value)
            {
                var previousValue = new Temperature()
                {
                    Active = activeProp.boolValue,
                    Value = valueProp.floatValue
                };

                var newValue = new Temperature()
                {
                    Active = active ?? previousValue.Active,
                    Value = value ?? previousValue.Value
                };

                if (!previousValue.Equals(newValue))
                {
                    using (var tempChangedEvt = ChangeEvent<Temperature>.GetPooled(previousValue, newValue))
                    {
                        tempChangedEvt.target = container;
                        container.SendEvent(tempChangedEvt);
                    }
                }
            }
        }

    }
}