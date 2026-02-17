using UnityEngine;
using System;
using WeArt.Utils;

namespace WeArt.Bluetooth
{
    /// <summary>
    /// ConnectionCallback — proxy callback class to receive confirmation that device is connected or disconnected by BluetoothManager.
    /// </summary>
    public class ConnectionCallback : AndroidJavaProxy
    {
        /// <summary>
        /// Invokes when connection to the BLE device is finished.
        /// </summary>
        public Action<BleDevice> OnDeviceConnected;
        /// <summary>
        /// Invokes when BLE device is ready to receive and send data.
        /// </summary>
        public Action<BleDevice> OnDeviceReady;
        /// <summary>
        /// Invokes when disconnection form the BLE device is finished.
        /// </summary>
        public Action<BleDevice> OnDeviceDisconnected;

        private BluetoothManager bluetoothManager;

        public ConnectionCallback(BluetoothManager bluetoothManager) : base("it.weart.bluetooth.ConnectionCallback")
        {
            this.bluetoothManager = bluetoothManager;
        }
        
        /// <summary>
        /// Calls by Android Native plugin when connection to the BLE device is finished.
        /// </summary>
        /// <param name="deviceMacAddress">MAC address of the connected BLE device</param>
        public void onConnected(string deviceMacAddress)
        {
            WeArtLog.Log($"PLUGIN CALLED OnConnected EVENT FOR DEVICE: {deviceMacAddress}");
            OnDeviceConnected?.Invoke(this.bluetoothManager.GetDeviceFromScannedDevices(deviceMacAddress));
        }

        /// <summary>
        /// Calls by Android Native plugin when connected BLE device is ready for data transhipment.
        /// </summary>
        /// <param name="deviceMacAddress"></param>
        public void onDeviceReady(string deviceMacAddress)
        {
            WeArtLog.Log($"PLUGIN CALLED OnDeviceReady EVENT FOR DEVICE: {deviceMacAddress}");
            OnDeviceReady?.Invoke(this.bluetoothManager.GetDeviceFromScannedDevices(deviceMacAddress));
        }
        
        /// <summary>
        /// Calls by Android Native plugin when disconnection from the BLE device is finished.
        /// </summary>
        /// <param name="deviceMacAddress">MAC address of the disconnected BLE device</param>
        public void onDisconnected(string deviceMacAddress)
        {
            WeArtLog.Log($"PLUGIN CALLED OnDisconnected EVENT FOR DEVICE: {deviceMacAddress}");
            OnDeviceDisconnected?.Invoke(this.bluetoothManager.GetDeviceFromScannedDevices(deviceMacAddress));
        }
    }

}