using TMPro;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;

namespace WeArt.Utils
{
    public class WeArtStatusDisplay : MonoBehaviour
    {
        [SerializeField] internal WeArtStatusTracker tracker;
        [SerializeField] internal TextMeshProUGUI MiddlewareStatusText;
        [SerializeField] internal TextMeshProUGUI MiddlewareStatusCodeText;
        [SerializeField] internal TextMeshProUGUI MiddlewareVersionText;
        [SerializeField] internal TextMeshProUGUI ConnectionTypeText;
        [SerializeField] internal TextMeshProUGUI ErrorDescriptionText;
        [SerializeField] internal GameObject ErrorDescriptionPanel;
        [SerializeField] internal GameObject ActuationEnabledIcon;
        [SerializeField] internal GameObject AutoConnectionIcon;
        [SerializeField] internal GameObject RawDataLogIcon;
        [SerializeField] internal GameObject SensorOnMaskIcon;
        [SerializeField] internal TextMeshProUGUI DeviceGeneration;
        
        private bool _weartAppConnected = false;
        private MiddlewareStatusData _currentMiddleWareStatus = MiddlewareStatusData.Empty;
        private WeartAppStatusMessage _currentWeartAppStatus;
        private bool StandaloneAndroidActive = false;
        private bool _middlewareStatusReceived;
        private bool _weartAppStatusReceived;
        private bool _needToUpdate;
        
        private void OnEnable()
        {
            Init();
        }

        private void Start()
        {
            DisableIcons();
            DeviceGeneration.text = WeArtController.Instance.DeviceGeneration == Core.DeviceGeneration.TD ? "TD" : "TD Pro";
        }
        
        private void Update()
        {
            UpdateDisplay();
        }

        private void Init()
        {
            tracker.OnMiddlewareStatus.RemoveListener(OnMiddlewareStatus);
            tracker.OnMiddlewareStatus.AddListener(OnMiddlewareStatus);
            tracker.OnWeartAppStatus.RemoveListener(OnWeartAppStatus);
            tracker.OnWeartAppStatus.AddListener(OnWeartAppStatus);
            
            if (WeArtController.Instance is null)
                return;

            WeArtController.Instance.Client.OnConnectionStatusChanged += OnConnectionChanged;

            // Force Connected value to true when targeting ANDROID architecture
#if UNITY_ANDROID && !UNITY_EDITOR
            WriteInitialInfoOnUI();
            StandaloneAndroidActive = true;
#endif
        }

        
        private void OnConnectionChanged(bool connected)
        {
            _weartAppConnected = connected;
            _needToUpdate = true;
        }

        public void WriteInitialInfoOnUI()
        {
            _currentMiddleWareStatus.Version = WeArtConstants.WEART_SDK_VERSION;
            MiddlewareVersionText.text = _currentMiddleWareStatus.Version;
        }

        private void OnMiddlewareStatus(MiddlewareStatusData newStatusData)
        {
            _currentMiddleWareStatus = newStatusData;
            _middlewareStatusReceived = true;
            _needToUpdate = true;
        }

        private void OnWeartAppStatus(WeartAppStatusMessage newStatusData)
        {
            _currentWeartAppStatus = newStatusData;
            _needToUpdate = true;
            _weartAppStatusReceived = true;
        }

        private void UpdateDisplay()
        {
            if (!_needToUpdate) return;
            
            if (_middlewareStatusReceived) UpdateDisplayWithMiddlewareData();
            if (_weartAppStatusReceived) UpdateDisplayWithWeartAppData();
        }
        
        private void UpdateDisplayWithMiddlewareData()
        {
            bool isOk = _currentMiddleWareStatus.StatusCode == 0;
            _weartAppConnected = StandaloneAndroidActive || _weartAppConnected;

            MiddlewareStatusText.text = _weartAppConnected ? _currentMiddleWareStatus.Status.ToString() : "DISCONNECTED";
            MiddlewareStatusText.color = MiddlewareStatusColor(_currentMiddleWareStatus.Status);
           
            MiddlewareVersionText.text = StandaloneAndroidActive ? WeArtConstants.WEART_SDK_VERSION : _currentMiddleWareStatus.Version;
            
            MiddlewareStatusCodeText.text = isOk ? "OK" : "[" + _currentMiddleWareStatus.StatusCode + "] ";
            MiddlewareStatusCodeText.color = isOk ? Color.green : Color.red;
            
            ErrorDescriptionText.text = isOk ? "-" : "[" + _currentMiddleWareStatus.StatusCode + "] " + _currentMiddleWareStatus.ErrorDesc; 
            
            ConnectionTypeText.text = _weartAppConnected ? ConnectionType.BLE.ToString() : "-";
            
            ActuationEnabledIcon.SetActive(_currentMiddleWareStatus.ActuationsEnabled);
            
            if (SensorOnMaskIcon.activeSelf) SensorOnMaskIcon.SetActive(false);
            if (AutoConnectionIcon.activeSelf) AutoConnectionIcon.SetActive(false);
            if (RawDataLogIcon.activeSelf) RawDataLogIcon.SetActive(false);

            _needToUpdate = false;
        }
        
        private void UpdateDisplayWithWeartAppData()
        {
            bool isOk = _currentWeartAppStatus.StatusCode == 0;
            _weartAppConnected = StandaloneAndroidActive || _weartAppConnected;

            MiddlewareStatusText.text = _weartAppConnected ? _currentWeartAppStatus.Status.ToString() : "DISCONNECTED";
            MiddlewareStatusText.color = MiddlewareStatusColor(_currentWeartAppStatus.Status);
           
            MiddlewareVersionText.text = StandaloneAndroidActive ? WeArtConstants.WEART_SDK_VERSION : _currentWeartAppStatus.Version;
            
            MiddlewareStatusCodeText.text = isOk ? "OK" : "[" + _currentWeartAppStatus.StatusCode + "] ";
            MiddlewareStatusCodeText.color = isOk ? Color.green : Color.red;
            
            ErrorDescriptionText.text = isOk ? "-" : "[" + _currentWeartAppStatus.StatusCode + "] " + _currentWeartAppStatus.ErrorDesc; 
            
            ConnectionTypeText.text = _weartAppConnected ? _currentWeartAppStatus.ConnectionType.ToString() : "-";
            
            ActuationEnabledIcon.SetActive(_currentWeartAppStatus.ActuationsEnabled);
            SensorOnMaskIcon.SetActive(_currentWeartAppStatus.SensorOnMask);
            AutoConnectionIcon.SetActive(_currentWeartAppStatus.AutoConnection);
            RawDataLogIcon.SetActive(_currentWeartAppStatus.RawDataLog);

            _needToUpdate = false;
        }
        
        private Color MiddlewareStatusColor(MiddlewareStatus status)
        {
            bool isOk = _weartAppConnected && status == MiddlewareStatus.RUNNING;
            bool isWarning = status != MiddlewareStatus.RUNNING && status != MiddlewareStatus.DISCONNECTED;

            return isOk ? Color.green : (isWarning ? Color.yellow : Color.red);
        }

        private void DisableIcons()
        {
            ActuationEnabledIcon.SetActive(false);
            SensorOnMaskIcon.SetActive(false);
            AutoConnectionIcon.SetActive(false);
            RawDataLogIcon.SetActive(false);
        }
    }
}