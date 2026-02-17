using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;
using WeArt.Bluetooth;
using System.IO;
using UnityEngine.UI;
using TMPro;
using WEART;
using WeArt.Core;
using WeArt.Utils;
using static WEART.Status;
using static WEART.Communication;
using PackTag = WeArt.Utils.LogEnums.PackTag;
using LogLevel = WeArt.Utils.LogEnums.LogLevel;

namespace WeArt.TouchUI
{
    /// <summary>
    /// UI Panel for TD devices connection management.
    /// </summary>
    public class BleConnectionPanel : MonoBehaviour
    {
        [Header("Canvas elements")]
        [SerializeField] internal GameObject touchDiverInfoPrefab;
        [SerializeField] internal Transform deviceContainer;
        [SerializeField] internal Button scrollUpButton;
        [SerializeField] internal Button scrollDownButton;
        [SerializeField] internal Scrollbar scrollBar;
        [SerializeField] internal GameObject slidingArea;
        [SerializeField] internal TextMeshProUGUI autoConnectionText;
        
        private BluetoothManager _bluetoothManager;
        private List<TouchDIVERInfo> _deviceHolders = new List<TouchDIVERInfo>();
        private RememberedDevices _rememberedDevices = new RememberedDevices();

        private readonly WaitForSeconds _oneSecWaiter = new WaitForSeconds(1f);
        
        private CanvasGroup _canvasGroup;
        private bool _isHidden;
        
        private bool _сonnectionProcessing = false;
        private BleDevice _connectingDevice;
        private string _rememberedDataPath;
        private bool _isDeviceHolderCreating = false;

        private int _minScrollStep = 0;
        private int _maxDevicesAtDisplay = 2;
        private int _maxScrollStep = 0;
        private int _currentScrollStep = 0;
        private float _scrollValueStep = 1;
        
        private void Awake()
        {
            _rememberedDataPath = Path.Combine(Application.persistentDataPath, $"RememberedDevices.txt");
            _canvasGroup = GetComponent<CanvasGroup>();
            RememberDevices();
        }

        private void Start()
        {
            deviceCurrentStatusReady += UseUpdatedDeviceStatusFromMiddleware;
            SubscribePanelButtons();
        }

        private void OnEnable()
        {
            PlaceNearHandAnchor();
        }

        private void OnDestroy()
        {
            deviceCurrentStatusReady -= UseUpdatedDeviceStatusFromMiddleware;
        }

        /// <summary>
        /// Shows/Hides panel.
        /// </summary>
        public void ShowHidePanel()
        {
            _isHidden = !_isHidden;
            _canvasGroup.alpha = _isHidden ? 0 : 1f;
            SetPanelInteractable(!_isHidden);
        }
        
        /// <summary>
        /// Sets link to BluetoothManager.
        /// </summary>
        /// <param name="bluetoothManager"></param>
        public void SetBluetoothManager(BluetoothManager bluetoothManager)
        {
            _bluetoothManager = bluetoothManager;
        }
        
        /// <summary>
        /// Creates the TouchDIVERInfo panel for new device and shows the related data in UI.
        /// </summary>
        /// <param name="device"></param>
        public void OnFoundDevice(BleDevice device)
        {
            foreach (var holder in _deviceHolders)
            {
                if (holder.BleDevice.DeviceMacAddress.Equals(device.DeviceMacAddress))
                {
                    UpdateCurrentDeviceStatus(device);
                    CheckConnectionStop(device);

                    if (holder.isDisconnectedManually)
                    {
                        holder.isDisconnectedManually = false;
                        return;
                    }
                    
                    CheckDeviceAutoConnection(holder);
                    
                    return;
                }
            }

            StartCoroutine(UpdateDeviceListCor(device));
        }

        /// <summary>
        /// Handler OnRemoveDevice Event;
        /// </summary>
        /// <param name="device"></param>
        public void OnRemovedDevice(BleDevice device)
        {
           UpdateCurrentDeviceStatus(device);
        }

        /// <summary>
        /// Handler OnConnectionDevice event;
        /// </summary>
        /// <param name="device"></param>
        public void OnConnectionDevice(BleDevice device)
        {
            UpdateCurrentDeviceStatus(device);
        }

        /// <summary>
        /// Handler OnReadyDevice event;.
        /// </summary>
        /// <param name="device"></param>
        public void OnReadyDevice(BleDevice device)
        {
            UpdateCurrentDeviceStatus(device);
        }

        /// <summary>
        /// Handler OnDisconnectDevice event.
        /// </summary>
        /// <param name="device"></param>
        public void OnDisconnectedDevice(BleDevice device)
        {
            CheckConnectionStop(device);
            UpdateCurrentDeviceStatus(device);
        }

        /// <summary>
        /// Updated device status on panel;
        /// </summary>
        /// <param name="device"></param>
        public void UpdateCurrentDeviceStatus(BleDevice device)
        {
            if (!this.isActiveAndEnabled) return;
            
            StartCoroutine(UpdateCurrentDeviceStatusCor(device));
        }

        /// <summary>
        /// Removes device from the UI device list.
        /// </summary>
        /// <param name="holder"></param>
        public void RemoveDeviceFromList(TouchDIVERInfo holder)
        {
            StartCoroutine(RemoveDeviceFromListCor(holder));
        }

        /// <summary>
        /// Checks the conditions to stop the UI blocking within connection process.
        /// </summary>
        /// <param name="device"></param>
        public void CheckConnectionStop(BleDevice device)
        {
            if (_connectingDevice != null && _connectingDevice.DeviceMacAddress.Equals(device.DeviceMacAddress))
            {
                _connectingDevice = null;
                _сonnectionProcessing = false;
                SetHoldersInteractable(true);
                autoConnectionText.gameObject.SetActive(false);
            }
        }
        
        private IEnumerator RemoveDeviceFromListCor(TouchDIVERInfo holder)
        {
            yield return null;
            
            _deviceHolders.Remove(holder);

            Destroy(holder.gameObject);
            
            UpdateScrollData();
        }
        
        private IEnumerator UpdateCurrentDeviceStatusCor(BleDevice device)
        {
            yield return _oneSecWaiter;
            
            foreach (var holder in _deviceHolders)
            {
                if (!holder.BleDevice.DeviceMacAddress.Equals(device.DeviceMacAddress)) continue;

                holder.UpdateDeviceStatus();
                yield break;
            }
        }

        private IEnumerator UpdateDeviceListCor(BleDevice device)
        {
            while (_isDeviceHolderCreating)
            {
                yield return null;
            }

            _isDeviceHolderCreating = true;
            
            yield return _oneSecWaiter;

            TouchDIVERInfo holder = Instantiate(touchDiverInfoPrefab, deviceContainer).GetComponent<TouchDIVERInfo>();

            holder.SetBleDevice(device);
            holder.SetBleConnectionPanel(this);
            holder.SetBluetoothManager(_bluetoothManager);
            
            holder.ConnectButton.onClick.AddListener(delegate { ConnectButtonSubscribe(holder); });
            holder.DisconnectButton.onClick.AddListener(delegate { DisconnectButtonSubscribe(holder); });
            holder.ChangeHandButton.onClick.AddListener(delegate { ChangeHandButtonSubscribe(holder); });
            holder.RememberToggle.onValueChanged.AddListener(delegate { SaveDevices(); });

            _deviceHolders.Add(holder);

            UpdateScrollData();

            foreach (var td in _deviceHolders)
            {
                if (!td.isConnecting && td.BleDevice.DeviceStatus != DeviceStatus.Connected) continue;
                
                var handSide = td.handSide == HandSide.Left ? HandSide.Right : HandSide.Left;
                
                holder.SetHandSide(handSide);
                break;
            }
            
            if (_сonnectionProcessing) holder.SetInteractable(false);
            
            while (_сonnectionProcessing)
            {
                yield return null;
            }

            CheckDeviceAutoConnection(holder);
            
            _isDeviceHolderCreating = false;
        }

        /// <summary>
        /// Checks the conditions for device auto connection.
        /// </summary>
        /// <param name="holder"></param>
        private void CheckDeviceAutoConnection(TouchDIVERInfo holder)
        {
            if (_bluetoothManager.ConnectedDevices.Count > 1) return;

            StartCoroutine(CheckDeviceAutoConnectionCor(holder));
        }

        private IEnumerator CheckDeviceAutoConnectionCor(TouchDIVERInfo holder)
        {
            yield return new WaitForSeconds(0.1f);

            foreach (var macAddress in _rememberedDevices.DeviceMacAddresses)
            {
                if (!macAddress.Equals(holder.BleDevice.DeviceMacAddress)) continue;

                autoConnectionText.gameObject.SetActive(true);
                
                holder.SetHandSide(_rememberedDevices.HandSides[_rememberedDevices.DeviceMacAddresses.IndexOf(macAddress)]);
                holder.RememberToggle.SetIsOnWithoutNotify(true);
                holder.ConnectButton.onClick.Invoke();

                yield break;
            }
        }
        
        /// <summary>
        /// Subscribes the ConnectButton on device info holder.
        /// </summary>
        /// <param name="holder"></param>
        private void ConnectButtonSubscribe(TouchDIVERInfo holder)
        {
            holder.ConnectButton.gameObject.SetActive(false);
            holder.ChangeHandButton.gameObject.SetActive(false);
            holder.ChangeHandButton.gameObject.SetActive(false);

            if (_bluetoothManager.ContainsConnectedDevice(holder.BleDevice))
            {
                WeArtLog.Log("WEART... : " + "ConnectButtonSubscriber if");
                holder.DisconnectButton.gameObject.SetActive(true);
                holder.UpdateDeviceStatus();
                return;
            }

            WeArtLog.Log("WEART... : " + "ConnectButtonSubscriber else");

            foreach (var deviceHolder in _deviceHolders)
            {
                if ((deviceHolder.BleDevice.DeviceStatus == DeviceStatus.Connected || deviceHolder.isConnecting) &&
                    deviceHolder.handSide == holder.handSide)
                {
                    holder.changeHandButton.onClick.Invoke();
                }

                if (!deviceHolder.BleDevice.DeviceMacAddress.Equals(holder.BleDevice.DeviceMacAddress) &&
                    deviceHolder.BleDevice.DeviceStatus == DeviceStatus.Available &&
                    deviceHolder.handSide == holder.handSide)
                {
                    deviceHolder.changeHandButton.onClick.Invoke();
                }
            }
            
            _сonnectionProcessing = true;
            _connectingDevice = holder.BleDevice;
            
            SetHoldersInteractable(false);
            
            WeArtController.Instance.ConnectDevice(holder.BleDevice, holder.handSide);
            holder.UpdateDeviceStatus();
        }

        /// <summary>
        /// Subscribes Disconnect Button on device info panel.
        /// </summary>
        /// <param name="holder"></param>
        private void DisconnectButtonSubscribe(TouchDIVERInfo holder)
        {
            holder.DisconnectButton.gameObject.SetActive(false);
            holder.isDisconnectedManually = true;

            WeArtController.Instance.DisconnectDevice(holder.BleDevice);
            holder.UpdateDeviceStatus();
        }

        /// <summary>
        /// Subscribes ChangeHandButton on device info panel.
        /// </summary>
        /// <param name="holder"></param>
        private void ChangeHandButtonSubscribe(TouchDIVERInfo holder)
        {
            if (holder.RememberToggle.isOn) SaveDevices();
        }
        
        /// <summary>
        /// Subscribes the buttons on the panel.
        /// </summary>
        private void SubscribePanelButtons()
        {
            scrollUpButton.onClick.AddListener(ScrollUp);
            scrollDownButton.onClick.AddListener(ScrollDown);
            
            scrollUpButton.gameObject.SetActive(false);
            scrollDownButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Sets interactable state for all buttons at the panel.
        /// </summary>
        /// <param name="newState"></param>
        private void SetPanelInteractable(bool newState)
        {
            scrollUpButton.interactable = newState;
            scrollDownButton.interactable = newState;

            SetHoldersInteractable(newState);
        }

        /// <summary>
        /// Sets interactable state for buttons at all holders.
        /// </summary>
        /// <param name="newState"></param>
        private void SetHoldersInteractable(bool newState)
        {
            foreach (var holder in _deviceHolders)
            {
                holder.SetInteractable(newState);
            }
        }
        
        /// <summary>
        /// The handler for changes of ble device status in the WeArtMiddleware.   
        /// </summary>
        /// <param name="middleware"></param>
        private void UseUpdatedDeviceStatusFromMiddleware(DeviceStatusInfoFromMiddleware middleware)
        {
            WeArtLog.LogFile(LogLevel.DEBUG, PackTag.EVENTS, $"RECEIVED UPDATED STATUS: DEVICE ID {middleware.DeviceID}, STATUS {middleware.CurrentStatus}");
            
            if (middleware.CurrentStatus == CURRENT_STATUS.CONNECTED)
            {
                CheckConnectionStop(WeArtController.Instance.GetBleDeviceByID((WeArt.Core.DeviceID)middleware.DeviceID));
            }
        }

        #region ScrollMethods

        /// <summary>
        /// Updates internal data for correct work of scroll list.
        /// </summary>
        private void UpdateScrollData()
        {
            var newMaxScrollStep = _deviceHolders.Count > _maxDevicesAtDisplay ? _deviceHolders.Count - _maxDevicesAtDisplay : _minScrollStep;
            
            if (newMaxScrollStep != _maxScrollStep) _currentScrollStep += newMaxScrollStep - _maxScrollStep;

            _maxScrollStep = newMaxScrollStep;
            _scrollValueStep = 1.0f / _maxScrollStep;

            if (_currentScrollStep > _maxScrollStep) _currentScrollStep = _maxScrollStep;
            if (_currentScrollStep < _minScrollStep) _currentScrollStep = _minScrollStep;
            
            if (_currentScrollStep > _minScrollStep)
            {
                scrollDownButton.gameObject.SetActive(true);
                slidingArea.gameObject.SetActive(true);
            }
            
            if (_maxScrollStep == _minScrollStep)
            {
                scrollDownButton.gameObject.SetActive(false);
                slidingArea.gameObject.SetActive(false);
                scrollUpButton.gameObject.SetActive(false);
                return;
            }
            
            if (_currentScrollStep == _minScrollStep)
            {
                scrollDownButton.gameObject.SetActive(false);
            }

            if (_currentScrollStep == _maxScrollStep)
            {
                scrollUpButton.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// Scrolls list up.
        /// </summary>
        private void ScrollUp()
        {
            _currentScrollStep++;
            if (_currentScrollStep > _maxScrollStep) _currentScrollStep = _maxScrollStep;
            
            scrollBar.value = _currentScrollStep * _scrollValueStep;
            scrollDownButton.gameObject.SetActive(true);
            
            if (_currentScrollStep == _maxScrollStep) scrollUpButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Scrolls list down.
        /// </summary>
        private void ScrollDown()
        {
            _currentScrollStep--;
            if (_currentScrollStep < _minScrollStep) _currentScrollStep = _minScrollStep;
           
            scrollBar.value = _currentScrollStep * _scrollValueStep;
            scrollUpButton.gameObject.SetActive(true);
            
            if (_currentScrollStep == _minScrollStep) scrollDownButton.gameObject.SetActive(false);
        }
        
        #endregion
        
        #region Panel Placement
        
        /// <summary>
        /// Move device in front of camera.
        /// </summary>
        private void PlaceNearHandAnchor()
        {
            //StartCoroutine(PlaceNearHandAnchorCor());
        }

        private IEnumerator PlaceNearHandAnchorCor()
        {
            Transform mainCamera = Camera.main.transform;
            
            if (!mainCamera)
            {
                WeArtLog.Log("No Main Camera in the project!",LogType.Warning);
                yield break;
            }
            
            float distanceFromAnchor = 0.5f;

            yield return new WaitForSeconds(1f);

            var newPosition = mainCamera.position + mainCamera.forward * distanceFromAnchor;
            var directionToCamera = mainCamera.position - newPosition;
            
            transform.position = newPosition;
            transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
        
        #endregion

        #region Device serialization

        /// <summary>
        /// Saves the device to remember and its hand side  in application folder.
        /// </summary>
        private void SaveDevices()
        {
            _rememberedDevices.Clear();

            foreach (var holder in _deviceHolders)
            {
                if (!holder.RememberToggle.isOn) continue;

                _rememberedDevices.AddDevice(holder.BleDevice.DeviceMacAddress, holder.handSide);
            }

            if (_rememberedDevices.DeviceMacAddresses.Count > 0)
            {
                string json = JsonUtility.ToJson(_rememberedDevices);
                File.WriteAllText(_rememberedDataPath, json);

            } else if (File.Exists(_rememberedDataPath)) File.Delete(_rememberedDataPath);
        }

        /// <summary>
        /// Restores data from application folder.
        /// </summary>
        private void RememberDevices()
        {
            if (!File.Exists(_rememberedDataPath)) return;

            string json = File.ReadAllText(_rememberedDataPath);
            
            _rememberedDevices = JsonUtility.FromJson<RememberedDevices>(json);
        }
        
        #endregion
    }
}
