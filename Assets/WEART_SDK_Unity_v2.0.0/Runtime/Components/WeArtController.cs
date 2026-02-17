using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Core;
using WeArt.Messages;
using WeArt.Utils;
using ClientError = WeArt.Core.WeArtClient.ErrorType;
using DeviceID = WeArt.Core.DeviceID;
using PackTag = WeArt.Utils.LogEnums.PackTag;
using LogLevel = WeArt.Utils.LogEnums.LogLevel;
using WeArt.Bluetooth;
using WeArt.TouchUI;

namespace WeArt.Components
{
    /// <summary>
    /// This component wraps and exposes the network client that communicates with the hardware middleware.
    /// Use the <see cref="Instance"/> singleton property to retrieve the instance.
    /// </summary>
    public class WeArtController : MonoBehaviour
    {
        private static WeArtController _instance;

        private WeArtHandController _weArtHandControllerRight;
        private WeArtHandController _weArtHandControllerLeft;

        private WeArtHapticObject[] _weArtHapticObjects;

        #region STANDALONE

        [SerializeField]
        internal WeArtDevice _deviceFirst;
        [SerializeField]
        internal WeArtDevice _deviceSecond;

        [SerializeField]
        internal LogLevel _projectLogLevel;

        internal BluetoothManager _bluetoothManager { get; private set; }
        internal BleConnectionPanel _bleConnectionPanel { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        private float _scannerStartDelayTime = 2f;
#endif
        private CalibrationManager _calibrationManager;
        internal WeArtTrackingCalibration _weArtTrackingCalibration;
        
        public bool IsCalibrated { get; internal set; } = false;
        
        #endregion

        [SerializeField] internal bool _allowGestures;
        
        [SerializeField]
        internal int _clientPort = 13031;

        [SerializeField]
        internal bool _startAutomatically = false;

        [SerializeField]
        internal bool _startCalibrationAutomatically = false;

        [SerializeField]
        internal bool _startRawDataAutomatically = false;

        [SerializeField]
        internal bool _debugMessages = false;

        [SerializeField] 
        internal DeviceGeneration _deviceGeneration;

        [NonSerialized]
        private WeArtClient _weArtClient;

        [NonSerialized]
        private bool _starting = false;

        [NonSerialized]
        private bool _startFromEditor = false;

        private static bool _variablesAssigned = false;
        private static bool _clientsStarted = false;
        
        /// <summary>
        /// The singleton instance of <see cref="WeArtController"/>
        /// </summary>
        public static WeArtController Instance
        {
            private set => _instance = Instance;
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<WeArtController>();
                return _instance;
            }
        }

        /// <summary>
        /// Public property about Device Generation G1 and G2 capabilities
        /// </summary>
        public DeviceGeneration DeviceGeneration
        {
            get { return _deviceGeneration; }
            set { _deviceGeneration = value; }
        }

        /// <summary>
        /// The network client that communicates with the hardware middleware.
        /// </summary>
        public WeArtClient Client
        {
            get
            {
                if (_weArtClient == null)
                {
#if UNITY_EDITOR || PLATFORM_STANDALONE_WIN
                    _weArtClient = new WeArtClient
                    {
                        IpAddress = WeArtNetwork.LocalIPAddress,
                        Port = _clientPort,
                    };
#elif UNITY_ANDROID && !UNITY_EDITOR
                    _weArtClient = new WeArtClientBLE { };
#endif
                    _weArtClient.OnConnectionStatusChanged += OnConnectionChanged;
                    _weArtClient.OnTextMessage += OnTextMessage;
                    _weArtClient.OnMessage += OnMessage;
                    _weArtClient.OnError += OnError;
                }
                return _weArtClient;
            }
        }
        
        private void Awake()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }
        
        private void Start()
        {
            StartClients();
        }

        /// <summary>
        /// OnDestroy do different stuff basing on the architecture where is running
        /// </summary>
        private void OnDestroy()
        {
            /*
#if UNITY_EDITOR || PLATFORM_STANDALONE_WIN
            Client.Stop();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
            DisconnectAllDevices();
            WeArtLog.StopLogging();
            //Stop any connection
#endif
            */
        }

        private void OnApplicationQuit()
        {
#if UNITY_EDITOR || PLATFORM_STANDALONE_WIN
            Client.Stop();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            DisconnectAllDevices();
            //Stop and release any device
            WeArtLog.StopLogging();
#endif
        }


#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnApplicationPause(bool pauseStatus)
        {
            WeArtLog.LogPauseHandler(pauseStatus);
        }
#endif

        public void OnSceneChanged(Scene current, Scene next)
        {
            AssignVariablesFromScene();
        }

        private void AssignVariablesFromScene()
        {
            if (_instance == null)
            {
                _instance = this;
            }  else if (_instance != this)
            {
                Destroy(this);
                return;
            }

            GetAndAssignHandControllers();
            
            if (_variablesAssigned) return;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            WeArtLog.StartLogging(_projectLogLevel);
            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, "Application is started...");
            _startAutomatically = true;
            
            _calibrationManager = FindObjectOfType<CalibrationManager>();
            _weArtTrackingCalibration = GetComponent<WeArtTrackingCalibration>();
            _bleConnectionPanel = FindObjectOfType<BleConnectionPanel>();

            _weArtTrackingCalibration._OnCalibrationResultSuccess.RemoveListener(ConfirmCalibration);
            _weArtTrackingCalibration._OnCalibrationResultSuccess.AddListener(ConfirmCalibration);
#endif
            _calibrationManager = FindObjectOfType<CalibrationManager>();
            _weArtTrackingCalibration = GetComponent<WeArtTrackingCalibration>();
            _weArtTrackingCalibration._OnCalibrationResultSuccess.RemoveListener(ConfirmCalibration);
            _weArtTrackingCalibration._OnCalibrationResultSuccess.AddListener(ConfirmCalibration);
            
            _variablesAssigned = true;
        }
        
        public void AfterConnectionSuccessful()
        {
            if (_startCalibrationAutomatically)
            {
                StartCalibration();
            }

            if (_startRawDataAutomatically)
            {
                StartRawData();
            }
        }

        private void OnConnectionChanged(bool connected)
        {
            WeArtLog.Log("Connected: " + connected.ToString());
            if (connected)
            {
                WeArtLog.Log($"Connected to {Client.IpAddress}.");
                
                if (_startAutomatically || _startFromEditor)
                {
                    StartMiddleware(TrackingType.WEART_HAND);
                }

                _startFromEditor = false;
            }
            else
            {
                WeArtLog.Log($"Disconnected from {Client.IpAddress}.");
                _starting = false;
            }
        }

        public void StartFromEditor()
        {
            _startFromEditor = true;
            Client.Start();
        }

        public void StartMiddleware(TrackingType trackingType)
        {
            _starting = true;
            Client.SendStartDevice(trackingType);
            Client.SendMessage(new GetMiddlewareStatusMessage());
            Client.SendMessage(new GetDevicesStatusMessage());
        }

        /// <summary>
        /// Start other procedures after the session is started correctly
        /// </summary>
        private void AfterStartSuccessful()
        {
            if (_startCalibrationAutomatically)
            {
                Client.StartCalibration();
            }
                

            if (_startRawDataAutomatically)
                Client.StartRawData();
        }

        public void StartCalibration()
        {
            Client.StartCalibration();
        }

        public void StopCalibration()
        {
            Client.StopCalibration();
        }

        public void StartRawData()
        {
            Client.StartRawData();
        }

        public void StopRawData()
        {
            Client.StopRawData();
        }

        /// <summary>
        /// Gets the assigned WeArtHandController.
        /// </summary>
        /// <param name="handSide"></param>
        /// <returns></returns>
        public WeArtHandController GetHandController(HandSide handSide)
        {
            return handSide == HandSide.Right ? _weArtHandControllerRight : _weArtHandControllerLeft;
        }
        
        private void OnTextMessage(WeArtClient.MessageType type, string message)
        {
            if (!_debugMessages)
                return;

            if (type == WeArtClient.MessageType.MessageSent)
                WeArtLog.Log($"To Middleware: \"{message}\"");

            else if (type == WeArtClient.MessageType.MessageReceived)
                WeArtLog.Log($"From Middleware: \"{message}\"");
        }

        private void OnMessage(WeArtClient.MessageType messageType, IWeArtMessage message)
        {
            // Check that the middleware is in running state and has no errors,
            // otherwise log the error

            if (!_starting) return;
            if (messageType != WeArtClient.MessageType.MessageReceived) return;

            if (message is MiddlewareStatusMessage statusMessage)
            {
                MiddlewareStatus status = statusMessage.Status;
                bool isError = statusMessage.StatusCode != 0;
                if (status == MiddlewareStatus.RUNNING)
                {
                    _starting = false;
                    AfterStartSuccessful();
                }
                else if (isError)
                {
                    // Error while starting, log it
                    WeArtLog.Log($"Error while starting session (status code {statusMessage.StatusCode}): {statusMessage.ErrorDesc}", LogType.Error);
                    _starting = false;
                }
            }

            if (message is WeartAppStatusMessage waStatusMessage)
            {
                var status = waStatusMessage.Status;
                bool isError = waStatusMessage.StatusCode != 0;
                if (status == MiddlewareStatus.RUNNING)
                {
                    _starting = false;
                    AfterStartSuccessful();
                } else if (isError)
                {
                    WeArtLog.Log($"Error while starting session (status code {waStatusMessage.StatusCode}): {waStatusMessage.ErrorDesc}", LogType.Error);
                    _starting = false;
                }
            }
        }

        private void OnError(ClientError error, Exception exception)
        {
            string errorMessage;
            switch (error)
            {
                case ClientError.ConnectionError:
                    errorMessage = $"Cannot connect to {Client.IpAddress}";
                    break;
                case ClientError.SendMessageError:
                    errorMessage = $"Error on send message";
                    break;
                case ClientError.ReceiveMessageError:
                    errorMessage = $"Error on message received";
                    break;
                default:
                    throw new NotImplementedException();
            }
            WeArtLog.Log($"{errorMessage}\n{exception.StackTrace}", LogType.Error);
        }


        /// <summary>
        /// Resets calibration.
        /// </summary>
        public void ResetCalibration()
        {
            IsCalibrated = false;
            Client.ResetCalibration();
            Client.ResetHandClosure();
            DisableAllThimbleHaptics();
        }

        /// <summary>
        /// Methods to call when calibration is finished successfully. 
        /// </summary>
        /// <param name="handSide"></param>
        private void ConfirmCalibration(HandSide handSide)
        {
            EnableHandThimbleHaptics(handSide);
            IsCalibrated = true;
        }

        private void ConnectedDevicesReady(ConnectedDevices e)
        {
            UpdateConnectedHandsDataForCalibrationPC(e);
        }

        /// <summary>
        /// Sets data regarding connected devices to the calibration manager. 
        /// </summary>
        private void UpdateConnectedHandsDataForCalibrationPC(ConnectedDevices e)
        {
            StartCoroutine(UpdateConnectedHandsDataForCalibrationCor(e));
        }

        private IEnumerator UpdateConnectedHandsDataForCalibrationCor(ConnectedDevices e)
        {
            yield return new WaitForSeconds(1f);

            if (!_calibrationManager) yield break;
            
            if (!e.MiddlewareRunning)
            {
                ResetCalibrationAndHandClosure();
                _calibrationManager.SetHandDominationData(0);              
                yield break;
            }

            if (!IsCalibrated)
            {
                ResetCalibrationAndHandClosure();

                if (e.Devices.Count < 1)
                {
                    _calibrationManager.SetHandDominationData(0);
                    yield break;
                }

                if (e.Devices.Count > 1) 
                {
                    _calibrationManager.SetHandDominationData(2);
                    yield break;
                }
                
                if (e.Devices[0].HandSide == HandSide.Right)
                {
                    _calibrationManager.SetHandDominationData(1);
                    yield break;
                }

                _calibrationManager.SetHandDominationData(1, (CalibrationManager.HandType)HandSide.Left);
            }
        }

        /// <summary>
        /// Resets the Calibration Process in all related components.
        /// </summary>
        private void ResetCalibrationAndHandClosure()
        {
            DisableAllThimbleHaptics();
            _calibrationManager.ResetCalibration();
            Client.ResetHandClosure();
        }
        
        /// <summary>
        /// Enables the thimble haptics at the hand.
        /// </summary>
        /// <param name="hand"></param>
        private void EnableHandThimbleHaptics(HandSide hand)
        {
            if (_weArtHandControllerLeft)
            {
                _weArtHandControllerLeft.EnableThimbleHaptic(true);
            }
        
            if (_weArtHandControllerRight) 
            {
                _weArtHandControllerRight.EnableThimbleHaptic(true);
                return;
            }

            EnableActuationAllHapticObjectsAtScene(true);
        }

        /// <summary>
        /// Disables thimble haptics at both hands.
        /// </summary>
        private void DisableAllThimbleHaptics()
        {
            if (_weArtHandControllerLeft) _weArtHandControllerLeft.EnableThimbleHaptic(false);
            if (_weArtHandControllerRight)
            {
                _weArtHandControllerLeft.EnableThimbleHaptic(false);
                return;
            }
            
            EnableActuationAllHapticObjectsAtScene(false);
        }

        /// <summary>
        /// Enables/Disbales Actuation of all WeArtHapticObjects at Scene.
        /// </summary>
        /// <param name="enable"></param>
        private void EnableActuationAllHapticObjectsAtScene(bool enable)
        {
            foreach (var haptic in _weArtHapticObjects)
            {
                haptic.EnablingActuating(enable);
            }
        }

        /// <summary>
        /// Gets TrackingType message depending on connected device generation.
        /// </summary>
        /// <returns></returns>
        private TrackingType GetTrackingType(DeviceGeneration deviceGeneration)
        {
            switch (deviceGeneration)
            {
                case DeviceGeneration.TD:
                    return TrackingType.WEART_HAND;
                case DeviceGeneration.TD_Pro:
                    return TrackingType.WEART_HAND_G2;
                default:
                    return TrackingType.DEFAULT;
            }
        }

        private void StartClients()
        {
            if (_clientsStarted) return;
            
#if UNITY_EDITOR || PLATFORM_STANDALONE_WIN

            Client.Start();

            WeArtStatusTracker.ConnectedDevicesReady += ConnectedDevicesReady;
            
#elif UNITY_ANDROID && !UNITY_EDITOR
            Client.Start();
            
            InitBleManager();
            EnableScanAtStartWithDelay(_scannerStartDelayTime);
            
            WeArtLog.Log($"AAR Version: {_bluetoothManager.GetPluginVersion()}\n\n");
#endif

            _clientsStarted = true;
        }
        
        #region BLE methods

        /// <summary>
        /// Enables or Disables the scanner for BLE devices.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableScan(bool enable)
        {
            var onOrOff = enable ? "ON" : "OFF";

            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"BLE Scanner {onOrOff}");

            if (enable)
            {
                _bluetoothManager.StartScanning(DeviceFoundHandler, DeviceRemovedHandler);
                return;
            }

            _bluetoothManager.StopScanning();
        }

        /// <summary>
        /// Enables or Disables Run mode to connected devices.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableRunMode(bool enable)
        {
            _deviceSecond.RunDevice(enable);
            _deviceFirst.RunDevice(enable);
        }

        /// <summary>
        /// Disconnects all connected TouchDIVER devices.
        /// </summary>
        public void DisconnectAllDevices()
        {
            _deviceSecond.DisconnectDevice();
            UpdateDeviceStatus(_deviceSecond.BleDevice);
            _deviceFirst.DisconnectDevice();
            UpdateDeviceStatus(_deviceFirst.BleDevice);
        }

        /// <summary>
        /// Force update of the TD device status on Connection Management Panel.
        /// </summary>
        /// <param name="device"></param>
        public void UpdateDeviceStatus(BleDevice device)
        {
            _bleConnectionPanel.UpdateCurrentDeviceStatus(device);
        }

        /// <summary>
        /// Connects TD device to WeArtTouchDiver with exact hand side.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="handSide"></param>
        public void ConnectDevice(BleDevice device, HandSide handSide)
        {
            if (_deviceFirst.BleDevice == null)
            {
                _deviceFirst.ConnectDevice(device);
                _deviceFirst._handSide = handSide;

                return;
            }
            _deviceSecond.ConnectDevice(device);
            _deviceSecond._handSide = handSide;

        }

        /// <summary>
        /// Disconnect BLE Device.
        /// </summary>
        /// <param name="device"></param>
        public void DisconnectDevice(BleDevice device)
        {
            if (_deviceFirst.BleDevice != null && _deviceFirst.BleDevice.DeviceMacAddress.Equals(device.DeviceMacAddress))
            {
                WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"Disconnecting device  {device.DeviceMacAddress}", DefineDeviceID(device));
                _deviceFirst.DisconnectDevice();
                UpdateDeviceStatus(device);
                return;
            }

            if (_deviceSecond.BleDevice != null && _deviceSecond.BleDevice.DeviceMacAddress.Equals(device.DeviceMacAddress))
            {
                WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"Disconnecting device  {device.DeviceMacAddress}", DefineDeviceID(device));
                _deviceSecond.DisconnectDevice();
                UpdateDeviceStatus(device);
                return;
            }

            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS,
                $"Tried to disconnect device {device.DeviceMacAddress}, but it didn't connected to TD in Project");
        }


        internal void SendMessage(IWeArtMessage weArtMessage)
        {
            _weArtClient.SendMessage(weArtMessage);
        }

        /// <summary>
        /// Gets BleDevice by DeviceID.
        /// </summary>
        /// <param name="deviceID"></param>
        /// <returns></returns>
        public BleDevice GetBleDeviceByID(DeviceID deviceID)
        {
            return _deviceFirst._deviceID == deviceID ? _deviceFirst.BleDevice : _deviceSecond.BleDevice;
        }

        /// <summary>
        /// Initialises the BluetoothManager and registers the methods to handle   
        /// </summary>
        private void InitBleManager()
        {
            _bluetoothManager = new BluetoothManager(
                DeviceConnectionHandler,
                DeviceReadyHandler,
                DeviceDisconnectionHandler,
                ErrorReceivedHandler);

            _deviceFirst.SetBluetoothManager(_bluetoothManager);
            _deviceSecond.SetBluetoothManager(_bluetoothManager);
            _bleConnectionPanel.SetBluetoothManager(_bluetoothManager);
        }

        /// <summary>
        /// Enables scan with delay to be sure that all Android permissions are received.
        /// </summary>
        /// <param name="delay"></param>
        private void EnableScanAtStartWithDelay(float delay)
        {
            StartCoroutine(EnableScanAtStartWithDelayCor(delay));
        }

        private IEnumerator EnableScanAtStartWithDelayCor(float delay)
        {
            yield return new WaitForSeconds(delay);

            EnableScan(true);
        }

        /// <summary>
        /// Define DeviceID of BleDevice.
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private DeviceID DefineDeviceID(BleDevice device)
        {
            if (_deviceFirst.BleDevice != null && _deviceFirst.BleDevice.DeviceMacAddress.Equals(device.DeviceMacAddress)) return DeviceID.First;
            if (_deviceSecond.BleDevice != null && _deviceSecond.BleDevice.DeviceMacAddress.Equals(device.DeviceMacAddress)) return DeviceID.Second;

            return DeviceID.None;

        }
        
        /// <summary>
        /// Sets data regarding connected devices to the calibration manager. 
        /// </summary>
        private void UpdateConnectedHandsDataInCalibrationManager()
        {
            StartCoroutine(UpdateConnectedHandsDataInCalibrationManagerCor());
        }
          
        private IEnumerator UpdateConnectedHandsDataInCalibrationManagerCor()
        {
            yield return new WaitForSeconds(2f);

            if (!_calibrationManager) yield break;

            if (_bluetoothManager.ConnectedDevices.Count < 1)
            {
                _calibrationManager.SetHandDominationData(0);
                yield break;
            }

            if (_bluetoothManager.ConnectedDevices.Count > 1) {
                    _calibrationManager.SetHandDominationData(2);
                    yield break;
            }

            if (_deviceFirst.BleDevice == null)
            {
                _calibrationManager.SetHandDominationData(1, (CalibrationManager.HandType)_deviceSecond._handSide);
                yield break;
            }

            _calibrationManager.SetHandDominationData(1, (CalibrationManager.HandType)_deviceFirst._handSide);
        }

        /// <summary>
        /// Gets and assigns the WeArtHandController components from Scene.
        /// </summary>
        private void GetAndAssignHandControllers()
        {
            WeArtHandController[] controllers = FindObjectsOfType<WeArtHandController>();

            foreach (var item in controllers)
            {
                if (item._handSide == HandSide.Right) _weArtHandControllerRight = item;
                if (item._handSide == HandSide.Left) _weArtHandControllerLeft = item;
            }

            if (!_weArtHandControllerRight && !_weArtHandControllerLeft) _weArtHapticObjects = FindObjectsOfType<WeArtHapticObject>();
        }
        
        #region BLE Handlers

        /// <summary>
        /// Methods that will be invoked when device sends the error event.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="errorMessage"></param>
        private void ErrorReceivedHandler(int errorCode, string errorMessage)
        {
            StartCoroutine(ErrorReceivedHandlerCor(errorCode, errorMessage));
        }

        private IEnumerator ErrorReceivedHandlerCor(int errorCode, string errorMessage)
        {
            yield return new WaitForSeconds(1.5f);

            switch (errorCode)
            {
                case 1:
                    WeArtLog.LogFile(LogLevel.ERROR, PackTag.EVENTS, "TURN ON THE BLE AND START APP AGAIN.");

                    yield return new WaitForSeconds(2f);

                    Application.Quit();
                    break;
                default:
                    {
                        var macAddress = _bluetoothManager.ExtractMacAddressFromMessage(errorMessage);

                        if (!String.IsNullOrEmpty(macAddress))
                        {
                            var device = _bluetoothManager.GetDeviceFromScannedDevices(macAddress);

                            _bleConnectionPanel.CheckConnectionStop(device);
                            _bleConnectionPanel.UpdateCurrentDeviceStatus(device);

                            WeArtLog.LogFile(LogLevel.ERROR, PackTag.RES, errorCode + " " + errorMessage, DefineDeviceID(device));
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// Methods that will be invoked when device sends the disconnection event.
        /// </summary>
        /// <param name="device">Device sent the event</param>
        private void DeviceDisconnectionHandler(BleDevice device)
        {
            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"Device {device.DeviceMacAddress} disconnected.", DefineDeviceID(device));

            _bleConnectionPanel.OnDisconnectedDevice(device);

            if (_startCalibrationAutomatically)
            {
                ResetCalibration();
                if (_bluetoothManager.ConnectedDevices.Count > 0)
                {
                    AfterConnectionSuccessful();
                }
                UpdateConnectedHandsDataInCalibrationManager();
            }
            else
            {
                ResetCalibration();
                UpdateConnectedHandsDataInCalibrationManager();
            }

            EnableScan(true);
        }

        /// <summary>
        /// Methods that will be invoked when device sends the ready to work event (after connection).
        /// </summary>
        /// <param name="device"></param>
        private void DeviceReadyHandler(BleDevice device)
        {
            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"Device {device.DeviceMacAddress} is ready.", DefineDeviceID(device));

            _bleConnectionPanel.OnReadyDevice(device);
            StartCoroutine(ConnectConfirmAndRunDevice(device));
        }

        /// <summary>
        /// Methods that will be invoked when device sends the connection event.
        /// </summary>
        /// <param name="device">Device sent the event</param>
        private void DeviceConnectionHandler(BleDevice device)
        {
            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"Device {device.DeviceMacAddress} connected.", DefineDeviceID(device));

            UpdateConnectedHandsDataInCalibrationManager();
            ResetCalibration();
            _bleConnectionPanel.OnConnectionDevice(device);
        }

        /// <summary>
        /// Methods that will be invoked when ble scanner sends the event that TD device is found.
        /// </summary>
        /// <param name="device">Device sent the event</param>
        private void DeviceFoundHandler(BleDevice device)
        {
            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"Scanner found TD device {device.DeviceMacAddress}");

            _bleConnectionPanel.OnFoundDevice(device);
        }

        /// <summary>
        /// Methods that will be invoked when ble scanner sends the event that TD device is not available for connection anymore.
        /// </summary>
        /// <param name="device">Device sent the event</param>
        private void DeviceRemovedHandler(BleDevice device)
        {
            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"Device removed from scanner {device.DeviceMacAddress}");

            _bleConnectionPanel.OnRemovedDevice(device);
        }

        private IEnumerator ConnectConfirmAndRunDevice(BleDevice device)
        {
            WaitForSeconds waiter = new WaitForSeconds(0.25f);

            yield return waiter;

            _bluetoothManager.SendData(device, DeviceCommands.CONNECT_CONFIRM);

            yield return waiter;
        }

        #endregion

        #endregion
    }
}