using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;

public class WeArtDeviceStatusDisplay : MonoBehaviour
{
    [SerializeField] internal WeArtStatusTracker tracker;
    [SerializeField] internal HandSide _handSide = HandSide.Left;
    [SerializeField] internal TextMeshProUGUI MacAddressText;
    [SerializeField] internal TextMeshProUGUI BatteryLevelText;
    [SerializeField] internal Image ChargingImage;
    [SerializeField] internal Image BatteryImage;
    [SerializeField] internal Transform NoBatteryImage;
    [SerializeField] internal GameObject InfoPanel;
    [SerializeField] internal TextMeshProUGUI CalibrationText;

    [SerializeField] internal Transform BatteryPercentagePanel;
    [SerializeField] internal Transform NoBatteryPanel;

    [Min(0)]   
    [SerializeField] internal float CalibrationFadeOutTimeSeconds = 10;
    [SerializeField] internal RawImage ThumbStatusLight;
    [SerializeField] internal RawImage IndexStatusLight;
    [SerializeField] internal RawImage MiddleStatusLight;
    [SerializeField] internal RawImage AnnularStatusLight;
    [SerializeField] internal RawImage PinkyStatusLight;
    [SerializeField] internal RawImage PalmStatusLight;
    [SerializeField] internal Image HandImage;
    [SerializeField] internal Color OkColor = Color.green;
    [SerializeField] internal Color ErrorColor = Color.red;
    [SerializeField] internal Color DisconnectedColor = Color.gray;
    [SerializeField] internal Color NormalColor = Color.white;

    private WeArtTrackingCalibration calibrationTracker;
    private CalibrationStatus calibrationStatus = CalibrationStatus.IDLE;

    private bool _clientConnected = false;
    private bool _deviceConnected = false;
    private bool _sessionRunning = false;
    private bool _touchDiverStatusReceived;
    private bool _touchDiverProStatusReceived;
    private DateTime _lastCalibrationCompletedTime = DateTime.MinValue;
    private DeviceStatusData _currentTDStatus = new DeviceStatusData();
    private TouchDiverProStatusData _currentTDProStatus = new TouchDiverProStatusData();
    private bool _standaloneAndroidActive = false;
    private string _firmwareVersion = "";

    /// <summary>
    /// The hand side of the thimble
    /// </summary>
    public HandSide HandSide
    {
        get => _handSide;
        set => _handSide = value;
    }
    
    private void OnEnable()
    {
        Init();
    }

    private void Start()
    {
        InitialPanelUpdate();
    }
    
    private void Update()
    {
        UpdatePanels();
        UpdateCalibrationStatus();
    }
    
    private void Init()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
            _standaloneAndroidActive = true;
#endif

        // Track middleware/devices status
        tracker.OnMiddlewareStatus.RemoveListener(OnDevicesStatus);
        tracker.OnMiddlewareStatus.AddListener(OnDevicesStatus);
        
        tracker.OnTouchDiverProStatus.RemoveListener(OnTouchDiverProStatus);
        tracker.OnTouchDiverProStatus.AddListener(OnTouchDiverProStatus);

        tracker.OnWeartAppStatus.RemoveListener(OnWeartAppStatus);
        tracker.OnWeartAppStatus.AddListener(OnWeartAppStatus);
        
        if (WeArtController.Instance is null)
            return;

        // Track calibration status
        calibrationTracker = WeArtController.Instance.gameObject.GetComponent<WeArtTrackingCalibration>();
        if (calibrationTracker != null)
        {
            calibrationTracker._OnCalibrationStart.RemoveListener(OnCalibrationStart);
            calibrationTracker._OnCalibrationStart.AddListener(OnCalibrationStart);

            calibrationTracker._OnCalibrationFinish.RemoveListener(OnCalibrationFinish);
            calibrationTracker._OnCalibrationFinish.AddListener(OnCalibrationFinish);
        }

        // Track connection status
        WeArtController.Instance.Client.OnConnectionStatusChanged -= OnConnectionChanged;
        WeArtController.Instance.Client.OnConnectionStatusChanged += OnConnectionChanged;
    }
    
    private void OnConnectionChanged(bool sdkConnected)
    {
        _clientConnected = sdkConnected;
        
        if (_clientConnected) return;
        
        _deviceConnected = false;
        _sessionRunning = false;
        calibrationStatus = CalibrationStatus.IDLE;
    }

    private void OnDevicesStatus(MiddlewareStatusData status)
    {
        _deviceConnected = false;
        _sessionRunning = _clientConnected && status.Status == MiddlewareStatus.RUNNING;
        _firmwareVersion = status.Version;
        _touchDiverStatusReceived = true;
        
        if (status.Devices == null) return;
        
        foreach (DeviceStatusData device in status.Devices)
        {
            if (device.HandSide != _handSide) continue;
            
            _deviceConnected = true;
            _currentTDStatus = device;
           
            return;
        }
        
    }

    private void OnCalibrationStart(HandSide handSide)
    {
        if (handSide != _handSide) return;
            
        calibrationStatus = CalibrationStatus.Calibrating;
    }

    private void OnCalibrationFinish(HandSide handSide)
    {
        if (handSide != _handSide)  return;
           
        calibrationStatus = CalibrationStatus.Running;
        _lastCalibrationCompletedTime = DateTime.Now;
    }

    private void UpdatePanels()
    {
        if (_touchDiverStatusReceived)
        {
            UpdateInfoPanel(_currentTDStatus.MacAddress, _currentTDStatus.BatteryLevel, _currentTDStatus.Charging);
            UpdateThimblesStatus(_currentTDStatus);
            _touchDiverStatusReceived = false;
        }

        if (_touchDiverProStatusReceived)
        {
            UpdateInfoPanel(_currentTDProStatus.MacAddress, _currentTDProStatus.Master.BatteryLevel, _currentTDProStatus.Master.Charging);
            UpdateThimblesStatus(_currentTDProStatus);
            _touchDiverProStatusReceived = false;
        }
    }
    
    private void UpdateInfoPanel(string deviceMacAddress, int batteryLevel, bool charging)
    {
        InfoPanel.SetActive(_deviceConnected);
        MacAddressText.text = _standaloneAndroidActive ? _firmwareVersion : deviceMacAddress;

        UpdateCalibrationStatus();
        UpdateBattery(batteryLevel, charging);
    }
    
    private void UpdateCalibrationStatus()
    {
        // Do not show calibration text if the session is not running (or the device is disconnected)
        /*
        if (!_sessionRunning)
        {
            CalibrationText.text = "";
            return;
        }*/

        switch (calibrationStatus)
        {
            case CalibrationStatus.IDLE: CalibrationText.text = ""; break;
            case CalibrationStatus.Calibrating: CalibrationText.text = "Calibrating..."; break;
            case CalibrationStatus.Running:
                {
                    // Stop showing calibration text after X seconds
                    bool timePassed = CalibrationFadeOutTimeSeconds > 0 && (DateTime.Now - _lastCalibrationCompletedTime).TotalSeconds > CalibrationFadeOutTimeSeconds;
                    CalibrationText.text = timePassed ? "" : "Calibrated!";
                }
                break;
        }
    }

    public void ForceUpdateBattery(int batteryLevel, bool charging)
    {
        UpdateBattery(batteryLevel, charging);
    }
    private void UpdateBattery(int batteryLevel, bool charging)
    {
        if (batteryLevel < 0)
        {
            BatteryPercentagePanel.gameObject.SetActive(false);
            NoBatteryImage.gameObject.SetActive(true);
            NoBatteryPanel.gameObject.SetActive(true);
            ChargingImage.enabled = false;
            BatteryImage.color = ErrorColor;
            return;
        }

        BatteryPercentagePanel.gameObject.SetActive(true);
        NoBatteryImage.gameObject.SetActive(false);
        NoBatteryPanel.gameObject.SetActive(false);
        BatteryImage.color = NormalColor;

        BatteryLevelText.text = "          "+batteryLevel + "%";
        ChargingImage.enabled = charging;
    }
    
    private void UpdateThimblesStatus(DeviceStatusData status)
    {
        HandImage.color = _deviceConnected ? Color.white : Color.gray;
        
        if (AnnularStatusLight.gameObject.activeInHierarchy) AnnularStatusLight.gameObject.SetActive(false);
        if (PinkyStatusLight.gameObject.activeInHierarchy) PinkyStatusLight.gameObject.SetActive(false);
        if (PalmStatusLight.gameObject.activeInHierarchy) PalmStatusLight.gameObject.SetActive(false);
        
        if (!_deviceConnected)
        {
            ThumbStatusLight.color = DisconnectedColor;
            IndexStatusLight.color = DisconnectedColor;
            MiddleStatusLight.color = DisconnectedColor;

            return;
        }

        foreach (var thimble in status.Thimbles)
        {
            Color color = TouchDiverThimbleColor(thimble.Connected, thimble.StatusCode != 0);
            switch (thimble.Id)
            {
                case ActuationPoint.Thumb:
                    ThumbStatusLight.color = color;
                    break;
                case ActuationPoint.Index:
                    IndexStatusLight.color = color;
                    break;
                case ActuationPoint.Middle:
                    MiddleStatusLight.color = color;
                    break;
            }
        }
    }

    private void UpdateThimblesStatus(TouchDiverProStatusData status)
    {
        HandImage.color = _deviceConnected ? Color.white : Color.gray;
        
        if (!AnnularStatusLight.gameObject.activeInHierarchy) AnnularStatusLight.gameObject.SetActive(true);
        if (!PinkyStatusLight.gameObject.activeInHierarchy) PinkyStatusLight.gameObject.SetActive(true);
        if (!PalmStatusLight.gameObject.activeInHierarchy) PalmStatusLight.gameObject.SetActive(true);
        
        if (!_deviceConnected)
        {
            ThumbStatusLight.color = DisconnectedColor;
            IndexStatusLight.color = DisconnectedColor;
            MiddleStatusLight.color = DisconnectedColor;
            AnnularStatusLight.color = DisconnectedColor;
            PinkyStatusLight.color = DisconnectedColor;
            PalmStatusLight.color = DisconnectedColor;

            return;
        }

        foreach (var thimble in status.Nodes)
        {
            Color color = TouchDiverProThimbleColor(thimble.Connected);
            switch (thimble.Id)
            {
                case ActuationPoint.Thumb:
                    ThumbStatusLight.color = color;
                    break;
                case ActuationPoint.Index:
                    IndexStatusLight.color = color;
                    break;
                case ActuationPoint.Middle:
                    MiddleStatusLight.color = color;
                    break;
                case ActuationPoint.Palm:
                    PalmStatusLight.color = color;
                    break;
                case ActuationPoint.Annular:
                    AnnularStatusLight.color = color;
                    break;
                case ActuationPoint.Pinky:
                    PinkyStatusLight.color = color;
                    break;
            }
        }
    }
    
    
    private Color TouchDiverThimbleColor(bool connected, bool ok)
    {
        return connected ? (ok ? ErrorColor : OkColor) : DisconnectedColor;
    }

    private Color TouchDiverProThimbleColor(bool connected)
    {
        return connected ? OkColor : ErrorColor;
    }
    
    private void OnTouchDiverProStatus(TouchDiverProStatus status)
    {
        _deviceConnected = false;
        _touchDiverProStatusReceived = true;
        
        if (status.Devices == null) return;
        
        foreach (var device in status.Devices)
        {
            if (device.HandSide != this.HandSide) continue;
            
            _currentTDProStatus = device;
            _deviceConnected = true;
            
            return;
        }
        
    }

    private void OnWeartAppStatus(WeartAppStatusMessage status)
    {
        _sessionRunning = _clientConnected && status.Status == MiddlewareStatus.RUNNING;
        _firmwareVersion = status.Version;
    }

    private void InitialPanelUpdate()
    {
        UpdateInfoPanel(MacAddressText.text, 0, false);
        UpdateThimblesStatus(_currentTDStatus);
    }
    
}
