using WEART;

namespace WeArt.Bluetooth
{
    /// <summary>
    /// Container with data about BLE Device.
    /// </summary>
    public class BleDevice
    {
        public string DeviceName { get; }
        public string DeviceMacAddress { get; }
        public DeviceID DeviceID { get; set; }
        public DeviceStatus DeviceStatus { get; set; }

        public BleDevice(string deviceMacAddress, string deviceName, DeviceID deviceID = DeviceID.First)
        {
            this.DeviceMacAddress = deviceMacAddress;
            this.DeviceName = deviceName;
            this.DeviceStatus = DeviceStatus.Available;
            this.DeviceID = deviceID;
        }
    }
}
