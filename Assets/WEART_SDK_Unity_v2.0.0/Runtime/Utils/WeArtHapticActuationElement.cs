using System.Globalization;
using UnityEngine;
using TMPro;
using WeArt.Components;

namespace WeArt.Utils.Actuation
{
    /// <summary>
    /// WeArtHapticActuationElement — UI container to represent the Actuation data from WeArtHapticObject in runtime.
    /// </summary>
    public class WeArtHapticActuationElement : MonoBehaviour
    {
        [SerializeField] internal TextMeshProUGUI fingerNameText;
        [SerializeField] internal TextMeshProUGUI forceValue;
        [SerializeField] internal TextMeshProUGUI temperatureValue;
        [SerializeField] internal TextMeshProUGUI textureTypeValue;
        [SerializeField] internal TextMeshProUGUI textureVelocityValue;
        [SerializeField] internal TextMeshProUGUI textureVolumeValue;

        private WeArtHapticObject _hapticObject;
        private static readonly string inactiveText = "Inactive";

        /// <summary>
        /// Sets WeArtHapticObject to track its values.
        /// </summary>
        /// <param name="hapticObject"></param>
        public void SetHapticObject(WeArtHapticObject hapticObject)
        {
            _hapticObject = hapticObject;
        }
        
        private void Update()
        {
            UpdateActuationData();
        }

        /// <summary>
        /// Updates the actuation data from WeArtHapticObject. 
        /// </summary>
        private void UpdateActuationData()
        {
            if (!_hapticObject) return;
            
            forceValue.text = _hapticObject.Force.Active
                ? _hapticObject.Force.Value.ToString(CultureInfo.InvariantCulture)
                : inactiveText;
            
            temperatureValue.text = _hapticObject.Temperature.Active
                ? _hapticObject.Temperature.Value.ToString(CultureInfo.InvariantCulture)
                : inactiveText;

            if (!_hapticObject.Texture.Active)
            {
                textureTypeValue.text = inactiveText;
                textureVelocityValue.text = inactiveText;
                textureVolumeValue.text = inactiveText;
                return;
            }
            
            textureTypeValue.text = _hapticObject.Texture.TextureType.ToString();
            textureVelocityValue.text = _hapticObject.Texture.Velocity.ToString(CultureInfo.InvariantCulture);
            textureVolumeValue.text = _hapticObject.Texture.Volume.ToString(CultureInfo.InvariantCulture);
        }
    }
}
