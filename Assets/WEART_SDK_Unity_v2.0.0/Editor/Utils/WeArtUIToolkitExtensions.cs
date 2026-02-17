using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// Custom Slider with a numerical input field aside.
    /// The UI Toolkit Sliders has a new property called "showInputField" starting from Unity 2021.1.7
    /// but we're not using it since we want compatibility with Unity 2019.
    /// </summary>
    public class SliderWithInputField : Slider
    {
        private TextField _inputTextField;

        public SliderWithInputField() : base()
        {
            SetupTextField();
        }

        public SliderWithInputField(float start, float end, SliderDirection direction = SliderDirection.Horizontal, float pageSize = 0) : base(start, end, direction, pageSize)
        {
            SetupTextField();
        }

        public SliderWithInputField(string label, float start = 0, float end = 10, SliderDirection direction = SliderDirection.Horizontal, float pageSize = 0) : base(label, start, end, direction, pageSize)
        {
            SetupTextField();
        }

        protected void SetupTextField()
        {
            _inputTextField = new TextField() { name = "unity-text-field" };
            _inputTextField.AddToClassList(ussClassName + "__text-field");
            _inputTextField.RegisterCallback<ChangeEvent<string>>(OnTextFieldValueChange);
            _inputTextField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusOut);
            Add(_inputTextField);
            UpdateTextFieldValue();
        }

        public override void SetValueWithoutNotify(float newValue)
        {
            base.SetValueWithoutNotify(newValue);
            UpdateTextFieldValue();
        }

        protected void OnTextFieldFocusOut(FocusOutEvent evt)
        {
            UpdateTextFieldValue();
        }

        protected void UpdateTextFieldValue()
        {
            _inputTextField.SetValueWithoutNotify(string.Format(CultureInfo.InvariantCulture, "{0:g7}", value));
        }

        protected void OnTextFieldValueChange(ChangeEvent<string> evt)
        {
            var newValue = GetClampedValue(ParseStringToValue(evt.newValue));
            if (!EqualityComparer<float>.Default.Equals(newValue, value))
            {
                value = newValue;
                evt.StopPropagation();

                if (panel != null)
                {
                    var onViewDataReadyMethod = typeof(Slider).GetMethod(
                        name: "OnViewDataReady",
                        bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
                        binder: null,
                        types: new Type[] { },
                        modifiers: null
                    );

                    onViewDataReadyMethod.Invoke(this, null);
                }
            }
        }

        protected float GetClampedValue(float newValue)
        {
            float lowest = lowValue, highest = highValue;
            if (lowest.CompareTo(highest) > 0)
            {
                var t = lowest;
                lowest = highest;
                highest = t;
            }

            return Mathf.Clamp(newValue, lowest, highest);
        }

        protected float ParseStringToValue(string stringValue)
        {
            if (float.TryParse(stringValue.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return default;
        }
    }
}