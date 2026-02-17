using System;
using UnityEditor;
using UnityEngine.UIElements;
using WeArt.Core;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom property drawer for properties of type <see cref="Closure"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(Closure))]
    public class ClosurePropertyDrawer : WeArtPropertyDrawer
    {
        public static VisualElement CreatePropertyGUI(string name, Action<float> setter = null, Func<float> getter = null)
        {
            var container = new VisualElement();
            container.AddToClassList("propertyRow");

            // Toggle
            var toggle = new Toggle();
            toggle.SetValueWithoutNotify(false);
            toggle.Display(false);
            container.Add(toggle);

            // Label
            var label = new Label(name);
            label.AddToClassList("unity-base-field__label");
            label.AddToClassList("unity-property-field__label");
            container.Add(label);

            // Value slider
            var valueSlider = new SliderWithInputField(WeArtConstants.minClosure, WeArtConstants.maxClosure);
            valueSlider.SetEnabled(false);
            valueSlider.SetValueWithoutNotify(WeArtConstants.defaultClosure);
            container.Add(valueSlider);

            if (setter != null)
            {
                valueSlider.RegisterCallback<ChangeEvent<float>>(evt =>
                {
                    setter(evt.newValue);
                });
            }

            if (getter != null)
            {
                valueSlider.RegisterCallback<AttachToPanelEvent>(evt => EditorApplication.update += updateCallback);
                valueSlider.RegisterCallback<DetachFromPanelEvent>(evt => EditorApplication.update -= updateCallback);
                void updateCallback() => valueSlider.SetValueWithoutNotify(getter());
            }

            return container;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var valueProp = property.FindPropertyRelative(nameof(Closure._value));

            return CreatePropertyGUI(
                name: property.displayName,
                setter: value => valueProp.floatValue = value,
                getter: () => valueProp.floatValue
            );
        }

    }
}