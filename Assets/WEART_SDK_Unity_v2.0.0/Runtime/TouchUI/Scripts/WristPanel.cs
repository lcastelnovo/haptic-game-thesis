using UnityEngine;
using UnityEngine.UI;
using WeArt.Components;
using WeArt.Utils.Actuation;

namespace WeArt.TouchUI
{
    /// <summary>
    /// WristPanel — class to manage the Show / Hide the WeArt Panels and restart Calibration process.
    /// </summary>
    public class WristPanel : MonoBehaviour
    {
        [SerializeField] internal Button bluetoothButton;
        [SerializeField] internal Button statusButton;
        [SerializeField] internal Button calibrationButton;
        [SerializeField] internal Button actuationButton;
        
        [SerializeField] internal BleConnectionPanel blePanel;
        [SerializeField] internal WeArtStatusTracker statusPanel;
        [SerializeField] internal CalibrationManager calibrationManager;
        [SerializeField] internal WeArtHapticActuationPanel actuationPanel;

        private bool StandaloneActive = false;

        public Button BluetoothButton => bluetoothButton;
        public Button StatusButton => statusButton;
        public Button CalibrationButton => calibrationButton;
        public Button ActuationButton => actuationButton;

        private void Awake()
        {

#if UNITY_ANDROID && !UNITY_EDITOR
            StandaloneActive = true;
#endif           
        }

        private void Start()
        {
            // If Standalone version, do not show BLE Panel and set to non Interactable BLE Button on Wrist Panel
            if (!StandaloneActive)
            {
                if (blePanel.gameObject.activeInHierarchy)
                {
                    blePanel.gameObject.SetActive(false);
                }
                bluetoothButton.interactable = false;
            }
            else
            {
                if (blePanel.gameObject.activeInHierarchy)
                {
                    blePanel.gameObject.SetActive(true);
                }
                bluetoothButton.onClick.AddListener(blePanel.ShowHidePanel);
            }


            calibrationButton.onClick.AddListener(calibrationManager.ResetCalibration);
            calibrationButton.onClick.AddListener(WeArtController.Instance.ResetCalibration);
            actuationButton.onClick.AddListener(actuationPanel.ShowHidePanel);
            statusButton.onClick.AddListener(statusPanel.ShowHidePanel);
        }
    }
}
