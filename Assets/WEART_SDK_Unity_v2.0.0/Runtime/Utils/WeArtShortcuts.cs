using UnityEngine;
using WeArt.Components;

namespace WeArt.Utils
{
    /// <summary>
    /// WeArtShortcuts — utils class to assign the keys for events from keyboard.
    /// </summary>
    public class WeArtShortcuts : MonoBehaviour
    {
        [SerializeField] internal KeyCode resetCalibration = KeyCode.KeypadEnter; 
        [SerializeField] internal CalibrationManager calibrationManager;

        private void Update()
        {
            if (Input.GetKeyDown(resetCalibration)) ResetCalibration();
        }

        /// <summary>
        /// Resets the calibration process. 
        /// </summary>
        private void ResetCalibration()
        {
            if (calibrationManager) calibrationManager.ResetCalibration();
            WeArtController.Instance.ResetCalibration();
        }
    }
}
