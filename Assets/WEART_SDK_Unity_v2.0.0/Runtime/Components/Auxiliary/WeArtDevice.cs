using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using WeArt.Core;
using WeArt.Bluetooth;
using WeArt.Components;
using WEART;
using static WEART.Communication;
using static WEART.ActuationCommons;
using static WeArt.Bluetooth.BluetoothManager;
using DeviceID = WeArt.Core.DeviceID;
using PackTag = WeArt.Utils.LogEnums.PackTag;
using LogLevel = WeArt.Utils.LogEnums.LogLevel;
using PackDescription = WeArt.Utils.LogEnums.PackDescription;
using System.Linq;
using static WEART.Status;
using WeArt.Utils;


public class WeArtDevice : MonoBehaviour
{
    [SerializeField]
    internal HandSide _handSide;

    [SerializeField]
    internal DeviceID _deviceID;

    //BLE property
    internal BleDevice BleDevice { get; private set; }
    private BluetoothManager _bluetoothManager;
    private Coroutine _actuationCoroutine;

    private static WeartSdkProtocol weartSdkProtocol;
    private byte[] oldDataFirst = null;
    private byte[] oldDataSecond = null;

    #region EVENTS

    public class TouchDIVERStatusUpdated : UnityEvent<object> { } //To be defined
    private TouchDIVERStatusUpdated _touchDIVERStatusUpdated;

    // Create a delegate for the event
    public delegate void dDeviceInfo<DeviceInfoEventArgs>(DeviceInfoEventArgs e);
    public event dDeviceInfo<DeviceInfoEventArgs> DeviceInfoReady;

    #endregion

    // Start is called before the first frame update
    private void Start()
    {
        weartSdkProtocol = new WeartSdkProtocol();

        if (_weartCommunication != null)
        {
            _weartCommunication.MessageReceivedFromMiddleware += OnTrackingOutputMessageReceived;
            _weartCommunication.PackReceivedFromMiddleware += OnPackReceivedFromMiddleware;
            DeviceInfoReady += _weartCommunication.DeviceInfoReady; // Send device info to Middleware DLL
            deviceCurrentStatusReady += DeviceStatusFromMiddleware;
        }
        else
        {
            WeArtLog.Log("Communication instance is not available!");
        }
    }

    #region Methods

    /// <summary>
    /// Sets the link to the BluetoothManager and BlePanel.
    /// </summary>
    /// <param name="bluetoothManager"></param>
    public void SetBluetoothManager(BluetoothManager bluetoothManager)
    {
        _bluetoothManager = bluetoothManager;
        // Set here methods from _bluetoothManager, so we are sure it is not null reference
        _bluetoothManager.DeviceStatusInfoReady += ReceiveDeviceStatus;
    }

    /// <summary>
    /// Enables / disables sending WEART actuation data request from connected TD device.
    /// </summary>
    /// <param name="enable"></param>
    public void RunDevice(bool enable)
    {
        if (BleDevice == null) return;

        if ((_actuationCoroutine == null && !enable) || (_actuationCoroutine != null && enable)) return;

        if (enable)
        {
            SendData(DeviceCommands.ENABLING_RUNNING_ON);

            _actuationCoroutine = StartCoroutine(SendActuationDataCor());
            
            return;
        }
        
        StopActuationCoroutine();
        SendData(DeviceCommands.ENABLING_RUNNING_OFF);
    }

    /// <summary>
    /// Start continuous sending the Actuation data request to connected device.
    /// </summary>
    /// <returns></returns>
    private IEnumerator SendActuationDataCor()
    {

        WaitForSeconds waiter = new WaitForSeconds(0.06f);

        yield return new WaitForSeconds(1f);

        while (true)
        {
            (byte[] dataToSend, bool needToBeSend) = ComposeActPack((int)_deviceID, _handSide);

            if (needToBeSend)
            {
                SendData(dataToSend);
                WeArtLog.LogFile(LogLevel.DEBUG, PackTag.REQ, Utils.logHexPack(dataToSend), _deviceID, PackDescription.ActuationPack);
            }

            yield return waiter;
        }
    }

    #endregion

    #region Communication

    /// <summary>
    /// Connects to TouchDiver ble device.
    /// </summary>
    /// <param name="device"></param>
    public void ConnectDevice(BleDevice device)
    {
        if (BleDevice != null) DisconnectDevice();

        BleDevice = device;

        StartCoroutine(ConnectDeviceCor(device));

        //Manage device connection BLE and communication
    }

    /// <summary>
    /// Disconnects connected TouchDiver ble device.
    /// </summary>
    public void DisconnectDevice()
    {
        if (BleDevice == null) return;
        
        RunDevice(false);
        _bluetoothManager?.DisconnectDevice(BleDevice);
        WeArtController.Instance.UpdateDeviceStatus(BleDevice);
        
        BleDevice = null;
    }

    public void PingDevice()
    {
        //Send WEART protocol message to make blink the device
    }

    /// <summary>
    /// Stops actuation coroutine if it was launched.
    /// </summary>
    private void StopActuationCoroutine()
    {
        if (_actuationCoroutine == null) return;
        
        StopCoroutine(_actuationCoroutine);
        _actuationCoroutine = null;
    }

    #endregion

    #region BLE 

    /// <summary>
    /// Callback to receive data from connected TD device.
    /// </summary>
    /// <param name="device"></param>
    /// <param name="data"></param>
    private void ReceiveData(BleDevice device, sbyte[] data)
    {
        byte[] byteArray = new byte[data.Length];

        for (int i = 0; i < data.Length; i++)
        {
            byteArray[i] = (byte)data[i];
        }

        string message = BitConverter.ToString(byteArray).Replace("-", " ");

        if (_weartCommunication == null)
        {
            message += "\nDevice sends data, but communication instance is not available!";
            WeArtLog.Log(message);

            return;
        }
        _weartCommunication.SendMessageToMiddleware(byteArray, (int)_deviceID);
    }

    /// <summary>
    /// Sending data to connected device.
    /// </summary>
    /// <param name="data"></param>
    private void SendData(byte[] data)
    {
        _bluetoothManager?.SendData(BleDevice, data);
    }

    private IEnumerator ConnectDeviceCor(BleDevice device)
    {
        yield return new WaitForSeconds(1f);

        _bluetoothManager?.ConnectToDevice(device, ReceiveData);
    }

    #endregion

    /// <summary>
    /// Message received from Middleware containing tracking algorithm message with all closure values
    /// </summary>
    /// <param name="trackingData"></param>
    private void OnTrackingOutputMessageReceived(string trackingData)
    {

    }

    /// <summary>
    /// Compose actuation message to send to a device basing on the handSide and deviceID of the TouchDIVER connected
    /// </summary>
    /// <param name="deviceID"></param>
    /// <param name="handSide"></param>
    /// <returns></returns>
    private (byte[], bool) ComposeActPack(int deviceID, HandSide handSide)
    {       
        // Check if right and left hand side belong to firstTouchDIVER or secondTouchDIVER
        if (deviceID == 0)
        {
            if (handSide == HandSide.Left)
            {
                deviceID = 1;
            }
        }
        else
        {
            if (handSide == HandSide.Right)
            {
                deviceID = 0;
            }
        }

        byte[] data = null;
        LinkedList<byte[]> queue;

        bool needToSend = false;
        switch (deviceID)
        { 
            case 0:
                queue = queueFirst;
                (data, needToSend)  = ExtractActData(queue, deviceID);
                oldDataFirst = data.Clone() as byte[];
                return needToSend ? (weartSdkProtocol.RequestActAndTrackData(data), true) : (weartSdkProtocol.RequestActAndTrackData(data), false);
            case 1:
                queue = queueSecond;
                (data, needToSend) = ExtractActData(queue, deviceID);
                oldDataSecond = data.Clone() as byte[];
                return needToSend ? (weartSdkProtocol.RequestActAndTrackData(data), true) : (weartSdkProtocol.RequestActAndTrackData(data), false);
            default:
                return needToSend ? (weartSdkProtocol.RequestActAndTrackData(data), true) : (weartSdkProtocol.RequestActAndTrackData(data), false);

        }
    }

    /// <summary>
    /// Extract data from the right buffer basing on ID of the device we want to send data
    /// </summary>
    /// <param name="queue"></param>
    /// <param name="ID"></param>
    /// <returns></returns>
    private (byte[], bool) ExtractActData(LinkedList<byte[]> queue, int ID)
    {
        lock (queue)
        {
            byte[] data;
            if (queue.Count > 0)
            {
                data = queue.First.Value;
                queue.RemoveFirst();
                bool actChanged = true;
                if (ID == (int)DeviceID.First)
                {
                    // If the act data is different from the last applied one, send it
                    actChanged = oldDataFirst is null || !oldDataFirst.SequenceEqual(data);
                }
                else
                {
                    // If the act data is different from the last applied one, send it
                    actChanged = oldDataSecond is null || !oldDataSecond.SequenceEqual(data);
                }
                if (actChanged)
                {
                    return (data, true);
                }
                else
                {
                    return (data, false);
                }
            }
            else
            {
                if ((DeviceID)ID == DeviceID.First)
                    data = oldDataFirst != null ? oldDataFirst : nullActPacket;
                else
                    data = oldDataSecond != null ? oldDataSecond : nullActPacket;
                return (data, false);
            }
        }
    }

    /// <summary>
    /// Byte pack received from Middleware to be sent to the device specified with deviceID
    /// </summary>
    /// <param name="pack"></param>
    /// <param name="deviceID"></param>
    private void OnPackReceivedFromMiddleware(byte[] pack, int deviceID)
    {
        if (WeArtUtility.EnableRunningPack(pack) && (DeviceID)deviceID == _deviceID)
        {
            if (WeArtUtility.RunON(pack))
            {
                RunDevice(true);
            }   
            else
            {
                DisconnectDevice();
            }
        }        
        else
        {
            if ((DeviceID)deviceID == _deviceID)
            {
                try
                {
                    SendData(pack);
                }
                catch (Exception e)
                {
                    WeArtLog.Log("Error on sending data to Device nr. " + deviceID.ToString() + "\n" + e.Message);
                }
            }
            else
            {
                return;
            }
        }
    }

    

    /// <summary>
    /// Callback of DeviceStatusInfoReady that updates DeviceStatusInfo with the latest values
    /// </summary>
    /// <param name="deviceStatusInfo"></param>
    private void ReceiveDeviceStatus(DeviceStatusInfo deviceStatusInfo)
    {
        if (BleDevice != null)
        {
            if (BleDevice.DeviceMacAddress == deviceStatusInfo.MacAddress)
            {
                bool connected = deviceStatusInfo.DeviceStatus == DeviceStatus.Connected;
                GenerateDeviceInfoEvent(connected, (int)_deviceID, (int)_handSide);
            }
        }
    }

    /// <summary>
    /// Event raise to be called by Middleware to propagate DeviceInfo
    /// </summary>
    /// <param name="connected"></param>
    /// <param name="deviceID"></param>
    /// <param name="handSide"></param>
    public void GenerateDeviceInfoEvent(bool connected, int deviceID, int handSide)
    {
        HandSide handSideM = SwitchHandSideForMiddleware((HandSide)handSide);
        DeviceInfoEventArgs info = new DeviceInfoEventArgs(connected, deviceID, (int)handSideM);
        DeviceInfoReady?.Invoke(info);
    }

    /// <summary>
    /// Switch hand side for Middleware
    /// </summary>
    /// <param name="handside"></param>
    /// <returns></returns>
    private HandSide SwitchHandSideForMiddleware(HandSide handside)
    {        
        return handside == HandSide.Right ? HandSide.Left : HandSide.Right;
    }

    /// <summary>
    /// Callback of changing device status event coming from Middleware
    /// </summary>
    /// <param name="deviceStatusInfoFromMiddleware"></param>
    private void DeviceStatusFromMiddleware (DeviceStatusInfoFromMiddleware deviceStatusInfoFromMiddleware)
    {
        if ((DeviceID)deviceStatusInfoFromMiddleware.DeviceID == _deviceID)
        {
            if (deviceStatusInfoFromMiddleware.CurrentStatus == CURRENT_STATUS.CONNECTED)
            {
                WeArtController.Instance.AfterConnectionSuccessful();
            }
        }
    }
}
