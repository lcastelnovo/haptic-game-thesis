using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;
using WeArt.Utils;

public class CalibrationAreaManager : MonoBehaviour
{
    #region Fields
    public System.Action OnInitialized = delegate { };

    [SerializeField] WeArtController weArtController;
    [SerializeField] WeArtTrackingCalibration trackingCalibration;
    
    [Space,SerializeField] GameObject handCalibrationRoot;

    [Space, SerializeField] ColliderHandle triggerRightHand;
    [SerializeField] ColliderHandle triggerLeftHand;

    [Space, SerializeField] HandCalibrationState holoHandL;
    [SerializeField] HandCalibrationState holoHandR;

    [Space, SerializeField] Transform weartTrackingRightHand;
    [SerializeField] Transform weartTrackingLeftHand;

    [SerializeField] Transform fakeRightHand;
    [SerializeField] Transform fakeLeftHand;

    [Space, SerializeField] CalibrationInfoField infoField;
    [SerializeField] float startWaitingTime = 2f;
    
    private bool isCalibrated = false;
    //private float currentTime = 0f;
    //private float CALIBRATION_TIME = 5f;
    private GameObject HandInstanceRight;
    private GameObject HandInstanceLeft;
    private int nTriggerHands = 0;
    private bool isAligned = false;
    public bool IsAligned { get { return isAligned; }}

    /// <summary>
    /// With this boolean avoid to call multiple times, start and stop client 
    /// </summary>
    private bool _clientStarted = false;
    private bool _isMiddlewareReady = false;

    private bool StandaloneAndroidIsActive = false;

    #endregion

    #region Default Methods
    private void OnEnable()
    {

#if UNITY_ANDROID && !UNITY_EDITOR
        StandaloneAndroidIsActive = true;        
#endif

        if (!trackingCalibration)
        {
            WeArtLog.Log("CalibrationAreaManager: you have to assign tracking calibration component", LogType.Warning);
        }

        triggerLeftHand.TriggerEnter += CalibrateStart;
        triggerLeftHand.TriggerStay += CalibrateUpdate;
        triggerLeftHand.TriggerExit += CalibrateExit;

        triggerRightHand.TriggerEnter += CalibrateStart;
        triggerRightHand.TriggerStay += CalibrateUpdate;
        triggerRightHand.TriggerExit += CalibrateExit;

        
        OnInitialized += GetHandsInstance;
        OnInitialized += InitCalibration;
    }

    private void OnDisable()
    {
        triggerLeftHand.TriggerEnter -= CalibrateStart;
        triggerLeftHand.TriggerStay -= CalibrateUpdate;
        triggerLeftHand.TriggerExit -= CalibrateExit;

        triggerRightHand.TriggerEnter -= CalibrateStart;
        triggerRightHand.TriggerStay -= CalibrateUpdate;
        triggerRightHand.TriggerExit -= CalibrateExit;
       
        OnInitialized -= GetHandsInstance;
        OnInitialized -= InitCalibration;
    }

    void Start()
    {
        //InitCalibration();
        ResetParams();
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
        UpdateFakeHandTracking();

        if (Input.GetKeyDown(KeyCode.F1))
            ResetCalibration();

    }
    #endregion

    #region Event Trigger 

    void CalibrateStart(Collider other)
    {
        if (TryGetHand(other.gameObject, out WeArtDeviceTrackingObject hand))
        {
            if (isCalibrated)
                return;

            if (hand.gameObject.name.Contains("Left"))
            {
                holoHandL.SetState(HandCalibrationState.State.Calibrating);
                nTriggerHands++;
            }

            if (hand.gameObject.name.Contains("Right"))
            {
                holoHandR.SetState(HandCalibrationState.State.Calibrating);
                nTriggerHands++;
            }

            ConnectClient();

            infoField.Show(CalibrationPhrases.instance.OnHandEnter, true);
        }

    }

    void CalibrateUpdate(Collider other)
    {
        if(TryGetHand(other.gameObject, out WeArtDeviceTrackingObject hand))
        {
            // if is calibrated yet, just ignore

            if (isCalibrated)
                return;

            // If the middleware is not ready during the first second, ignoring calibration

            if(!_isMiddlewareReady)
            {
                return;
            }

            // Otherwise check the calibration status

            if(!IsHandAlignedCorrectly())
            {
                CalibrationAborted();
            }

        }
    }

    void CalibrateExit(Collider other)
    {
        if (TryGetHand(other.gameObject, out WeArtDeviceTrackingObject hand))
        {
            if (isCalibrated)
                return;

            if (hand.gameObject.name.Contains("Left"))
            {
                nTriggerHands--;
            }

            if (hand.gameObject.name.Contains("Right"))
            {
                nTriggerHands--;
            }

            if(_isMiddlewareReady)
                CalibrationAborted();

            //currentTime = 0f;

            infoField.Show(CalibrationPhrases.instance.OnHandExit, false);
        }
    }

    #endregion

    #region Private Methods
    void InitCalibration()
    {
        holoHandL.SetState(HandCalibrationState.State.Idle);
        holoHandR.SetState(HandCalibrationState.State.Idle);

        // Set as default these parameters
        isCalibrated = false;
        isAligned = false;
        _isMiddlewareReady = false;
        nTriggerHands = 0;

        infoField.Show(CalibrationPhrases.instance.OnStart, false);
        
    }

    /// <summary>
    /// It get hand instance based on clap preferences (one-two hands or dominant hands)
    /// </summary>
    void GetHandsInstance()
    {
       
    }

    /// <summary>
    /// Update the tracking about the fake hand(s)
    /// </summary>
    void UpdateFakeHandTracking()
    {
        
    }

    /// <summary>
    /// Check if the hand is inside the trigger area
    /// </summary>
    /// <returns></returns>
    bool IsHandAlignedCorrectly()
    {
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

    /// <summary>
    /// Perfomed action to start the middleware
    /// </summary>
    void ConnectClient()
    {
        if (!StandaloneAndroidIsActive) 
        { 
            if (!weArtController.Client.IsConnected && !_clientStarted)
            {
                weArtController.Client.Start();
                _clientStarted = true;

                StartCoroutine(DelayStartClient(startWaitingTime));
            }
        }
    }

    /// <summary>
    /// Performed action to disconnect the middleware and avoid to recall it immediately after
    /// </summary>
    void DisconnectClient()
    {
        if (!StandaloneAndroidIsActive)
        {
            if (weArtController.Client.IsConnected && _clientStarted)
            {
                //trackingCalibration.ResetState();
                weArtController.Client.Stop();

                //StopCoroutine(DelayStopClient(5f));
                StartCoroutine(DelayStopClient(5f));
            }
        }
    }

    #endregion

    #region Public Methods

    public void UpdateCalibrationStatus()
    {
        if (!IsHandAlignedCorrectly())
        {
            CalibrationAborted();
            //WeArtLog.Log("event calibration fail");
        }
        else
        {
            //WeArtLog.Log("event calibration success");
        }
    }

    public void ResetCalibration()
    {
        DisconnectClient();
        InitCalibration();
    }

    public void CalibrationCompleted(HandSide hand)
    {
        // Switch from fake hand to real hand
        if (fakeRightHand.gameObject.activeInHierarchy)
        {
            fakeRightHand.gameObject.SetActive(false);
            HandInstanceRight.SetActive(true);
            infoField.Hide();
        }
        if (fakeLeftHand.gameObject.activeInHierarchy)
        {
            fakeLeftHand.gameObject.SetActive(false);
            HandInstanceLeft.SetActive(true);
            infoField.Hide();
        }

        infoField.Show(CalibrationPhrases.instance.OnSuccess, false, 3f);

        isCalibrated = true;

        //WeArtLog.Log("Calibration completed succesfully");
    }

    public void CalibrationAborted()
    {
        if (isCalibrated)
            return;

        DisconnectClient();
        
        infoField.Show(CalibrationPhrases.instance.OnCalibrationError, false);
    }

    #endregion

    #region Coroutines
    IEnumerator HandleUI_Success(HandCalibrationState holoHand)
    {
        holoHand.SetState(HandCalibrationState.State.Success);
        yield return new WaitForSeconds(1f);
        holoHand.gameObject.SetActive(false);
    }

    IEnumerator HandleUI_Aborted(HandCalibrationState holoHand)
    {
        holoHand.SetState(HandCalibrationState.State.Failed);
        yield return new WaitForSeconds(1f);
        holoHand.SetState(HandCalibrationState.State.Idle);
    }

    /// <summary>
    /// Delay the changes from true to false for the client. This because the middleware spend some seconds
    /// to stopping. With this you doesnt allow the user to re-start middleware during this waiting
    /// </summary>
    /// <param name="seconds"></param>
    /// <returns></returns>
    IEnumerator DelayStopClient(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _clientStarted = false;
        _isMiddlewareReady = false;
    }

    IEnumerator DelayStartClient(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _isMiddlewareReady = true;
        UpdateCalibrationStatus();
    }
    #endregion
}
