using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WeArt.Utils;

namespace WeArt.Bluetooth
{
    /// <summary>
    /// BluetoothManager — is a main class for BLE interactions. 
    /// </summary>
    public class BluetoothManager
    {
        public bool IsScanning { get; private set; }

        /// <summary>
        /// List of devices that were scanned by BluetoothManager within current Application session.
        /// </summary>
        public List<BleDevice> ListOfScannedDevices { get; } = new List<BleDevice>();
            
        /// <summary>
        /// List of connected BleDevices.
        /// </summary>
        public List<BleDevice> ConnectedDevices { get; } = new List<BleDevice>();

        /// <summary>
        /// Event when Device status is changing
        /// </summary>
        public delegate void dDeviceStatus<DeviceStatusInfo>(DeviceStatusInfo deviceStatusInfo);
        public event dDeviceStatus<DeviceStatusInfo> DeviceStatusInfoReady;

        public class DeviceStatusInfo : EventArgs
        {
            public string MacAddress { get; set; }
            public DeviceStatus DeviceStatus { get; set; }

            public DeviceStatusInfo(string macAddress, DeviceStatus deviceStatus)
            {
                MacAddress = macAddress;
                DeviceStatus = deviceStatus;
            }
        }

        /// <summary>
        /// List of connected macAddresses.
        /// </summary>
        private List<string> connectedMacAddresses { get; } = new List<string>();

        /// <summary>
        /// The list of required Android permissions to be able to scan Bluetooth devices.
        /// Please, be sure that they are listed in the manifest.
        /// </summary>
        private readonly string[] requiredAndroidPermissions = new string[]
        {
            Permission.FineLocation,
            Permission.CoarseLocation,
            "android.permission.BLUETOOTH_ADMIN",
            "android.permission.BLUETOOTH_SCAN",
            "android.permission.BLUETOOTH",
            "android.permission.BLUETOOTH_CONNECT"
        };

        private bool _permissionsGranted = false;
        
        /// <summary>
        /// Proxy variable to use BluetoothManager class from BLE Android Native plugin. 
        /// </summary>
        private AndroidJavaObject bluetoothManager;
        
        /// <summary>
        /// Proxy variable to use BluetoothScanner class from BLE Android Native plugin. 
        /// </summary>
        private AndroidJavaObject bluetoothScanner;

        /// <summary>
        /// Constructor of the class, calls the request of required permissions from device and init the core variable for further work.
        /// </summary>
        /// <param name="deviceConnectionHandler">Method to call when connection to device is finished.</param>
        /// <param name="deviceReadyHandler">Method to call when device is ready for transhipment.</param>
        /// <param name="deviceDisconnectionHandler">Method to call when the disconnection from devices is finished(optional).</param>
        /// /// <param name="errorReceivedHandler">Method to call when error is occurred.</param>
        public BluetoothManager(Action<BleDevice> deviceConnectionHandler, Action<BleDevice> deviceReadyHandler, Action<BleDevice> deviceDisconnectionHandler, Action<int, string> errorReceivedHandler)
        {
            CallPermissions(this.requiredAndroidPermissions);
            Init(deviceConnectionHandler, deviceReadyHandler, deviceDisconnectionHandler, errorReceivedHandler);
        }

        /// <summary>
        /// Inits the internal BluetoothManager proxy to communicate with plugin.
        /// </summary>
        /// <param name="deviceConnectionHandler">Method to call when connection to device is finished.</param>
        /// <param name="deviceReadyHandler">Method to call when device is ready for transhipment.</param>
        /// <param name="deviceDisconnectionHandler">Method to call when the disconnection from devices is finished(optional).</param>
        /// <param name="errorReceivedHandler">Method to call when error is occurred.</param>
        private void Init(Action<BleDevice> deviceConnectionHandler, Action<BleDevice> deviceReadyHandler, Action<BleDevice> deviceDisconnectionHandler, Action<int, string> errorReceivedHandler)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            var connectionCallback = new ConnectionCallback(this);
            connectionCallback.OnDeviceConnected += SetDeviceStatusAsConnected;
            connectionCallback.OnDeviceConnected += deviceConnectionHandler;
            connectionCallback.OnDeviceReady += deviceReadyHandler;
            connectionCallback.OnDeviceDisconnected += CheckHowDisconnected;
            connectionCallback.OnDeviceDisconnected += deviceDisconnectionHandler;
            
            var errorCallback = new ErrorCallback();
            errorCallback.OnErrorReceived += errorReceivedHandler;
            errorCallback.OnErrorReceived += OnErrorActions;
            
            this.bluetoothManager = new AndroidJavaObject("it.weart.bluetooth.BleManager", currentActivity, errorCallback, connectionCallback);
        }

        /// <summary>
        /// Gets the current version of BLE Android plugin.
        /// </summary>
        /// <returns></returns>
        public string GetPluginVersion()
        {
            return this.bluetoothManager.Call<string>("getVersion");
        }
        
        /// <summary>
        /// Starts the scan for bluetooth devices.
        /// </summary>
        /// <param name="deviceFoundHandler">Method to call when new device is found.</param>
        /// /// <param name="deviceRemovedHandler">Method to call when discovered device stops being available for connection.</param>
        public async void StartScanning(Action<BleDevice> deviceFoundHandler, Action<BleDevice> deviceRemovedHandler)
        {
            if (this.IsScanning) return;

            if (!this._permissionsGranted)
            {
                await WaitsPermissionsAsync();
            }

            var scanResultCallback = new ScanResultCallback(this);
            scanResultCallback.OnDeviceFound += UpdateScannedDeviceList;
            scanResultCallback.OnDeviceFound += deviceFoundHandler;
            scanResultCallback.OnDeviceRemoved += OnRemovedActions;
            scanResultCallback.OnDeviceRemoved += deviceRemovedHandler;
            
            this.bluetoothScanner = this.bluetoothManager.Call<AndroidJavaObject>("getBluetoothScanner");
            
            int statusCode = this.bluetoothScanner.Call<int>("startScan", scanResultCallback);

            if (statusCode > 0)
            {
                WeArtLog.Log("Have an error code: " + statusCode,LogType.Error);
                return;
            }
                
            this.IsScanning = true;
        }
        
        /// <summary>
        /// Stops the scan for bluetooth devices.
        /// </summary>
        public void StopScanning()
        {
            if (this.bluetoothScanner == null) return;

            this.bluetoothScanner.Call("stopScan");

            this.IsScanning = false;
        }

        /// <summary>
        /// Gets the list with currently available devices.
        /// </summary>
        /// <returns>List with scanned BleDevices. Devices without name are ignored. </returns>
        public List<BleDevice> GetAvailableBleDevices()
        {
            AndroidJavaObject kotlinMap = this.bluetoothScanner.Call<AndroidJavaObject>("getDevices");
            int size = kotlinMap.Call<int>("size");

            if (size < 1)
            {
                kotlinMap.Dispose();
                return null;
            }

            AndroidJavaObject keySet = kotlinMap.Call<AndroidJavaObject>("keySet");
            AndroidJavaObject iterator = keySet.Call<AndroidJavaObject>("iterator");

            List<BleDevice> deviceList = new List<BleDevice>();

            while (iterator.Call<bool>("hasNext"))
            {
                string macAddress = iterator.Call<string>("next");
                string deviceName = kotlinMap.Call<string>("get", macAddress);

                if (!String.IsNullOrEmpty(deviceName)) deviceList.Add(new BleDevice(macAddress, deviceName));
            }

            kotlinMap.Dispose();
            keySet.Dispose();
            iterator.Dispose();
            
            return deviceList;
        }

        /// <summary>
        /// Connects to device using its MAC address and callback to handle data from device.
        /// </summary>
        /// <param name="deviceMacAddress">MAC Address of device.</param>
        /// <param name="dataReceiveHandler">Method to call when data received from device.</param>
        public void ConnectToDevice(string deviceMacAddress, Action<BleDevice, sbyte[]> dataReceiveHandler)
        {
            SetDeviceStatusAsConnecting(deviceMacAddress);
            
            var dataReceivedCallback = new DataReceivedCallback(this);
            
            dataReceivedCallback.OnDataReceived += dataReceiveHandler;
            
            int statusCode = this.bluetoothManager.Call<int>(
                "connectToDevice",
                deviceMacAddress,
                dataReceivedCallback);

            if (statusCode > 0)
            {
                WeArtLog.Log("Have an error code: " + statusCode, LogType.Error);
            }
        }

        /// <summary>
        /// Connects to device using its MAC address and callback to handle data from device.
        /// </summary>
        /// <param name="device">Ble device to connect.</param>
        /// <param name="dataReceiveHandler">Method to call when data received from device.</param>
        public void ConnectToDevice(BleDevice device, Action<BleDevice, sbyte[]> dataReceiveHandler)
        {
            ConnectToDevice(device.DeviceMacAddress, dataReceiveHandler);
        }
        
        /// <summary>
        /// Disconnects device by its MAC address.
        /// </summary>
        /// <param name="deviceMacAddress">MAC address of device to disconnect.</param>
        public void DisconnectDevice(string deviceMacAddress)
        {
            int statusCode = this.bluetoothManager.Call<int>("disconnect", deviceMacAddress);
            
            if (statusCode > 0)
            {
                WeArtLog.Log("Have an error code: " + statusCode, LogType.Error);
            }
            
            CleanConnectedDeviceList(deviceMacAddress);
            SetDeviceStatusAsDisconnecting(deviceMacAddress);
        }

        /// <summary>
        /// Disconnects device.
        /// </summary>
        /// <param name="device">BLE device to disconnect.</param>
        public void DisconnectDevice(BleDevice device)
        {
            DisconnectDevice(device.DeviceMacAddress);
        }
        
        /// <summary>
        /// Sends data to connected device by its MAC Address.
        /// </summary>
        /// <param name="deviceMacAddress">MAC address of the connected device.</param>
        /// <param name="sendingData">Data to be sent to device.</param>
        public void SendData(string deviceMacAddress, byte[] sendingData)
        {
            if (!this.connectedMacAddresses.Contains(deviceMacAddress)) return;

            AndroidJavaObject dataManager = this.bluetoothManager.Call<AndroidJavaObject>("getDataManager");

            int statusCode = dataManager.Call<int>("sendData", deviceMacAddress, sendingData);
            
            if (statusCode > 0)
            {
                WeArtLog.Log("Have an error code: " + statusCode, LogType.Error);
            }
            
            dataManager.Dispose();
        }

        /// <summary>
        /// Sends data to connected device.
        /// </summary>
        /// <param name="device">Connected device.</param>
        /// <param name="sendingData">Data to be sent to device.</param>
        public void SendData(BleDevice device, byte[] sendingData)
        {
            SendData(device.DeviceMacAddress, sendingData);
        }
        
        /// <summary>
        /// Checks if such BleDevice is connected to this BluetoothManager.
        /// </summary>
        /// <param name="device">BLE Device to be checked</param>
        /// <returns>"True" if connected and "False" otherwise.</returns>
        public bool ContainsConnectedDevice(BleDevice device)
        {
            return this.connectedMacAddresses.Contains(device.DeviceMacAddress);
        }

        /// <summary>
        /// Checks if such MAC Address is connected to this BluetoothManager.
        /// </summary>
        /// <param name="deviceMacAddress">MAC Address to be checked.</param>
        /// <returns>"True" if connected and "False" otherwise.</returns>
        public bool ContainsConnectedDevice(string deviceMacAddress)
        {
            return this.connectedMacAddresses.Contains(deviceMacAddress);
        }

        /// <summary>
        /// Returns connected device name by its MAC Address or empty string if it is not connected.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        /// <returns></returns>
        public string GetConnectedDeviceName(string deviceMacAddress)
        {
            foreach (var device in this.ConnectedDevices)
            {
                if (!device.DeviceMacAddress.Equals(deviceMacAddress)) continue;

                return device.DeviceName;
            }
            
            return String.Empty;
        }

        /// <summary>
        /// Returns name by its MAC Address or empty string if it is not scanned by BLE plugin.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        /// <returns></returns>
        public string GetDeviceNameFromScanner(string deviceMacAddress)
        {
            foreach (var device in this.ListOfScannedDevices)
            {
                if (!device.DeviceMacAddress.Equals(deviceMacAddress)) continue;

                return device.DeviceName;
            }
            
            return String.Empty;
        }

        /// <summary>
        /// Gets device from ScannedDeviceList by its MAC Address. If this device is absent in the list — creates and returns the new one.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        /// <returns></returns>
        public BleDevice GetDeviceFromScannedDevices(string deviceMacAddress)
        {
            BleDevice device = this.ListOfScannedDevices.FirstOrDefault(d => d.DeviceMacAddress.Equals(deviceMacAddress));

            if (device != null) return device;
            
            device = new BleDevice(deviceMacAddress, GetDeviceNameFromScanner(deviceMacAddress));
            
            this.ListOfScannedDevices.Add(device);

            return device;
        }
        
        /// <summary>
        /// Returns known DeviceStatus by its MAC Address.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        /// <returns></returns>
        public DeviceStatus GetDeviceStatus(string deviceMacAddress)
        {
            if (this.connectedMacAddresses.Contains(deviceMacAddress)) return DeviceStatus.Connected;

            foreach (var device in ListOfScannedDevices)
            {
                if (!device.DeviceMacAddress.Equals(deviceMacAddress)) continue;

                return device.DeviceStatus;
            }
            
            return DeviceStatus.Unavailable;
        }

        /// <summary>
        /// Returns known BLE Device Status.
        /// </summary>
        /// <param name="bleDevice"></param>
        /// <returns></returns>
        public DeviceStatus GetDeviceStatus(BleDevice bleDevice)
        {
            return GetDeviceStatus(bleDevice.DeviceMacAddress);
        }
        
        /// <summary>
        /// Delete device from connected lists by its MAC Address.
        /// </summary>
        /// <param name="deviceMacAddress">Device MAC Address to be deleted.</param>
        private void CleanConnectedDeviceList(string deviceMacAddress)
        {
            if (!this.ContainsConnectedDevice(deviceMacAddress)) return;

            this.connectedMacAddresses.Remove(deviceMacAddress);

            foreach (var device in this.ConnectedDevices)
            {
                if (!device.DeviceMacAddress.Equals(deviceMacAddress)) continue;

                this.ConnectedDevices.Remove(device);
                return;
            }
        }

        /// <summary>
        /// Checks if device disconnected from device side.
        /// </summary>
        /// <param name="device"></param>
        private void CheckHowDisconnected(BleDevice device)
        {
            if (GetDeviceStatus(device) == DeviceStatus.Connected || GetDeviceStatus(device) == DeviceStatus.Connecting)
            {
                if (CheckDeviceConnectionInPlugin(device)) return;
                
                CleanConnectedDeviceList(device.DeviceMacAddress);
                SetDeviceStatusAsUnavailable(device);
            }
            
            SetDeviceAsAvailable(device);
        }

        /// <summary>
        /// Checks if the device is connected with plugin. Useful to handle the delayed OnDisconnected events. 
        /// </summary>
        /// <param name="bleDevice"></param>
        /// <returns></returns>
        public bool CheckDeviceConnectionInPlugin(BleDevice bleDevice)
        {
            AndroidJavaObject kotlinDeviceList = this.bluetoothManager.Call<AndroidJavaObject>("getConnectedAddresses");
            int size = kotlinDeviceList.Call<int>("size");

            if (size < 1)
            {
                kotlinDeviceList.Dispose();
                return false;
            }
            
            for (int i = 0; i < size; i++)
            {
                string device = kotlinDeviceList.Call<string>("get", i);

                if (bleDevice.DeviceMacAddress.Equals(device))
                {
                    kotlinDeviceList.Dispose();
                    return true;
                }
            }

            kotlinDeviceList.Dispose();
            
            return false;
        }
        
        /// <summary>
        /// Updates device status when plugin calls OnRemoved callback.
        /// </summary>
        /// <param name="device"></param>
        private void OnRemovedActions(BleDevice device)
        {
            if (GetDeviceStatus(device) is DeviceStatus.Available)
            {
                SetDeviceStatusAsUnavailable(device.DeviceMacAddress);
            }
        }

        /// <summary>
        /// Internal handler of different errors.
        /// </summary>
        /// <param name="errorCode">Code of error from native plugin.</param>
        /// <param name="errorMessage">Error description.</param>
        private void OnErrorActions(int errorCode, string errorMessage)
        {
            
        }
        
        /// <summary>
        /// Extracts the MAC Addresses from provided string and returns the first match or returns null if there is no MAC Address.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public string ExtractMacAddressFromMessage(string input)
        {
            string pattern = "([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})";
            Regex regex = new Regex(pattern);

            MatchCollection matches = regex.Matches(input);

            if (matches.Count > 0)
            {
                string macAddress = matches[0].Value.ToUpper();
                
                return macAddress;
            }
            
            return null;
        }

        #region Update Device Status Region

        /// <summary>
        /// Update the scanned device list from BluetoothManager scanner.
        /// </summary>
        /// <param name="device"></param>
        private void UpdateScannedDeviceList(BleDevice device)
        {
            foreach (var scannedDevice in this.ListOfScannedDevices)
            {
                if (!scannedDevice.DeviceMacAddress.Equals(device.DeviceMacAddress)) continue;
                WeArtLog.Log($"BL MANAGER SETS STATUS {device.DeviceMacAddress} BEFORE {GetDeviceStatus(device.DeviceMacAddress)}");
                this.ListOfScannedDevices[this.ListOfScannedDevices.IndexOf(scannedDevice)].DeviceStatus = DeviceStatus.Available;
                WeArtLog.Log($"BL MANAGER SETS STATUS {device.DeviceMacAddress} AFTER {GetDeviceStatus(device.DeviceMacAddress)}");
                return;
            }
            
            this.ListOfScannedDevices.Add(device);
        }

        /// <summary>
        /// Set status of BLE device as Connected.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        private void SetDeviceStatusAsConnected(string deviceMacAddress)
        {
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} BEFORE {GetDeviceStatus(deviceMacAddress)}");
            this.connectedMacAddresses.Add(deviceMacAddress);
            this.ConnectedDevices.Add(new BleDevice(deviceMacAddress, GetDeviceNameFromScanner(deviceMacAddress)));

            UpdateDeviceStatusInList(deviceMacAddress, DeviceStatus.Connected);
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} AFTER {GetDeviceStatus(deviceMacAddress)}");
        }

        /// <summary>
        /// Set status of BLE device as Connected.
        /// </summary>
        /// <param name="device"></param>
        private void SetDeviceStatusAsConnected(BleDevice device)
        {
            SetDeviceStatusAsConnected(device.DeviceMacAddress);
        }
        
        /// <summary>
        /// Set status of BLE device as Unavailable.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        private void SetDeviceStatusAsUnavailable(string deviceMacAddress)
        {
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} BEFORE {GetDeviceStatus(deviceMacAddress)}");
            UpdateDeviceStatusInList(deviceMacAddress, DeviceStatus.Unavailable);
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} AFTER {GetDeviceStatus(deviceMacAddress)}");
        }

        /// <summary>
        /// Set status of BLE device as Unavailable.
        /// </summary>
        /// <param name="device"></param>
        private void SetDeviceStatusAsUnavailable(BleDevice device)
        {
            SetDeviceStatusAsUnavailable(device.DeviceMacAddress);
        }
        
        /// <summary>
        /// Set status of BLE device as Connecting.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        private void SetDeviceStatusAsConnecting(string deviceMacAddress)
        {
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} BEFORE {GetDeviceStatus(deviceMacAddress)}");
            UpdateDeviceStatusInList(deviceMacAddress, DeviceStatus.Connecting);
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} AFTER {GetDeviceStatus(deviceMacAddress)}");
        }

        /// <summary>
        /// Set status of BLE device as Connecting.
        /// </summary>
        /// <param name="device"></param>
        private void SetDeviceStatusAsConnecting(BleDevice device)
        {
            SetDeviceStatusAsConnecting(device.DeviceMacAddress);
        }
        
        /// <summary>
        /// Set status of BLE device as Disconnecting.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        private void SetDeviceStatusAsDisconnecting(string deviceMacAddress)
        {
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} BEFORE {GetDeviceStatus(deviceMacAddress)}");
            UpdateDeviceStatusInList(deviceMacAddress, DeviceStatus.Disconnecting);
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} AFTER {GetDeviceStatus(deviceMacAddress)}");
        }

        /// <summary>
        /// Set status of BLE device as Disconnecting.
        /// </summary>
        /// <param name="device"></param>
        private void SetDeviceStatusAsDisconnecting(BleDevice device)
        {
            SetDeviceStatusAsDisconnecting(device.DeviceMacAddress);
        }
        
        /// <summary>
        /// Set status of BLE device as Available.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        private void SetDeviceAsAvailable(string deviceMacAddress)
        {
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} BEFORE {GetDeviceStatus(deviceMacAddress)}");
            UpdateDeviceStatusInList(deviceMacAddress, DeviceStatus.Available);
            WeArtLog.Log($"BL MANAGER SETS STATUS {deviceMacAddress} AFTER {GetDeviceStatus(deviceMacAddress)}");
            
        }

        /// <summary>
        /// Set status of BLE device as Available.
        /// </summary>
        /// <param name="device"></param>
        private void SetDeviceAsAvailable(BleDevice device)
        {
            SetDeviceAsAvailable(device.DeviceMacAddress);
        }
        
        /// <summary>
        /// Set new the Status to the BLE device in the list of scanned devices.
        /// </summary>
        /// <param name="deviceMacAddress">Device to updated status</param>
        /// <param name="newStatus">New Status</param>
        private void UpdateDeviceStatusInList(string deviceMacAddress, DeviceStatus newStatus)
        {
            foreach (var device in this.ListOfScannedDevices)
            {
                if (!device.DeviceMacAddress.Equals(deviceMacAddress)) continue;

                this.ListOfScannedDevices[this.ListOfScannedDevices.IndexOf(device)].DeviceStatus = newStatus;

                DeviceStatusInfo statusInfo = new DeviceStatusInfo(deviceMacAddress, newStatus);
                DeviceStatusInfoReady?.Invoke(statusInfo);

                return;
            }
        }
        
        #endregion
        
        
        #region Getting Android Permissions Region
        
        /// <summary>
        /// Waits for till required Android permissions will be received. 
        /// </summary>
        private async Task WaitsPermissionsAsync()
        {
            while (!_permissionsGranted)
            {
                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// Call for permissions withing Unity. With possibility to handle result callbacks for each permission.
        /// </summary>
        /// <param name="permissions">Array of permissions that will be called for.</param>
        private void CallPermissions(string[] permissions)
        {
#if UNITY_2021_1_OR_NEWER
            var callbacks = new PermissionCallbacks();
            
            callbacks.PermissionDenied += PermissionCallbacks_PermissionDenied;
            callbacks.PermissionGranted += PermissionCallbacks_PermissionGranted;
            callbacks.PermissionDeniedAndDontAskAgain += PermissionCallbacks_PermissionDeniedAndDontAskAgain;
            Permission.RequestUserPermissions(permissions, callbacks);
#endif
        }
        
        private void PermissionCallbacks_PermissionDeniedAndDontAskAgain(string permissionName)
        {
            WeArtLog.Log($"{permissionName} Permission Denied And Dont Ask Again");
            if (permissionName.Equals("android.permission.BLUETOOTH_SCAN")) _permissionsGranted = true;
        }

        private void PermissionCallbacks_PermissionGranted(string permissionName)
        {
            WeArtLog.Log($"{permissionName} Granted");
            if (permissionName.Equals("android.permission.BLUETOOTH_SCAN")) _permissionsGranted = true;
        }

        private void PermissionCallbacks_PermissionDenied(string permissionName)
        {
            WeArtLog.Log($"{permissionName} Permission Denied.");
        }
        
        #endregion
        
        
    }

}
