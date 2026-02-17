using UnityEngine;
using System;
using WeArt.Utils;

namespace WeArt.Bluetooth
{
    /// <summary>
    /// ScanResultCallback — proxy callback class to receive scan updates from BluetoothManager.
    /// </summary>
    public class ScanResultCallback : AndroidJavaProxy
    {
        /// <summary>
        /// Invokes if the new device is discovered.
        /// </summary>
        public Action<BleDevice> OnDeviceFound;
        /// <summary>
        /// Invokes if the discovered device stops being available for connection.
        /// </summary>
        public Action<BleDevice> OnDeviceRemoved;
        /// <summary>
        /// Invokes if scan failed.
        /// </summary>
        public Action<int> OnScanFailed;

        /// <summary>
        /// Link to bluetooth manager to handle the BLE Devices.
        /// </summary>
        private BluetoothManager bluetoothManager;

        public ScanResultCallback(BluetoothManager bluetoothManager) : base("it.weart.bluetooth.ScanResultCallback")
        {
            this.bluetoothManager = bluetoothManager;
        }

        /// <summary>
        /// Method calls by Android Native plugin within device scanning when new device is discovered.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        /// <param name="name"></param>
        public void onDeviceFound(string deviceMacAddress, string name)
        {
            if (!name.Contains("Weart")) return;
            if (bluetoothManager.ContainsConnectedDevice(deviceMacAddress)) return;
            
            WeArtLog.Log($"CALLED FOUND WITH DEVICE {deviceMacAddress}");
            this.OnDeviceFound?.Invoke(new BleDevice(deviceMacAddress, name));
        }

        /// <summary>
        /// Method calls by Android Native plugin within device scanning when discovered device stops being available for connection.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        public void onDeviceRemoved(string deviceMacAddress)
        {
            if (!bluetoothManager.GetDeviceNameFromScanner(deviceMacAddress).Contains("Weart")) return;
            
            WeArtLog.Log($"CALLED REMOVED WITH DEVICE {deviceMacAddress}");
            this.OnDeviceRemoved?.Invoke(this.bluetoothManager.GetDeviceFromScannedDevices(deviceMacAddress));
        }

        /// <summary>
        /// Method calls by Android Native plugin within device scanning if something went wrong.
        /// </summary>
        /// <param name="errorCode"></param>
        public void onScanFailed(int errorCode)
        {
            WeArtLog.Log($"DEVICE SCAN IS FAILED, ERROR CODE {errorCode}", LogType.Error);

            this.OnScanFailed?.Invoke(errorCode);
        }
    }
}