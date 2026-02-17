using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using UnityEngine;
using UnityEngine.Events;
using WeArt.Core;
using WeArt.Messages;

namespace WeArt.Components
{
    /// <summary>
    /// MiddlewareStatusData — struct to keep the received data from MW_STATUS and DEVICES_STATUS messages for further use in publc events.
    /// </summary>
    public struct MiddlewareStatusData
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MiddlewareStatus Status { get; set; }
        public string Version { get; set; }
        public int StatusCode { get; set; }
        public string ErrorDesc { get; set; }
        public bool ActuationsEnabled { get; set; }
        public List<DeviceStatusData> Devices { get; set; }

        public static MiddlewareStatusData Empty => new MiddlewareStatusData()
        {
            Status = MiddlewareStatus.DISCONNECTED,
            ActuationsEnabled = false,
            StatusCode = 0,
            ErrorDesc = "",
            Version = "",
            Devices = new List<DeviceStatusData>(),
        };
    }
    
    /// <summary>
    /// Receives and notifies of middleware and connected devices status changes
    /// </summary>
    public class WeArtStatusTracker : MonoBehaviour
    {
        [Serializable]
        public class StatusTrackingEvent : UnityEvent<MiddlewareStatusData> { }
        
        [Serializable]
        public class WeartAppStatusTrackingEvent : UnityEvent<WeartAppStatusMessage> { }
        
        [Serializable]
        public class TouchDiverProStatusTrackingEvent : UnityEvent<TouchDiverProStatus> { }
        
        /// <summary>
        /// Event fired when the middleware status or TD devices changes
        /// </summary>
        public StatusTrackingEvent OnMiddlewareStatus;
        /// <summary>
        /// Event invoked when the weart application status changes
        /// </summary>
        public WeartAppStatusTrackingEvent OnWeartAppStatus;
        /// <summary>
        /// Event invoked when the weart application updates TD_Pro status
        /// </summary>
        public TouchDiverProStatusTrackingEvent OnTouchDiverProStatus;
        
        private MiddlewareStatusData _currentMiddlewareStatus;

        /// <summary>
        /// Delegate for Connected Devices
        /// </summary>
        /// <typeparam name="ConnectedDevices"></typeparam>
        /// <param name="e"></param>
        public delegate void dConnectedDevices<ConnectedDevices>(ConnectedDevices e);
        /// <summary>
        /// Event for handling connected devices from middleware
        /// </summary>
        public static event dConnectedDevices<ConnectedDevices> ConnectedDevicesReady;
        
        private bool _receivedNewConnectedDeviceData = false;

        private List<ITouchDiverData> _connectedDevices;

        private MiddlewareStatus _currentStatus = MiddlewareStatus.DISCONNECTED;
        
        private void Init()
        {
            if (WeArtController.Instance is null)
                return;

            var client = WeArtController.Instance.Client;
            client.OnConnectionStatusChanged -= OnConnectionChanged;
            client.OnConnectionStatusChanged += OnConnectionChanged;
            client.OnMessage -= OnMessageReceived;
            client.OnMessage += OnMessageReceived;
            
        }

        private void Update()
        {
            if (_receivedNewConnectedDeviceData) GenerateEventConnectedDevices();
        }
        
        private void OnEnable()
        {
            Init();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gameObject.scene.IsValid())
                Init();
        }
#endif

        internal void OnConnectionChanged(bool connected)
        {
            if (connected) return;
            
            _currentMiddlewareStatus = MiddlewareStatusData.Empty;
        }

        private void OnMessageReceived(WeArtClient.MessageType type, IWeArtMessage message)
        {
            if (message is MiddlewareStatusMessage mwStatusMessage)
            {
                FulfillCurrentStatusWithMwData(mwStatusMessage);
                GenerateInternalMessages();
                return;
            }
                
            if (message is DevicesStatusMessage devicesStatusMessage)
            {
                FulfillCurrentStatusWithTdDeviceStatus(devicesStatusMessage);
                GenerateInternalMessages();
                
                return;
            }

            if (message is WeartAppStatusMessage waStatusMessage)
            {
                _currentStatus = waStatusMessage.Status;
                OnWeartAppStatus?.Invoke(waStatusMessage);
                _receivedNewConnectedDeviceData = true;
                return;
            }

            if (message is TouchDiverProStatus tdProStatus)
            {
                OnTouchDiverProStatus?.Invoke(tdProStatus);
                _connectedDevices = tdProStatus.Devices.Cast<ITouchDiverData>().ToList();
                _receivedNewConnectedDeviceData = true;
                return;
            }
        }

        private void GenerateInternalMessages()
        {
            OnMiddlewareStatus?.Invoke(_currentMiddlewareStatus);
        }

        /// <summary>
        /// Shows/Hides this panel
        /// </summary>
        public void ShowHidePanel()
        {
            gameObject.SetActive(!isActiveAndEnabled);
        }

        /// <summary>
        /// Method to generate event on Connected Devices change
        /// </summary>
        /// <param name="status"></param>
        private void GenerateEventConnectedDevices()
        {
            if (_connectedDevices == null) return;
            
            ConnectedDevices connectedDevices = new ConnectedDevices(_connectedDevices, _currentStatus == MiddlewareStatus.RUNNING);
            ConnectedDevicesReady?.Invoke(connectedDevices);

            _receivedNewConnectedDeviceData = false;
        }


        private void FulfillCurrentStatusWithMwData(MiddlewareStatusMessage mwStatusMessage)
        {
            _currentStatus = mwStatusMessage.Status;
            _currentMiddlewareStatus.Status = mwStatusMessage.Status;
            _currentMiddlewareStatus.Version = mwStatusMessage.Version;
            _currentMiddlewareStatus.ActuationsEnabled = mwStatusMessage.ActuationsEnabled;
            _currentMiddlewareStatus.StatusCode = mwStatusMessage.StatusCode;
            _currentMiddlewareStatus.ErrorDesc = mwStatusMessage.ErrorDesc;
            _receivedNewConnectedDeviceData = true;
        }

        private void FulfillCurrentStatusWithTdDeviceStatus(DevicesStatusMessage devicesStatusMessage)
        {
            _currentMiddlewareStatus.Devices = devicesStatusMessage.Devices.ToList();
            _connectedDevices = devicesStatusMessage.Devices.Cast<ITouchDiverData>().ToList();
            _receivedNewConnectedDeviceData = true;
        }
    }
}