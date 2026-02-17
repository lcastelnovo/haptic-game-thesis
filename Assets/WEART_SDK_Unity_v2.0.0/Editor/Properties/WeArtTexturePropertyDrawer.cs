using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using WeArt.Core;
using Texture = WeArt.Core.Texture;

namespace WeArt.UnityEditor
{
    /// <summary>
    /// A custom property drawer for properties of type <see cref="Texture"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(Texture))]
    public class TexturePropertyDrawer : WeArtPropertyDrawer
    {

        bool _isInsidefBounds = true;

        // The gradient indicating the possible slider's dragger colors
        private static readonly string[] _velocitySlidersLabels = new string[]
        {
            "Vx", "Vy", "Vz"
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Sub-properties
            var activeProp = property.FindPropertyRelative(nameof(Texture._active));
            var textureTypeProp = property.FindPropertyRelative(nameof(Texture._textureType));
            var forcedVelocityProp = property.FindPropertyRelative(nameof(Texture._forcedVelocity));

            // Values getters and setters
            TextureType getTextureType() => (TextureType)textureTypeProp.intValue + WeArtConstants.minTextureIndex;
            void setTextureType(TextureType type) => textureTypeProp.intValue = (int)type - WeArtConstants.minTextureIndex;

            // Container that holds all fields
            var completeContainer = new VisualElement();
            completeContainer.AddToClassList("toggled");

            // Property container
            var textureMaincontainer = new VisualElement();
            textureMaincontainer.AddToClassList("propertyRow");
            textureMaincontainer.AddToClassList("toggled");

            var textureBehaviorLabelContainer = new VisualElement();
            textureBehaviorLabelContainer.AddToClassList("propertyRow");

            // Volume
            var volumeContainer = new VisualElement();
            volumeContainer.AddToClassList("propertyRow");
            volumeContainer.AddToClassList("toggled");

            var volumeValueProp = property.FindPropertyRelative(nameof(Texture._volume));

            // Volume Value slider
            var volumeValueSlider = new SliderWithInputField(WeArtConstants.minVolumeTexture, WeArtConstants.maxVolumeTexture);
            volumeValueSlider.SetValueWithoutNotify(volumeValueProp.floatValue);
            volumeValueSlider.BindProperty(volumeValueProp);

            //Forced Velocity
            var forcedVelocityContainer = new VisualElement();
            forcedVelocityContainer.AddToClassList("propertyRow");
            //volumeContainer.AddToClassList("toggled");

            var forcedVelocityToggle = new Toggle();
            forcedVelocityToggle.SetValueWithoutNotify(forcedVelocityProp.boolValue);
            forcedVelocityToggle.BindProperty(forcedVelocityProp);

            // Active Toggle
            var toggle = new Toggle();
            toggle.SetValueWithoutNotify(activeProp.boolValue);
            toggle.BindProperty(activeProp);
            toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                sendChange(evt.newValue, null, null, null, null);
            });
            textureMaincontainer.Add(toggle);

            // Label
            var label = new Label();
            if (_isInsidefBounds)
            {
                label = new Label(property.displayName);
            }
            else
            {
                label = new Label(((int)getTextureType()).ToString());
            }

            label.AddToClassList("unity-base-field__label");
            label.AddToClassList("unity-property-field__label");
            textureMaincontainer.Add(label);

            // Values
            var mainValuesContainer = new VisualElement();
            mainValuesContainer.SetEnabled(toggle.value);
            toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                mainValuesContainer.SetEnabled(evt.newValue);
                volumeValueSlider.SetEnabled(evt.newValue);
                forcedVelocityToggle.SetEnabled(evt.newValue);
            });
            mainValuesContainer.AddToClassList("horizontal");
            mainValuesContainer.AddToClassList("unity-property-field");
            {
                // Texture image
                var image = new Image();
                image.AddToClassList("texture-preview");
                image.image = GetTexturePreview(getTextureType());
                mainValuesContainer.Add(image);

                var innerValuesContainer = new VisualElement();
                innerValuesContainer.AddToClassList("unity-property-field");

                // Texture type
                var textureTypeField = new PropertyField(textureTypeProp)
                {
                    label = string.Empty
                };
                textureTypeField.userData = getTextureType();
                textureTypeField.RegisterCallback<ChangeEvent<string>>(evt =>
                {
                    var previousValue = (TextureType)textureTypeField.userData;
                    var newValue = getTextureType();
                    textureTypeField.userData = newValue;

                    setTextureType(newValue);
                    sendChange(null, newValue, null, null, null);

                    image.image = GetTexturePreview(newValue);
                });
                innerValuesContainer.Add(textureTypeField);

                /*
                // Velocity foldout
                var velocityFoldout = new Foldout
                {
                    text = "Velocity",
                    value = false
                };
                innerValuesContainer.Add(velocityFoldout);
                */

                /*
                // Velocity sliders
                var velocitySliders = new Slider[velocitiesProps.Length];
                for (int i = 0; i < velocitySliders.Length; i++)
                {
                    var velocitySlider = new SliderWithInputField(WeArtConstants.minTextureVelocity, WeArtConstants.maxTextureVelocity);
                    velocitySlider.SetValueWithoutNotify(velocitiesProps[i].floatValue);
                    velocitySlider.BindProperty(velocitiesProps[i]);

                    // Slider label
                    Label sliderLabel = new Label(_velocitySlidersLabels[i]);
                    velocitySlider.Insert(0, sliderLabel);

                    // Changes
                    velocitySlider.RegisterCallback<ChangeEvent<float>>(evt =>
                        sendChange(null, null, velocitySliders.Select(s => s.value).ToArray())
                    );

                    velocitySliders[i] = velocitySlider;
                    velocityFoldout.Add(velocitySlider);
                }
                */
                mainValuesContainer.Add(innerValuesContainer);
            }

            textureMaincontainer.Add(mainValuesContainer);

            // Label
            {
                var header = new Label("Texture Behavior");
                header.AddToClassList("header");

                textureBehaviorLabelContainer.Add(header);
            }

            // ValueSlider Callback
            volumeValueSlider.RegisterCallback<ChangeEvent<float>>(evt => sendChange(null, null, null, evt.newValue, null));
            volumeValueSlider.SetEnabled(toggle.value);

            // ForcedVelocity Callback
            forcedVelocityToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                sendChange(null, null, null, null, evt.newValue);
            });
            forcedVelocityToggle.SetEnabled(toggle.value);

            // Texture Volume Label
            {
                var header = new Label("Texture Volume");
                header.AddToClassList("unity-base-field__label");
                header.AddToClassList("unity-property-field__label");

                volumeContainer.Add(header);
            }
            volumeContainer.Add(volumeValueSlider);

            completeContainer.Add(textureMaincontainer);
            completeContainer.Add(textureBehaviorLabelContainer);
            completeContainer.Add(volumeContainer);

            // Forced Velocity Label
            {
                var header = new Label("Forced Velocity");
                header.AddToClassList("unity-base-field__label");
                header.AddToClassList("unity-property-field__label");

                forcedVelocityContainer.Add(header);
            }
            forcedVelocityContainer.Add(forcedVelocityToggle);
            completeContainer.Add(forcedVelocityContainer);

            return completeContainer;


            // Local function that creates and send a ChangeEvent<Texture> event
            void sendChange(bool? active, TextureType? type, float[] velocity, float? volume, bool? forcedVelocity)
            {
                var previousValue = new Texture()
                {
                    Active = activeProp.boolValue,
                    TextureType = getTextureType(),
                    Volume = volumeValueProp.floatValue,
                    ForcedVelocity = forcedVelocityProp.boolValue
                };

                var newValue = new Texture()
                {
                    Active = active ?? previousValue.Active,
                    TextureType = type ?? previousValue.TextureType,
                    Velocity = velocity != null ? velocity[2] : previousValue.Velocity,
                    Volume = volume ?? previousValue.Volume,
                    ForcedVelocity = forcedVelocity ?? previousValue.ForcedVelocity
                };

                
                if (!previousValue.Equals(newValue))
                {
                    using (var tempChangedEvt = ChangeEvent<Texture>.GetPooled(previousValue, newValue))
                    {
                        tempChangedEvt.target = textureMaincontainer;
                        textureMaincontainer.SendEvent(tempChangedEvt);
                    }
                }
            }
        }

        private static UnityEngine.Texture GetTexturePreview(TextureType textureType)
        {
            return Resources.Load<UnityEngine.Texture>($"Textures/{(int)textureType}");
        }
    }
}