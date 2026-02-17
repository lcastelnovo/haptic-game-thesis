using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using WeArt.Bluetooth;
using WeArt.Core;

namespace WeArt.TouchUI
{
    public class TouchDIVERInfo : MonoBehaviour
    {
        [SerializeField] internal TextMeshProUGUI deviceMacAddress;
        [SerializeField] internal TextMeshProUGUI deviceStatus;
        [SerializeField] internal TextMeshProUGUI handSideText;
        [SerializeField] internal Button connectButton;
        [SerializeField] internal Button disconnectButton;
        [SerializeField] internal Button changeHandButton;
        [SerializeField] internal Toggle rememberToggle;
        [SerializeField] internal GameObject sleepModeNotice;

        public BleDevice BleDevice { get; private set; }
        public Button ConnectButton => this.connectButton;
        public Button DisconnectButton => this.disconnectButton;
        public Button ChangeHandButton => this.changeHandButton;
        public Toggle RememberToggle => this.rememberToggle;

        public HandSide handSide = HandSide.Right;

        internal bool isConnecting;
        internal bool isDisconnectedManually;
        internal bool isDisconnecting;
        
        private WaitForSeconds _oneSecWaiter = new WaitForSeconds(1f);
        private Color _normalTextColor;
        private BleConnectionPanel _bleConnectionPanel;
        private BluetoothManager _bluetoothManager;
        private Coroutine _updateStatusCoroutine;

        private static float _timeOut = 7f;
        
        private void Start()
        {
            changeHandButton.onClick.AddListener(ChangeHandSide);
            handSideText.text = handSide.ToString();
            _normalTextColor = deviceMacAddress.color;
        }
        
        /// <summary>
        /// Sets the TD device link to the TouchDiverInfo.
        /// </summary>
        /// <param name="device"></param>
        public void SetBleDevice(BleDevice device)
        {
            BleDevice = device;
            deviceMacAddress.text = device.DeviceMacAddress;
            UpdateDeviceStatus();
        }

        /// <summary>
        /// Sets the link to the UI BLE connection panel.
        /// </summary>
        /// <param name="bleConnectionPanel"></param>
        public void SetBleConnectionPanel(BleConnectionPanel bleConnectionPanel)
        {
            _bleConnectionPanel = bleConnectionPanel;
        }

        /// <summary>
        /// Sets the link to BluetoothManager.
        /// </summary>
        /// <param name="bluetoothManager"></param>
        public void SetBluetoothManager(BluetoothManager bluetoothManager)
        {
            _bluetoothManager = bluetoothManager;
        }

        /// <summary>
        /// Updates device status to information panel.
        /// </summary>
        public void UpdateDeviceStatus()
        {
            if (_updateStatusCoroutine != null)
            {
                StopCoroutine(_updateStatusCoroutine);
            }
            
            _updateStatusCoroutine = StartCoroutine(UpdateDeviceStatusCor());
        }

        /// <summary>
        /// Sets the new hand side.
        /// </summary>
        /// <param name="newHandSide"></param>
        public void SetHandSide(HandSide newHandSide)
        {
            handSide = newHandSide;
            handSideText.text = handSide.ToString();
        }

        /// <summary>
        /// Sets interactable state for all buttons at the panel. 
        /// </summary>
        /// <param name="newState"></param>
        public void SetInteractable(bool newState)
        {
            connectButton.interactable = newState;
            disconnectButton.interactable = newState;
            changeHandButton.interactable = newState;
            rememberToggle.interactable = newState;
        }

        /// <summary>
        /// Shows the sleep mode notification.
        /// </summary>
        public void ShowSleepModeNotification()
        {
            sleepModeNotice.gameObject.SetActive(true);
        }
        
        private IEnumerator UpdateDeviceStatusCor()
        {
            yield return _oneSecWaiter;
            
            BleDevice.DeviceStatus = _bluetoothManager.GetDeviceStatus(BleDevice);
            deviceStatus.text = BleDevice.DeviceStatus.ToString();
            
            switch (BleDevice.DeviceStatus)
            {
                case DeviceStatus.Available:
                    DisconnectButton.gameObject.SetActive(false);
                    ConnectButton.gameObject.SetActive(true);
                    ChangeHandButton.gameObject.SetActive(true);
                    sleepModeNotice.gameObject.SetActive(false);
                    deviceStatus.color = _normalTextColor;
                    isConnecting = false;
                    isDisconnecting = false;
                    break;
                case (DeviceStatus.Connected):
                    DisconnectButton.gameObject.SetActive(true);
                    ConnectButton.gameObject.SetActive(false);
                    ChangeHandButton.gameObject.SetActive(false);
                    sleepModeNotice.gameObject.SetActive(false);
                    deviceStatus.color = Color.green;
                    isConnecting = false;
                    isDisconnecting = false;
                    break;
                case (DeviceStatus.Unavailable):
                    DisconnectButton.gameObject.SetActive(false);
                    ConnectButton.gameObject.SetActive(false);
                    ChangeHandButton.gameObject.SetActive(false);
                    sleepModeNotice.gameObject.SetActive(false);
                    deviceStatus.color = Color.red;
                    isConnecting = false;
                    isDisconnecting = false;

                    if (!RememberToggle.isOn) _bleConnectionPanel.RemoveDeviceFromList(this);
                    break;
                case (DeviceStatus.Disconnecting):
                    DisconnectButton.gameObject.SetActive(false);
                    ConnectButton.gameObject.SetActive(false);
                    ChangeHandButton.gameObject.SetActive(false);
                    sleepModeNotice.gameObject.SetActive(false);
                    deviceStatus.color = _normalTextColor;

                    if (!isDisconnecting)
                    {
                        isDisconnecting = true;
                        StartCoroutine(CheckDisconnectionTimeOut());
                    }
                    break;
                case(DeviceStatus.Connecting):
                    DisconnectButton.gameObject.SetActive(false);
                    ConnectButton.gameObject.SetActive(false);
                    ChangeHandButton.gameObject.SetActive(false);
                    sleepModeNotice.gameObject.SetActive(false);
                    deviceStatus.color = _normalTextColor;
                    isDisconnecting = false;

                    if (!isConnecting)
                    {
                        isConnecting = true;
                        StartCoroutine(CheckConnectionTimeOut());
                    }
                    break;
            }

            _updateStatusCoroutine = null;
        }
        
        /// <summary>
        /// Changes the hand side from Left to Right and vice versa.
        /// </summary>
        private void ChangeHandSide()
        {
            handSide = handSide == HandSide.Right ? HandSide.Left : HandSide.Right;
            handSideText.text = handSide.ToString();
        }

        /// <summary>
        /// Checks if device is not fell asleep within connection process.
        /// </summary>
        /// <returns></returns>
        private IEnumerator CheckConnectionTimeOut()
        {
            yield return new WaitForSeconds(_timeOut);
            
            if (BleDevice.DeviceStatus == DeviceStatus.Connecting) ShowSleepModeNotification();
        }
        
        /// <summary>
        /// Checks if device is not fell asleep within disconnection process.
        /// </summary>
        /// <returns></returns>
        private IEnumerator CheckDisconnectionTimeOut()
        {
            yield return new WaitForSeconds(_timeOut);
            
            if (BleDevice.DeviceStatus == DeviceStatus.Disconnecting) ShowSleepModeNotification();
        }
        
    }
}
