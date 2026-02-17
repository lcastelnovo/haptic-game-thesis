using UnityEngine;
using System;

namespace WeArt.Bluetooth
{
    /// <summary>
    /// DataReceivedCallback — proxy callback class to receive data from connected device by BluetoothManager.
    /// </summary>
    public class DataReceivedCallback : AndroidJavaProxy
    {
        /// <summary>
        /// Invokes if the connected device is sending data.
        /// </summary>
        public Action<BleDevice, sbyte[]> OnDataReceived;

        /// <summary>
        /// Link to bluetooth manager to handle the BLE Devices.
        /// </summary>
        private BluetoothManager bluetoothManager;

        public DataReceivedCallback(BluetoothManager bluetoothManager) : base("it.weart.bluetooth.DataReceivedCallback")
        {
            this.bluetoothManager = bluetoothManager;
        }
        
        /// <summary>
        /// Method calls by Android Native plugin if the connected device is sending data.
        /// </summary>
        /// <param name="dataPackage">Data container from Android Native Plugin that contain the sender device MAC Address and sbyte[] data</param>
        public void onDataReceived(AndroidJavaObject dataPackage)
        {
            string deviceMacAddress = dataPackage.Get<string>("macAddress");
            sbyte[] data = dataPackage.Get<sbyte[]>("data");
            
            OnDataReceived?.Invoke(this.bluetoothManager.GetDeviceFromScannedDevices(deviceMacAddress), data);

            dataPackage.Dispose();
        }
    }

}