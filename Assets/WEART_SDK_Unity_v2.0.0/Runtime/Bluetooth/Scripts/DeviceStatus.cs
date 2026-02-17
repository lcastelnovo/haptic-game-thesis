namespace WeArt.Bluetooth
{
    /// <summary>
    /// Statuses of BLE Device: <b>Available</b> to connect, <b>Connected</b> with this plugin, <b>Connecting</b> to this plugin <b>Disconnecting</b> from this plugin <b>Unavailable</b>  to update info.
    /// </summary>
    public enum DeviceStatus
    {
        Available,
        Connected,
        Connecting,
        Disconnecting,
        Unavailable
    }
}
