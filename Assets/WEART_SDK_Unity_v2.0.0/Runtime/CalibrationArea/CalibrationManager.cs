using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;
using WeArt.Utils;

public class CalibrationManager : MonoBehaviour
{
    #region Fields
    public System.Action OnInitialized = delegate { };

    public enum HandType { Left = 0, Right = 1 };

    [SerializeField] WeArtController weArtController;

    [Space, SerializeField] GameObject handCalibrationRoot;

    [Space, SerializeField] ColliderHandle triggerRightHand;
    [SerializeField] ColliderHandle triggerLeftHand;

    [Space, SerializeField] HandCalibrationState holoHandL;
    [SerializeField] HandCalibrationState holoHandR;

    [Space, SerializeField] Transform weArtControllerRightHand;
    [SerializeField] Transform weArtControllerLeftHand;

    [SerializeField] GameObject WEARTRightHand;
    [SerializeField] GameObject WEARTLeftHand;

    [Space, SerializeField] CalibrationInfoField infoField;
    
    [SerializeField] AudioEvents audioEvents;

    [Range(0, 2)] private int maxHands = 0;
    private HandType dominantHand = HandType.Right;
    private float _calibrationTimeout = 5f;
    
    private WeArtTrackingCalibration trackingCalibration;
    private bool isCalibrated = false;
    private int nTriggerHands = 0;
    private bool isAligned = false;
    public bool IsAligned { get { return isAligned; } }
    private bool ignore_calibration = false;
    private float t_timeout = 0f;

    private bool StandaloneActive = false;

    private ConnectedDevices connectedDevices = new ConnectedDevices(new System.Collections.Generic.List<ITouchDiverData>(), false);
    
    #endregion

    #region Default Methods
    private void OnEnable()
    {
        trackingCalibration = weArtController.gameObject.GetComponent<WeArtTrackingCalibration>();

        if (!trackingCalibration)
        {
            WeArtLog.Log("CalibrationAreaManager: you have to assign tracking calibration component", LogType.Warning);
        }

        triggerLeftHand.TriggerEnter += CalibrateStartLeft;
        triggerLeftHand.TriggerStay += CalibrateUpdate;
        triggerLeftHand.TriggerExit += CalibrateExit;

        triggerRightHand.TriggerEnter += CalibrateStartRight;
        triggerRightHand.TriggerStay += CalibrateUpdate;
        triggerRightHand.TriggerExit += CalibrateExit;

        WeArtStatusTracker.ConnectedDevicesReady += ConnectedHands;

#if UNITY_ANDROID && !UNITY_EDITOR
        StandaloneActive = true;
#endif
    }

    private void ConnectedHands(ConnectedDevices connectedDev)
    {
        connectedDevices = connectedDev;
    }

    private void OnDisable()
    {
        triggerLeftHand.TriggerEnter -= CalibrateStartLeft;
        triggerLeftHand.TriggerStay -= CalibrateUpdate;
        triggerLeftHand.TriggerExit -= CalibrateExit;

        triggerRightHand.TriggerEnter -= CalibrateStartRight;
        triggerRightHand.TriggerStay -= CalibrateUpdate;
        triggerRightHand.TriggerExit -= CalibrateExit;
    }

    private void Awake()
    {
        weArtController = FindObjectOfType<WeArtController>();

        if (weArtController == null)
        {
            WeArtLog.Log("There is no WeArtController in the scene.",LogType.Error);
        }
        else
        {
            if (weArtController._startCalibrationAutomatically == true)
            {
                WeArtLog.Log("If you are using the CalibrationManager prefab, make sure to disable StartCalibrationAutomatically from WeArtController.", LogType.Error);
            }

            WeArtTrackingCalibration weArtTrackingCalibration = weArtController.gameObject.GetComponent<WeArtTrackingCalibration>();

            if (weArtTrackingCalibration == null)
            {
                WeArtLog.Log("There is no WeArtTrackingCalibration on the WeArtController.", LogType.Error);
            }
            else
            {
                weArtTrackingCalibration._OnCalibrationStart.RemoveListener(ValidateProcess);
                weArtTrackingCalibration._OnCalibrationStart.AddListener(ValidateProcess);

                weArtTrackingCalibration._OnCalibrationResultSuccess.RemoveListener(CalibrationCompleted);
                weArtTrackingCalibration._OnCalibrationResultSuccess.AddListener(CalibrationCompleted);
            }
        }

        WeArtHandController[] deviceTrackingObjects = FindObjectsOfType<WeArtHandController>();

        foreach (var item in deviceTrackingObjects)
        {
            if (item._handSide == HandSide.Right) weArtControllerRightHand = item.transform;
            if (item._handSide == HandSide.Left) weArtControllerLeftHand = item.transform;
        }

        if (weArtControllerRightHand == null)
        {
            WeArtLog.Log("There is no WEARTRightHand in the scene.", LogType.Error);
        }

        if (weArtControllerLeftHand == null)
        {
            WeArtLog.Log("There is no WEARTLeftHand in the scene.",LogType.Error);
        }
    }

    void Start()
    {
        ResetCalibration();

        WEARTLeftHand.SetActive(true);
        WEARTRightHand.SetActive(true);
    }

    /// <summary>
    /// Call this method when scene start. It reset all stuffs before was initialized
    /// </summary>
    private void ResetParams()
    {
        holoHandL.gameObject.SetActive(false);
        holoHandR.gameObject.SetActive(false);
        infoField.Show("Hand calibration will start shortly. Please wait.", false);
    }

    void Update()
    {
        /*
        if (Input.GetKeyDown(KeyCode.F1))
        { 
            WeArtController.Instance.SendMessage(new GetMiddlewareStatusMessage());
            WeArtController.Instance.SendMessage(new GetDevicesStatusMessage()); 
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            weArtController.Client.StartCalibration();
            WeArtLog.Log("start cali");
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            weArtController.Client.StopCalibration();
            WeArtLog.Log("stop cali");
        }
        */
    }
#endregion

    #region Event Trigger 

    void CalibrateStartRight(Collider other)
    {
        if (isCalibrated)
            return;

        if (other.gameObject.name != "RightHapticIndex")
            return;

        if (other.GetComponent<WeArtHapticObject>().HandSides == HandSideFlags.Right)
        {
            holoHandR.SetState(HandCalibrationState.State.Calibrating);
            nTriggerHands++;
        }

        if (IsHandAlignedCorrectly())
        {
            weArtController.Client.StartCalibration();
            infoField.Show(CalibrationPhrases.instance.OnHandEnter, true);
            audioEvents.Play(audioEvents.onStart);
        }

        infoField.SetIcon(CalibrationInfoField.MessageType.None);
        ignore_calibration = false;
        t_timeout = 0f;
    }

    void CalibrateStartLeft(Collider other)
    {
        if (isCalibrated)
            return;

        if (other.gameObject.name != "LeftHapticIndex")
            return;

        if (other.GetComponent<WeArtHapticObject>().HandSides == HandSideFlags.Left)
        {
            holoHandL.SetState(HandCalibrationState.State.Calibrating);
            nTriggerHands++;
        }

        if (IsHandAlignedCorrectly())
        {
            weArtController.Client.StartCalibration();
            infoField.Show(CalibrationPhrases.instance.OnHandEnter, true);
            audioEvents.Play(audioEvents.onStart);
        }

        infoField.SetIcon(CalibrationInfoField.MessageType.None);
        ignore_calibration = false;
        t_timeout = 0f;
    }

    void CalibrateUpdate(Collider other)
    {
        if (other.gameObject.name != "RightHapticIndex" && other.gameObject.name != "LeftHapticIndex")
            return;

        if (isCalibrated)
            return;

        if (ignore_calibration)
            return;


        // Otherwise check the calibration status
        if (maxHands == 1)
        {
            if (IsHandAlignedCorrectly())
            {
                if (maxHands == 1)
                {
                    if (dominantHand == HandType.Left)
                    {
                        holoHandL.SetState(HandCalibrationState.State.Calibrating);
                    }
                    else
                    {
                        holoHandR.SetState(HandCalibrationState.State.Calibrating);
                    }
                }
                UpdateTimeoutStatus();
            }
            else
            {
                CalibrationAborted("hand_exit");
            }
        }
        else
        {
            if (IsHandAlignedCorrectly())
            {
                infoField.Show(CalibrationPhrases.instance.OnRequestTwoHands, true);

                if (other.GetComponent<WeArtHapticObject>().HandSides == HandSideFlags.Right)
                {
                    UpdateTimeoutStatus();
                }
            }
            else
            {
                infoField.Show(CalibrationPhrases.instance.OnRequestTwoHands, false);
            }
        }
    }

    void CalibrateExit(Collider other)
    {
        if (isCalibrated)
            return;

        if (ignore_calibration)
            return;

        if (other.gameObject.name != "RightHapticIndex" && other.gameObject.name != "LeftHapticIndex")
            return;

        if (other.GetComponent<WeArtHapticObject>().HandSides == HandSideFlags.Left)
        {
            nTriggerHands--;
        }

        if (other.GetComponent<WeArtHapticObject>().HandSides == HandSideFlags.Right)
        {
            nTriggerHands--;
        }

        CalibrationAborted("hand_exit");
    }

    #endregion

    #region Private Methods
    void InitCalibration()
    {
        WEARTRightHand.SetActive(true);
        WEARTLeftHand.SetActive(true);

        if (WeArtController.Instance._startCalibrationAutomatically)
        {
            infoField.ShowStartAutomaticCalibrationWindow(true);

            holoHandR.gameObject.SetActive(false);
            holoHandL.gameObject.SetActive(false);

            triggerRightHand.gameObject.SetActive(false);
            triggerLeftHand.gameObject.SetActive(false);

            isCalibrated = true;

            return;
        }

        switch (maxHands)
        {
            case 0:
                if (StandaloneActive)
                    infoField.ShowStartWindow(true);
                else 
                    infoField.ShowStartWindowPC(true);

                holoHandR.gameObject.SetActive(false);
                holoHandL.gameObject.SetActive(false);

                triggerRightHand.gameObject.SetActive(false);
                triggerLeftHand.gameObject.SetActive(false);

                return;
            case 1:
                {
                    switch (dominantHand)
                    {
                        case HandType.Right:

                            holoHandR.gameObject.SetActive(true);
                            holoHandL.gameObject.SetActive(false);

                            triggerRightHand.gameObject.SetActive(true);
                            triggerLeftHand.gameObject.SetActive(false);

                            break;

                        case HandType.Left:

                            holoHandR.gameObject.SetActive(false);
                            holoHandL.gameObject.SetActive(true);

                            triggerRightHand.gameObject.SetActive(false);
                            triggerLeftHand.gameObject.SetActive(true);

                            break;
                    }

                    break;
                }
            case 2:

                holoHandL.gameObject.SetActive(true);
                holoHandR.gameObject.SetActive(true);

                triggerRightHand.gameObject.SetActive(true);
                triggerLeftHand.gameObject.SetActive(true);

                break;
        }

        if (StandaloneActive)
            infoField.ShowStartWindow(false);
        else
            infoField.ShowStartWindowPC(false);
        

        holoHandL.SetState(HandCalibrationState.State.Idle);
        holoHandR.SetState(HandCalibrationState.State.Idle);

        ResetVariables();
        infoField.Show(CalibrationPhrases.instance.OnStart, false, true);
        infoField.SetIcon(CalibrationInfoField.MessageType.None);
    }

    void ResetVariables()
    {
        isCalibrated = false;
        isAligned = false;
        nTriggerHands = 0;
        t_timeout = 0f;
    }

    /// <summary>
    /// Update the timeout status. If it reach the limit just return CalibrationAborted event
    /// </summary>
    void UpdateTimeoutStatus()
    {
        if (t_timeout >= _calibrationTimeout)
        { 
            CalibrationAborted("timeout");
            t_timeout = 0f;
            return;
        }

        float countdown = Mathf.Abs(_calibrationTimeout - t_timeout);
        infoField.SetTime(countdown);
        t_timeout += Time.deltaTime;
    }

    /// <summary>
    /// Check if the hand is inside the trigger area
    /// </summary>
    /// <returns></returns>
    bool IsHandAlignedCorrectly()
    {
        if (nTriggerHands == maxHands)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check if the object inside the trigger is an hand
    /// </summary>
    /// <param name="other"></param>
    /// <param name="hand"></param>
    /// <returns></returns>
    bool TryGetHand(GameObject other, out WeArtDeviceTrackingObject hand)
    {
        hand = other.GetComponent<WeArtDeviceTrackingObject>();
        if (hand != null)
            return true;
        return false;
    }

    #endregion

    #region Public Methods

    public void ResetCalibration()
    {
        InitCalibration();
    }

    public void ValidateProcess(HandSide hand)
    {

    }

    public void CalibrationCompleted(HandSide hand)
    {
        if (WeArtController.Instance._startCalibrationAutomatically)
        {
            infoField.Hide();

            SetHandsOnScene(connectedDevices);
            return;
        }

        if (maxHands > 1)
        {
            if (!IsHandAlignedCorrectly())
            {
                return;
            }
        }
        
        audioEvents.Play(audioEvents.onSuccess);
        infoField.Show(CalibrationPhrases.instance.OnSuccess, false, 2.5f);
        infoField.SetIcon(CalibrationInfoField.MessageType.Success);
        
        isCalibrated = true;
        ignore_calibration = true;

        // Hand holo behaviour
        if (maxHands == 1)
        {
            if (dominantHand == HandType.Left)
            {
                StartCoroutine(HandleUI_Success(holoHandL));
                SetHandsOnScene(connectedDevices);
            }
            else
            {
                StartCoroutine(HandleUI_Success(holoHandR));
                SetHandsOnScene(connectedDevices);
            }
        }
        else
        {
            StartCoroutine(HandleUI_Success(holoHandL));
            StartCoroutine(HandleUI_Success(holoHandR));

            SetHandsOnScene(connectedDevices);
        }
        
    }

    public void CalibrationAborted(string reason)
    {
        if (isCalibrated)
            return;

        if (maxHands == 1)
        {
            if (dominantHand == HandType.Left)
            {
                if (reason == "hand_exit")
                {
                    StartCoroutine(HandleUI_Aborted(holoHandL, false));
                    infoField.Show(CalibrationPhrases.instance.OnHandExit, false);
                    infoField.SetIcon(CalibrationInfoField.MessageType.Error);

                    weArtController.Client.StopCalibration();
                }
                else if (reason == "wrong_calibration")
                {
                    holoHandL.gameObject.SetActive(false);

                    infoField.Show(CalibrationPhrases.instance.OnCalibrationError, false, 5f);
                    infoField.SetIcon(CalibrationInfoField.MessageType.Warning);
                }
                else if (reason == "timeout")
                {
                    StartCoroutine(HandleUI_Aborted(holoHandL, false));
                    infoField.Show(CalibrationPhrases.instance.OnTimeout, false);
                    infoField.SetIcon(CalibrationInfoField.MessageType.Error);

                    weArtController.Client.StopCalibration();
                }
            }
            else
            {
                if (reason == "hand_exit")
                {
                    StartCoroutine(HandleUI_Aborted(holoHandR, false));
                    infoField.Show(CalibrationPhrases.instance.OnHandExit, false);
                    infoField.SetIcon(CalibrationInfoField.MessageType.Error);

                    weArtController.Client.StopCalibration();
                }
                else if (reason == "wrong_calibration")
                {
                    holoHandR.gameObject.SetActive(false);

                    infoField.Show(CalibrationPhrases.instance.OnCalibrationError, false, 5f);
                    infoField.SetIcon(CalibrationInfoField.MessageType.Warning);
                }
                else if (reason == "timeout")
                {
                    StartCoroutine(HandleUI_Aborted(holoHandR, false));
                    infoField.Show(CalibrationPhrases.instance.OnTimeout, false);
                    infoField.SetIcon(CalibrationInfoField.MessageType.Error);

                    weArtController.Client.StopCalibration();
                }
            }
        }
        else if (maxHands == 2)
        {
            if (reason == "hand_exit")
            {
                StartCoroutine(HandleUI_Aborted(holoHandL, false));
                StartCoroutine(HandleUI_Aborted(holoHandR, false));

                infoField.Show(CalibrationPhrases.instance.OnHandExit, false);
                infoField.SetIcon(CalibrationInfoField.MessageType.Error);

                weArtController.Client.StopCalibration();
            }
            else if (reason == "wrong_calibration")
            {
                holoHandL.gameObject.SetActive(false);
                holoHandR.gameObject.SetActive(false);

                infoField.Show(CalibrationPhrases.instance.OnCalibrationError, false, 5f);
                infoField.SetIcon(CalibrationInfoField.MessageType.Warning);
            }
            else if (reason == "timeout")
            {
                StartCoroutine(HandleUI_Aborted(holoHandL, false));
                StartCoroutine(HandleUI_Aborted(holoHandR, false));

                infoField.Show(CalibrationPhrases.instance.OnTimeout, false);
                infoField.SetIcon(CalibrationInfoField.MessageType.Error);

                weArtController.Client.StopCalibration();
            }
        }

        audioEvents.Play(audioEvents.onFailed);
        ResetVariables();
        ignore_calibration = true;
    }

    private void SetHandsOnScene(ConnectedDevices connectedDevices)
    {

        if (connectedDevices.Devices.Count < 1)
        {
            return;
        }
        if (connectedDevices.MiddlewareRunning)
        {
            if (connectedDevices.Devices.Count < 2)
            {
                if (connectedDevices.Devices[0].HandSide == HandSide.Right)
                {
                    WEARTLeftHand.SetActive(false);
                    return;
                }
                else
                {
                    WEARTRightHand.SetActive(false);
                }
            }
            else
            {
                WEARTLeftHand.SetActive(true);
                WEARTRightHand.SetActive(true);
            }
        }
        else
        {
            WEARTLeftHand.SetActive(true);
            WEARTRightHand.SetActive(true);
        }
    }


    /// <summary>
    /// If you using Calibration, you need to force on the haptic by this function
    /// </summary>
    public void ForceEnableHaptics()
    {
        //WeArtController.Instance.EnableActuation = true;
    }

    /// <summary>
    /// Sets data of connected devices and dominated hand side if device is only one. 
    /// </summary>
    /// <param name="handQuantity"></param>
    /// <param name="dominantHandType"></param>
    public void SetHandDominationData(int handQuantity, HandType dominantHandType = HandType.Right)
    {
        maxHands = handQuantity;
        dominantHand = dominantHandType;
        ResetCalibration();
    }
    
    #endregion

    #region Coroutines
    IEnumerator HandleUI_Success(HandCalibrationState holoHand)
    {
        holoHand.SetState(HandCalibrationState.State.Success);
        yield return new WaitForSeconds(1f);
        holoHand.gameObject.SetActive(false);
    }

    IEnumerator HandleUI_Aborted(HandCalibrationState holoHand, bool hideAtTheEnd)
    {
        holoHand.SetState(HandCalibrationState.State.Failed);
        yield return new WaitForSeconds(1f);

        if (hideAtTheEnd)
        {
            holoHand.gameObject.SetActive(false);
        }
        else
        {
            holoHand.SetState(HandCalibrationState.State.Idle);
        }
    }


    #endregion

    #region Custom Classes

    [System.Serializable]
    public class AudioEvents
    {
        public AudioClip onStart;
        public AudioClip onFailed;
        public AudioClip onSuccess;

        public AudioSource source;

        /// <summary>
        /// Check if audiosource exist in the contest
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            return source != null;
        }

        public void Play(AudioClip clip)
        {
            if (IsValid())
            {
                source.PlayOneShot(clip);
            }
        }
    }

    #endregion
}
