using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using WeArt.Components;
using WeArt.Core;
using WEART;
using Texture = WeArt.Core.Texture;


[RequireComponent(typeof(WeArtHandController))]
public class WeArtHandGraspingSystem : MonoBehaviour
{
    /// <summary>
    /// Defines the object that will be the parent of the the grasped objects
    /// </summary>
    [SerializeField]
    internal GameObject _grasper;

    /// <summary>
    /// Defines the location where the hand will check for touchable objects with snap grasping
    /// </summary>
    [SerializeField]
    internal Transform _snapGraspProximityTransform;

    /// <summary>
    /// Defines the finger's proximity collider
    /// </summary>
    [SerializeField]
    internal WeArtGraspCheck _thumbProximityColider, _indexProximityColider, _middleProximityColider, _annularProximityColider, _pinkyProximityColider;
    
    /// <summary>
    /// Defines the hand controller component
    /// </summary>
    private WeArtHandController _handController;

    /// <summary>
    /// Defines the surface exploration component
    /// </summary>
    private WeArtHandSurfaceExploration _surfaceExploration;

    /// <summary>
    /// Defines the grasped object
    /// </summary>
    private WeArtTouchableObject _graspedObject;

    /// <summary>
    /// Defines if the hand is grasping an object or not
    /// </summary>
    private GraspingState _graspingState = GraspingState.Released;

    // Read only values of the grasping effects
    private readonly WeArtTouchEffect _thumbGraspingEffect = new WeArtTouchEffect();
    private readonly WeArtTouchEffect _indexGraspingEffect = new WeArtTouchEffect();
    private readonly WeArtTouchEffect _middleGraspingEffect = new WeArtTouchEffect();
    private readonly WeArtTouchEffect _annularGraspingEffect = new WeArtTouchEffect();
    private readonly WeArtTouchEffect _pinkyGraspingEffect = new WeArtTouchEffect();

    /// <summary>
    /// Defines the finger's thimble tracking component
    /// </summary>
    private WeArtThimbleTrackingObject _thumbThimbleTracking, _indexThimbleTracking, _middleThimbleTracking, _annularThimbleTracking, _pinkyThimbleTracking;

    /// <summary>
    /// Defines the finger's haptic object component
    /// </summary>
    private WeArtHapticObject _thumbThimbleHaptic, _indexThimbleHaptic, _middleThimbleHaptic, _palmThimbleHaptic, _annularThimbleHaptic, _pinkyThimbleHaptic;

    /// <summary>
    /// Grasping delegte for grasping or releaseing an object
    /// </summary>
    /// <param name="handSide"></param>
    /// <param name="gameObject"></param>
    public delegate void GraspingDelegate(HandSide handSide, GameObject gameObject);

    /// <summary>
    /// Grasping delegate
    /// </summary>
    public GraspingDelegate OnGraspingEvent;

    /// <summary>
    /// Release Delegate
    /// </summary>
    public GraspingDelegate OnReleaseEvent;

    // Lists of objects touched by hand's every haptic object
    private List<WeArtTouchableObject> _thumbContactTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _indexContactTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _middleContactTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _annularContactTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _pinkyContactTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _palmContactTouchables = new List<WeArtTouchableObject>();

    // Lists of objects touched by hand's every proximity collider
    private List<WeArtTouchableObject> _thumbProximityTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _indexProximityTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _middleProximityTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _annularProximityTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _pinkyProximityTouchables = new List<WeArtTouchableObject>();

    // Lists of obects that are blocking the closure of fingers
    private List<WeArtTouchableObject> _thumbBlockingTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _indexBlockingTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _middleBlockingTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _annularBlockingTouchables = new List<WeArtTouchableObject>();
    private List<WeArtTouchableObject> _pinkyBlockingTouchables = new List<WeArtTouchableObject>();

    // Variables defining if a finger is able to start grasping an object
    private bool _isThumbTryingToGrasp = false;
    private bool _isIndexTryingToGrasp = false;
    private bool _isMiddleTryingToGrasp = false;
    private bool _isAnnularTryingToGrasp = false;
    private bool _isPinkyTryingToGrasp = false;

    /// <summary>
    /// Defines if the grasping system is using the palm for the current grasping situation
    /// </summary>
    private bool _isGraspingWithPalm = false;

    // Variables that restrict grasping after releasing an object
    private float _notAllowedToGrabSeconds = 0f;
    private float _notAllowedToGrabSecondsMax = 1f;
    private float _intentionalGraspThreshold = 0.1f;

    // Snap grasping variables
    private float _tryingToGraspThreshhold = 0.1f;
    private float _lastThumbClosure;
    private float _lastIndexClosure;
    private float _lastMiddleClosure;
    private float _lastAnnularClosure;
    private float _lastPinkyClosure;

    /// <summary>
    /// Hand side of the parent
    /// </summary>
    private HandSide _handSide;

    /// <summary>
    /// The opposide hand's hand controller
    /// </summary>
    private WeArtHandController _otherHand;

    /// <summary>
    /// The opposite hand's grasping system
    /// </summary>
    private WeArtHandGraspingSystem _otherGraspingSystem;

    /// <summary>
    /// Defines the device tracking object component on the same game object
    /// </summary>
    private WeArtDeviceTrackingObject _deviceTrackingObject;

    /// <summary>
    /// List for holding the touchable objects that have snap grasping and are inside the proximity if the hand
    /// </summary>
    private List<WeArtTouchableObject> _snapGraspObjectsInProximity = new List<WeArtTouchableObject>();

    /// <summary>
    /// Container for the touchable objects found insde the snap grasp proximity
    /// </summary>
    private Collider[] _snapGraspColliders;

    /// <summary>
    /// Defines the closes touchable object with snap grasping active. Will be null if there are no objects in snap proximity
    /// </summary>
    private WeArtTouchableObject _snapGraspClosestTouchable;

    private WeArtEasyGraspPose _snapGraspPose;

    private float _snapGraspingThreshold = 0.05f;

    // Read only lists of proximity colliders' touchable objects in contact 
    public IReadOnlyList<WeArtTouchableObject> ThumbGraspCheckTouchables => _thumbProximityTouchables as IReadOnlyList<WeArtTouchableObject>;
    public IReadOnlyList<WeArtTouchableObject> IndexGraspCheckTouchables => _indexProximityTouchables as IReadOnlyList<WeArtTouchableObject>;
    public IReadOnlyList<WeArtTouchableObject> MiddleGraspCheckTouchables => _middleProximityTouchables as IReadOnlyList<WeArtTouchableObject>;
    public IReadOnlyList<WeArtTouchableObject> AnnularGraspCheckTouchables => _annularProximityTouchables as IReadOnlyList<WeArtTouchableObject>;
    public IReadOnlyList<WeArtTouchableObject> PinkyGraspCheckTouchables => _pinkyProximityTouchables as IReadOnlyList<WeArtTouchableObject>;

    /// <summary>
    /// Get the grasping state of the hand controller.
    /// </summary>
    public GraspingState GraspingState
    {
        get => _graspingState;
        set => _graspingState = value;
    }


    /// <summary>
    /// Return the object that is currently grabbed 
    /// </summary>
    public WeArtTouchableObject GetGraspedObject()
    {
        return _graspedObject;
    }

    /// <summary>
    /// Set up
    /// </summary>
    private void Awake()
    {
        _handController = GetComponent<WeArtHandController>();
        _surfaceExploration = GetComponent<WeArtHandSurfaceExploration>();
        _deviceTrackingObject = GetComponent<WeArtDeviceTrackingObject>();

        _handSide = _handController._handSide;
        _thumbThimbleHaptic = _handController._thumbThimbleHaptic;
        _indexThimbleHaptic = _handController._indexThimbleHaptic;
        _middleThimbleHaptic = _handController._middleThimbleHaptic;
        _annularThimbleHaptic = _handController._annularThimbleHaptic;
        _pinkyThimbleHaptic = _handController._pinkyThimbleHaptic;
        _palmThimbleHaptic = _handController._palmThimbleHaptic;

        _thumbThimbleTracking = _handController._thumbThimbleTracking;
        _indexThimbleTracking = _handController._indexThimbleTracking;
        _middleThimbleTracking = _handController._middleThimbleTracking;
        _annularThimbleTracking = _handController._annularThimbleTracking;
        _pinkyThimbleTracking = _handController._pinkyThimbleTracking;

        WeArtHandController[] components = GameObject.FindObjectsOfType<WeArtHandController>();
        foreach (var component in components)
        {
            if (component._handSide != _handSide)
            {
                _otherHand = component;
            }
        }

        _otherGraspingSystem = _otherHand.gameObject.GetComponent<WeArtHandGraspingSystem>();
    }

    void Start()
    {
    }


    /// <summary>
    /// Run the grasping system's main update loop
    /// </summary>
    public void UpdateGraspingSystem()
    {

        if (WeArtEasyGraspManager.Instance != null)
        {
            if (_graspingState == GraspingState.Grabbed)
            {
                if (_graspedObject != null)
                {
                    if (_graspedObject.GraspingType == GraspingType.Snap)
                    {
                        HandleSnapGrasping();
                        //HandleFingerEffects();
                        return;
                    }
                }
            }
            else
            {
                CheckForSnapGraspObjects();

                if (_snapGraspClosestTouchable != null)
                {
                    HandleSnapGrasping();
                }
            }

            if (_graspedObject != null)
            {
                if (_graspedObject.GraspingType == GraspingType.Snap)
                {
                    return;
                }
            }
        }

        _lastThumbClosure = _thumbThimbleTracking.Closure.Value;
        _lastIndexClosure = _indexThimbleTracking.Closure.Value;
        _lastMiddleClosure = _middleThimbleTracking.Closure.Value;
        _lastAnnularClosure = _annularThimbleTracking.Closure.Value;
        _lastPinkyClosure = _pinkyThimbleTracking.Closure.Value;

        CheckAllGraspingEffects();

        CheckFingerBlocking();

        CheckGraspingConditions();

        HandleFingerEffects();
    }

    /// <summary>
    /// The OnEnable
    /// </summary>
    private void OnEnable()
    {
        // Fingers Disable
        if (WeArtController.Instance._deviceGeneration != DeviceGeneration.TD_Pro)
        {
            _annularThimbleHaptic.gameObject.SetActive(false);
            _pinkyThimbleHaptic.gameObject.SetActive(false);
            _annularProximityColider.gameObject.SetActive(false);
            _pinkyProximityColider.gameObject.SetActive(false);
        }

        // Fingers collider event assignments
        _thumbThimbleHaptic.TriggerEnter -= ThumbTriggerEnterHandle;
        _thumbThimbleHaptic.TriggerStay -= ThumbTriggerStayHandle;
        _thumbThimbleHaptic.TriggerExit -= ThumbTriggerExitHandle;
        _thumbThimbleHaptic.TriggerEnter += ThumbTriggerEnterHandle;
        _thumbThimbleHaptic.TriggerStay += ThumbTriggerStayHandle;
        _thumbThimbleHaptic.TriggerExit += ThumbTriggerExitHandle;

        _indexThimbleHaptic.TriggerEnter -= IndexTriggerEnterHandle;
        _indexThimbleHaptic.TriggerStay -= IndexTriggerStayHandle;
        _indexThimbleHaptic.TriggerExit -= IndexTriggerExitHandle;
        _indexThimbleHaptic.TriggerEnter += IndexTriggerEnterHandle;
        _indexThimbleHaptic.TriggerStay += IndexTriggerStayHandle;
        _indexThimbleHaptic.TriggerExit += IndexTriggerExitHandle;

        _middleThimbleHaptic.TriggerEnter -= MiddleTriggerEnterHandle;
        _middleThimbleHaptic.TriggerStay -= MiddleTriggerStayHandle;
        _middleThimbleHaptic.TriggerExit -= MiddleTriggerExitHandle;
        _middleThimbleHaptic.TriggerEnter += MiddleTriggerEnterHandle;
        _middleThimbleHaptic.TriggerStay += MiddleTriggerStayHandle;
        _middleThimbleHaptic.TriggerExit += MiddleTriggerExitHandle;

        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
        {
            _annularThimbleHaptic.TriggerEnter -= AnnularTriggerEnterHandle;
            _annularThimbleHaptic.TriggerStay -= AnnularTriggerStayHandle;
            _annularThimbleHaptic.TriggerExit -= AnnularTriggerExitHandle;
            _annularThimbleHaptic.TriggerEnter += AnnularTriggerEnterHandle;
            _annularThimbleHaptic.TriggerStay += AnnularTriggerStayHandle;
            _annularThimbleHaptic.TriggerExit += AnnularTriggerExitHandle;

            _pinkyThimbleHaptic.TriggerEnter -= PinkyTriggerEnterHandle;
            _pinkyThimbleHaptic.TriggerStay -= PinkyTriggerStayHandle;
            _pinkyThimbleHaptic.TriggerExit -= PinkyTriggerExitHandle;
            _pinkyThimbleHaptic.TriggerEnter += PinkyTriggerEnterHandle;
            _pinkyThimbleHaptic.TriggerStay += PinkyTriggerStayHandle;
            _pinkyThimbleHaptic.TriggerExit += PinkyTriggerExitHandle;

            _palmThimbleHaptic.TriggerStay -= PalmTriggerStayHandle;
            _palmThimbleHaptic.TriggerStay += PalmTriggerStayHandle;
        }
            _palmThimbleHaptic.TriggerEnter -= PalmTriggerEnterHandle;
            _palmThimbleHaptic.TriggerExit -= PalmTriggerExitHandle;
            _palmThimbleHaptic.TriggerEnter += PalmTriggerEnterHandle;
            _palmThimbleHaptic.TriggerExit += PalmTriggerExitHandle;

        // Fingers proximity colliders events assignments ****

        _thumbProximityColider.TriggerEnter -= ThumbGraspCheckEnterHandle;
        _thumbProximityColider.TriggerStay -= ThumbGraspCheckStayHandle;
        _thumbProximityColider.TriggerExit -= ThumbGraspCheckExitHandle;
        _thumbProximityColider.TriggerEnter += ThumbGraspCheckEnterHandle;
        _thumbProximityColider.TriggerStay += ThumbGraspCheckStayHandle;
        _thumbProximityColider.TriggerExit += ThumbGraspCheckExitHandle;

        _indexProximityColider.TriggerEnter -= IndexGraspCheckEnterHandle;
        _indexProximityColider.TriggerStay -= IndexGraspCheckStayHandle;
        _indexProximityColider.TriggerExit -= IndexGraspCheckExitHandle;
        _indexProximityColider.TriggerEnter += IndexGraspCheckEnterHandle;
        _indexProximityColider.TriggerStay += IndexGraspCheckStayHandle;
        _indexProximityColider.TriggerExit += IndexGraspCheckExitHandle;

        _middleProximityColider.TriggerEnter -= MiddleGraspCheckEnterHandle;
        _middleProximityColider.TriggerStay -= MiddleGraspCheckStayHandle;
        _middleProximityColider.TriggerExit -= MiddleGraspCheckExitHandle;
        _middleProximityColider.TriggerEnter += MiddleGraspCheckEnterHandle;
        _middleProximityColider.TriggerStay += MiddleGraspCheckStayHandle;
        _middleProximityColider.TriggerExit += MiddleGraspCheckExitHandle;

        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
        {
            _annularProximityColider.TriggerEnter -= AnnularGraspCheckEnterHandle;
            _annularProximityColider.TriggerStay -= AnnularGraspCheckStayHandle;
            _annularProximityColider.TriggerExit -= AnnularGraspCheckExitHandle;
            _annularProximityColider.TriggerEnter += AnnularGraspCheckEnterHandle;
            _annularProximityColider.TriggerStay += AnnularGraspCheckStayHandle;
            _annularProximityColider.TriggerExit += AnnularGraspCheckExitHandle;

            _pinkyProximityColider.TriggerEnter -= PinkyGraspCheckEnterHandle;
            _pinkyProximityColider.TriggerStay -= PinkyGraspCheckStayHandle;
            _pinkyProximityColider.TriggerExit -= PinkyGraspCheckExitHandle;
            _pinkyProximityColider.TriggerEnter += PinkyGraspCheckEnterHandle;
            _pinkyProximityColider.TriggerStay += PinkyGraspCheckStayHandle;
            _pinkyProximityColider.TriggerExit += PinkyGraspCheckExitHandle;
        }
    }

    /// <summary>
    /// Add or remove rigidbodies for fingers in the situation of grasping an anchored object
    /// </summary>
    /// <param name="value"></param>
    public void AddOrRemoveRigidbodiesFromFinggers(bool value, Collider collider)
    {
        if (value)
        {
            if (_thumbThimbleHaptic.gameObject.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = _thumbThimbleHaptic.gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            if (_indexThimbleHaptic.gameObject.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = _indexThimbleHaptic.gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            if (_middleThimbleHaptic.gameObject.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = _middleThimbleHaptic.gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            // Proximity
            if (_thumbProximityColider.gameObject.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = _thumbProximityColider.gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            if (_indexProximityColider.gameObject.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = _indexProximityColider.gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            if (_middleProximityColider.gameObject.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = _middleProximityColider.gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
            {
                if (_annularThimbleHaptic.gameObject.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rb = _annularThimbleHaptic.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }

                if (_pinkyThimbleHaptic.gameObject.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rb = _pinkyThimbleHaptic.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }

                if (_palmThimbleHaptic.gameObject.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rb = _palmThimbleHaptic.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }

                //Proximity
                if (_annularProximityColider.gameObject.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rb = _annularProximityColider.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }

                if (_pinkyProximityColider.gameObject.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rb = _pinkyProximityColider.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }
            }

        }
        else
        {
            if (_thumbThimbleHaptic.gameObject.GetComponent<Rigidbody>() != null)
                Destroy(_thumbThimbleHaptic.gameObject.GetComponent<Rigidbody>());

            if (_indexThimbleHaptic.gameObject.GetComponent<Rigidbody>() != null)
                Destroy(_indexThimbleHaptic.gameObject.GetComponent<Rigidbody>());

            if (_middleThimbleHaptic.gameObject.GetComponent<Rigidbody>() != null)
                Destroy(_middleThimbleHaptic.gameObject.GetComponent<Rigidbody>());

            //Proximity
            if (_thumbProximityColider.gameObject.GetComponent<Rigidbody>() != null)
                Destroy(_thumbProximityColider.gameObject.GetComponent<Rigidbody>());

            if (_indexProximityColider.gameObject.GetComponent<Rigidbody>() != null)
                Destroy(_indexProximityColider.gameObject.GetComponent<Rigidbody>());

            if (_middleProximityColider.gameObject.GetComponent<Rigidbody>() != null)
                Destroy(_middleProximityColider.gameObject.GetComponent<Rigidbody>());

            ThumbTriggerExitHandle(collider);
            IndexTriggerExitHandle(collider);
            MiddleTriggerExitHandle(collider);

            if (_thumbProximityTouchables.Contains(_graspedObject))
                _thumbProximityTouchables.Remove(_graspedObject);

            if (_indexProximityTouchables.Contains(_graspedObject))
                _indexProximityTouchables.Remove(_graspedObject);

            if (_middleProximityTouchables.Contains(_graspedObject))
                _middleProximityTouchables.Remove(_graspedObject);

            // Blocked
            if (_thumbBlockingTouchables.Contains(_graspedObject))
                _thumbBlockingTouchables.Remove(_graspedObject);

            if (_indexBlockingTouchables.Contains(_graspedObject))
                _indexBlockingTouchables.Remove(_graspedObject);

            if (_middleBlockingTouchables.Contains(_graspedObject))
                _middleBlockingTouchables.Remove(_graspedObject);


            if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
            {
                if (_annularProximityColider.gameObject.GetComponent<Rigidbody>() != null)
                    Destroy(_annularProximityColider.gameObject.GetComponent<Rigidbody>());

                if (_pinkyProximityColider.gameObject.GetComponent<Rigidbody>() != null)
                    Destroy(_pinkyProximityColider.gameObject.GetComponent<Rigidbody>());

                AnnularTriggerExitHandle(collider);
                PinkyTriggerExitHandle(collider);
                PalmTriggerExitHandle(collider);

                //Proximity
                if (_annularProximityTouchables.Contains(_graspedObject))
                    _annularProximityTouchables.Remove(_graspedObject);

                if (_pinkyProximityTouchables.Contains(_graspedObject))
                    _pinkyProximityTouchables.Remove(_graspedObject);

                if (_annularThimbleHaptic.gameObject.GetComponent<Rigidbody>() != null)
                    Destroy(_annularThimbleHaptic.gameObject.GetComponent<Rigidbody>());

                if (_pinkyThimbleHaptic.gameObject.GetComponent<Rigidbody>() != null)
                    Destroy(_pinkyThimbleHaptic.gameObject.GetComponent<Rigidbody>());

                if (_annularBlockingTouchables.Contains(_graspedObject))
                    _annularBlockingTouchables.Remove(_graspedObject);

                if (_pinkyBlockingTouchables.Contains(_graspedObject))
                    _pinkyBlockingTouchables.Remove(_graspedObject);
            }
        }
    }



    /// <summary>
    /// Handle interaction with a kinematic objects and check for trigger colliders
    /// </summary>
    private void HandleFingerEffects()
    {
        // Kinematic object check
        if (_graspingState == GraspingState.Grabbed)
        {
            if (_thumbThimbleTracking.IsGrasping != _thumbThimbleHaptic.IsGrasping)
            {
                _thumbThimbleHaptic.IsGrasping = _thumbThimbleTracking.IsGrasping;
                _thumbThimbleHaptic.GraspedObject = _thumbThimbleTracking.GraspedObject;
            }

            if (_indexThimbleTracking.IsGrasping != _indexThimbleHaptic.IsGrasping)
            {
                _indexThimbleHaptic.IsGrasping = _indexThimbleTracking.IsGrasping;
                _indexThimbleHaptic.GraspedObject = _indexThimbleTracking.GraspedObject;
            }
            if (_middleThimbleTracking.IsGrasping != _middleThimbleHaptic.IsGrasping)
                _middleThimbleHaptic.IsGrasping = _middleThimbleTracking.IsGrasping;

            if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
            {
                if (_annularThimbleTracking.IsGrasping != _annularThimbleHaptic.IsGrasping)
                {
                    _annularThimbleHaptic.IsGrasping = _annularThimbleTracking.IsGrasping;
                    _annularThimbleHaptic.GraspedObject = _annularThimbleTracking.GraspedObject;
                }
                if (_pinkyThimbleTracking.IsGrasping != _pinkyThimbleHaptic.IsGrasping)
                {
                    _pinkyThimbleHaptic.IsGrasping = _pinkyThimbleTracking.IsGrasping;
                    _pinkyThimbleHaptic.GraspedObject = _pinkyThimbleTracking.GraspedObject;
                }
                if (_palmThimbleHaptic.GraspedObject == null)
                {
                    foreach (var item in _palmThimbleHaptic.TouchedObjects)
                    {
                        if(item == _graspedObject)
                        {
                            _palmThimbleHaptic.GraspedObject = item;
                        }
                    }
                }
            }
        }
        else
        {
            if (_thumbThimbleHaptic.IsGrasping)
                _thumbThimbleHaptic.IsGrasping = false;

            if (_indexThimbleHaptic.IsGrasping)
                _indexThimbleHaptic.IsGrasping = false;

            if (_middleThimbleHaptic.IsGrasping)
                _middleThimbleHaptic.IsGrasping = false;

            if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
            {
                if (_annularThimbleHaptic.IsGrasping)
                    _annularThimbleHaptic.IsGrasping = false;

                if (_pinkyThimbleHaptic.IsGrasping)
                    _pinkyThimbleHaptic.IsGrasping = false;

                if(_palmThimbleHaptic.IsGrasping)
                    _palmThimbleHaptic.IsGrasping= false;
            }
        }
        
        _thumbThimbleHaptic.UpdateIfPositionedInTrigger();
        _indexThimbleHaptic.UpdateIfPositionedInTrigger();
        _middleThimbleHaptic.UpdateIfPositionedInTrigger();
        if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
        {
            _annularThimbleHaptic.UpdateIfPositionedInTrigger();
            _pinkyThimbleHaptic.UpdateIfPositionedInTrigger();
            _palmThimbleHaptic.UpdateIfPositionedInTrigger();
        }

    }


    /// <summary>
    /// Check if the grasping effects are applied, especially for mesh colliders
    /// </summary>
    private void CheckAllGraspingEffects()
    {
        if (_graspingState == GraspingState.Grabbed)
        {
            if (_thumbThimbleHaptic.IsGrasping)
            {
                if (_thumbThimbleHaptic.GraspedObject == null)
                    _thumbThimbleHaptic.GraspedObject = _graspedObject;

                List<Collider> colliders = new List<Collider>();


                bool foundCollision = false;
                foreach (var item in _graspedObject.GetTouchableColliders())
                {
                    if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _thumbThimbleHaptic.GetComponent<Collider>(), _thumbThimbleHaptic.transform.position, _thumbThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                    {
                        foundCollision = true;
                    }
                }

                if (foundCollision == false)
                {
                    ThumbTriggerExitHandle(_graspedObject.GetFirstCollider());
                    _thumbThimbleTracking.IsGrasping = false;
                    _thumbThimbleTracking.GraspedObject = null;
                    _thumbThimbleTracking.IsBlocked = false;
                }
            }
            else
            {
                {
                    bool foundCollision = false;
                    foreach (var item in _graspedObject.GetTouchableColliders())
                    {
                        if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _thumbThimbleHaptic.GetComponent<Collider>(), _thumbThimbleHaptic.transform.position, _thumbThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                        {
                            foundCollision = true;
                        }
                    }

                    if (foundCollision)
                    {
                        ThumbTriggerEnterHandle(_graspedObject.GetFirstCollider());
                    }
                }
            }

            if (_indexThimbleHaptic.IsGrasping)
            {
                if (_indexThimbleHaptic.GraspedObject == null)
                    _indexThimbleHaptic.GraspedObject = _graspedObject;

                bool foundCollision = false;
                foreach (var item in _graspedObject.GetTouchableColliders())
                {
                    if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _indexThimbleHaptic.GetComponent<Collider>(), _indexThimbleHaptic.transform.position, _indexThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                    {
                        foundCollision = true;
                    }
                }

                if (foundCollision == false)
                {
                    IndexTriggerExitHandle(_graspedObject.GetFirstCollider());
                    _indexThimbleTracking.IsGrasping = false;
                    _indexThimbleTracking.GraspedObject = null;
                    _indexThimbleTracking.IsBlocked = false;
                }
            }
            else
            {
                {
                    bool foundCollision = false;
                    foreach (var item in _graspedObject.GetTouchableColliders())
                    {
                        if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _indexThimbleHaptic.GetComponent<Collider>(), _indexThimbleHaptic.transform.position, _indexThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                        {
                            foundCollision = true;
                        }
                    }

                    if (foundCollision)
                    {
                        IndexTriggerEnterHandle(_graspedObject.GetFirstCollider());
                    }
                }
            }

            if (_middleThimbleHaptic.IsGrasping)
            {

                if (_middleThimbleHaptic.GraspedObject == null)
                    _middleThimbleHaptic.GraspedObject = _graspedObject;

                bool foundCollision = false;
                foreach (var item in _graspedObject.GetTouchableColliders())
                {
                    if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _middleThimbleHaptic.GetComponent<Collider>(), _middleThimbleHaptic.transform.position, _middleThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                    {
                        foundCollision = true;
                    }
                }

                if (foundCollision == false)
                {
                    MiddleTriggerExitHandle(_graspedObject.GetFirstCollider());
                    _middleThimbleTracking.IsGrasping = false;
                    _middleThimbleTracking.GraspedObject = null;
                    _middleThimbleTracking.IsBlocked = false;
                }
            }
            else
            {
                {
                    bool foundCollision = false;
                    foreach (var item in _graspedObject.GetTouchableColliders())
                    {
                        if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _middleThimbleHaptic.GetComponent<Collider>(), _middleThimbleHaptic.transform.position, _middleThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                        {
                            foundCollision = true;
                        }
                    }

                    if (foundCollision)
                    {
                        MiddleTriggerEnterHandle(_graspedObject.GetFirstCollider());
                    }
                }
            }

            if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
            {

                if (_annularThimbleHaptic.IsGrasping)
                {

                    if (_annularThimbleHaptic.GraspedObject == null)
                        _annularThimbleHaptic.GraspedObject = _graspedObject;

                    bool foundCollision = false;
                    foreach (var item in _graspedObject.GetTouchableColliders())
                    {
                        if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _annularThimbleHaptic.GetComponent<Collider>(), _annularThimbleHaptic.transform.position, _annularThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                        {
                            foundCollision = true;
                        }
                    }

                    if (foundCollision == false)
                    {
                        AnnularTriggerExitHandle(_graspedObject.GetFirstCollider());
                        _annularThimbleTracking.IsGrasping = false;
                        _annularThimbleTracking.GraspedObject = null;
                        _annularThimbleTracking.IsBlocked = false;
                    }
                }
                else
                {
                    {
                        bool foundCollision = false;
                        foreach (var item in _graspedObject.GetTouchableColliders())
                        {
                            if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _annularThimbleHaptic.GetComponent<Collider>(), _annularThimbleHaptic.transform.position, _annularThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                            {
                                foundCollision = true;
                            }
                        }

                        if (foundCollision)
                        {
                            AnnularTriggerEnterHandle(_graspedObject.GetFirstCollider());
                        }
                    }
                }


                if (_pinkyThimbleHaptic.IsGrasping)
                {

                    if (_pinkyThimbleHaptic.GraspedObject == null)
                        _pinkyThimbleHaptic.GraspedObject = _graspedObject;

                    bool foundCollision = false;
                    foreach (var item in _graspedObject.GetTouchableColliders())
                    {
                        if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _pinkyThimbleHaptic.GetComponent<Collider>(), _pinkyThimbleHaptic.transform.position, _pinkyThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                        {
                            foundCollision = true;
                        }
                    }

                    if (foundCollision == false)
                    {
                        PinkyTriggerExitHandle(_graspedObject.GetFirstCollider());
                        _pinkyThimbleTracking.IsGrasping = false;
                        _pinkyThimbleTracking.GraspedObject = null;
                        _pinkyThimbleTracking.IsBlocked = false;
                    }
                }
                else
                {
                    {
                        bool foundCollision = false;
                        foreach (var item in _graspedObject.GetTouchableColliders())
                        {
                            if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _pinkyThimbleHaptic.GetComponent<Collider>(), _pinkyThimbleHaptic.transform.position, _pinkyThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                            {
                                foundCollision = true;
                            }
                        }

                        if (foundCollision)
                        {
                            PinkyTriggerEnterHandle(_graspedObject.GetFirstCollider());
                        }
                    }
                }

                if (_palmThimbleHaptic.IsGrasping)
                {

                    if (_palmThimbleHaptic.GraspedObject == null)
                        _palmThimbleHaptic.GraspedObject = _graspedObject;

                    bool foundCollision = false;
                    foreach (var item in _graspedObject.GetTouchableColliders())
                    {
                        if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _palmThimbleHaptic.GetComponent<Collider>(), _palmThimbleHaptic.transform.position, _palmThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                        {
                            foundCollision = true;
                        }
                    }

                    if (foundCollision == false)
                    {
                        PalmTriggerExitHandle(_graspedObject.GetFirstCollider());
                    }
                }
                else
                {
                    {
                        bool foundCollision = false;
                        foreach (var item in _graspedObject.GetTouchableColliders())
                        {
                            if (Physics.ComputePenetration(item, item.transform.position, item.transform.rotation, _palmThimbleHaptic.GetComponent<Collider>(), _palmThimbleHaptic.transform.position, _palmThimbleHaptic.transform.rotation, out Vector3 dir, out float dis))
                            {
                                foundCollision = true;
                            }
                        }

                        if (foundCollision)
                        {
                            PalmTriggerEnterHandle(_graspedObject.GetFirstCollider());
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if a finger should be blocked when touching an object
    /// </summary>
    /// <param name="thimbleTracking"></param>
    /// <param name="contactTouchables"></param>
    /// <param name="graspCheckTouchables"></param>
    /// <param name="blockingTouchables"></param>
    /// <param name="fingerNumber"></param>
    private void CheckIndividualFingerBlocking(WeArtThimbleTrackingObject thimbleTracking,WeArtHapticObject hapticObject , List<WeArtTouchableObject> contactTouchables, List<WeArtTouchableObject> graspCheckTouchables, List<WeArtTouchableObject> blockingTouchables, int fingerNumber)
    {
        bool hasToUnblock = true;

        if (thimbleTracking.IsBlocked)
        {
            if (thimbleTracking.IsGrasping == false)
            {
                //Colapse the next "if" for better overview
                if (_graspingState == GraspingState.Released)
                {
                    foreach (WeArtTouchableObject contactItem in contactTouchables)
                    {
                        bool isContactItemBlocking = false;
                        foreach (WeArtTouchableObject graspCheckItem in graspCheckTouchables)
                        {
                            if (contactItem == graspCheckItem)
                            {
                                hasToUnblock = false;
                                isContactItemBlocking = true;
                            }
                        }

                        if (!isContactItemBlocking)
                        {
                            if (blockingTouchables.Contains(contactItem))
                            {
                                blockingTouchables.Remove(contactItem);
                            }
                        }
                    }

                    if (hasToUnblock)
                    {
                        thimbleTracking.IsBlocked = false;
                        thimbleTracking.SafeUnblockSeconds = _handController.GetSafeUnlockFrames();

                    }
                    else
                    {
                        if (thimbleTracking.Closure.Value < thimbleTracking.BlockedClosureValue)
                        {
                            thimbleTracking.IsBlocked = false;
                            thimbleTracking.IsGrasping = false;
                        }
                    }
                }
                else
                {
                    if (thimbleTracking.Closure.Value > thimbleTracking.BlockedClosureValue)
                    {
                        thimbleTracking.IsBlocked = false;
                    }
                }
            }
            else
            {
                if (thimbleTracking.Closure.Value < thimbleTracking.BlockedClosureValue)
                {
                    thimbleTracking.IsBlocked = false;
                    thimbleTracking.IsGrasping = false;
                }
            }
        }
        else
        {
            foreach (WeArtTouchableObject contactItem in contactTouchables)
            {
                foreach (WeArtTouchableObject graspCheckItem in graspCheckTouchables)
                {
                    if (contactItem == graspCheckItem)
                    {
                        thimbleTracking.IsBlocked = true;

                        if (_graspingState == GraspingState.Grabbed)
                        {
                            if(contactItem == _graspedObject)
                                thimbleTracking.IsGrasping = true;
                        }
                            

                        thimbleTracking.BlockedClosureValue = _handController.FingersMixers[fingerNumber].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
                        if (!blockingTouchables.Contains(contactItem))
                            blockingTouchables.Add(contactItem);
                    }
                }
            }
        }

        if (_graspingState == GraspingState.Grabbed)
        {
           if(!hapticObject.TouchedObjects.Contains(_graspedObject) && hapticObject.GraspedObject == _graspedObject)
            {
                thimbleTracking.IsBlocked = false;
                thimbleTracking.IsGrasping = false;
            }
        }
    }

    /// <summary>
    /// Disables all touchables in contact with the haptic objects
    /// </summary>
    public void DisableAllTouchablesInContact()
    {
        DisableTouchables(_thumbContactTouchables);
        DisableTouchables(_indexContactTouchables);
        DisableTouchables(_middleContactTouchables);
        if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
        {
            DisableTouchables(_annularContactTouchables);
            DisableTouchables(_pinkyContactTouchables);
            DisableTouchables(_palmContactTouchables);
        }
    }

    /// <summary>
    /// Disables a list of touchable objects
    /// </summary>
    /// <param name="touchables"></param>
    private void DisableTouchables(List<WeArtTouchableObject> touchables)
    {
        foreach (var touchable in touchables)
        {
            if(touchable != null)
            touchable.gameObject.SetActive(false);
        }
    }

    private void HandleSnapGrasping()
    {
        if (_notAllowedToGrabSeconds > 0)
        {
            _notAllowedToGrabSeconds -= Time.deltaTime;
            return;
        }

        if (_graspingState == GraspingState.Released)
        {
            if (IsTryingToSnapGrasp())
            {
                WeArtInterpolateFingers interpolateFingers = gameObject.AddComponent<WeArtInterpolateFingers>();
                interpolateFingers.SetTargetPose(_snapGraspPose.GetData());

                //Instant pose
                //_handController.SetFingerAnimation(poseFound.GetData());

                GraspTouchableObject(_snapGraspClosestTouchable);

                if (_snapGraspClosestTouchable.AnchoredObject == null)
                {
                    if (_snapGraspPose.GetIsInterpolating())
                    {
                        WeArtInterpolateToPosition weArtInterpolateToPosition = _snapGraspClosestTouchable.gameObject.AddComponent<WeArtInterpolateToPosition>();
                        if (weArtInterpolateToPosition != null)
                        {
                            weArtInterpolateToPosition.SetPositionAndRotation(_snapGraspPose.GetData().touchablePosition, _snapGraspPose.GetData().touchableRotation);
                        }
                    }
                    else
                    {
                        _snapGraspClosestTouchable.transform.localPosition = _snapGraspPose.GetData().touchablePosition;
                        _snapGraspClosestTouchable.transform.localRotation = _snapGraspPose.GetData().touchableRotation;
                    }
                }

                if (_snapGraspClosestTouchable.AnchoredObject != null)
                {

                    if (_snapGraspPose.GetIsInterpolating())
                    {
                        WeArtInterpolateToPosition weArtInterpolateToPosition = gameObject.AddComponent<WeArtInterpolateToPosition>();
                        if (weArtInterpolateToPosition != null)
                        {
                            weArtInterpolateToPosition.SetPositionAndRotation(_snapGraspPose.GetData().handPosition, _snapGraspPose.GetData().handRotation);
                        }
                    }
                    else
                    {
                        transform.localPosition = _snapGraspPose.GetData().handPosition;
                        transform.localRotation = _snapGraspPose.GetData().handRotation;
                    }
                }
            }
        }
        else
        {
            if (IsTryingToSnapRelease())
            {
                MakeTheHandUnableToGrab();
                ReleaseTouchableObject();
            }
        }
    }

    private bool IsTryingToSnapGrasp()
    {
        if (_thumbThimbleTracking.Closure.Value > GetSnapFingerThreshold(_thumbThimbleTracking) && ( _indexThimbleTracking.Closure.Value > GetSnapFingerThreshold(_indexThimbleTracking) || _middleThimbleTracking.Closure.Value > GetSnapFingerThreshold(_middleThimbleTracking) ||
            _annularThimbleTracking.Closure.Value > GetSnapFingerThreshold(_annularThimbleTracking) || _pinkyThimbleTracking.Closure.Value > GetSnapFingerThreshold(_pinkyThimbleTracking)))
        {
            if (_thumbThimbleTracking.Closure.Value - _lastThumbClosure > _snapGraspingThreshold ||
                _indexThimbleTracking.Closure.Value - _lastIndexClosure > _snapGraspingThreshold ||
                _middleThimbleTracking.Closure.Value - _lastMiddleClosure > _snapGraspingThreshold ||
                _annularThimbleTracking.Closure.Value - _lastAnnularClosure > _snapGraspingThreshold ||
                _pinkyThimbleTracking.Closure.Value - _lastPinkyClosure > _snapGraspingThreshold)
            {
                return true;
            }
        }
        return false;
    }

    private float GetSnapFingerThreshold(WeArtThimbleTrackingObject thimbleTraking)
    {
        if(thimbleTraking == null) 
            return 1f;

        float snapGraspSensivity = 0.5f;

        if (thimbleTraking == _thumbThimbleTracking)
        {
            return Mathf.Clamp(_snapGraspPose.GetData().thumbClosure * snapGraspSensivity,0.1f,1.0f);
        }

        if (thimbleTraking == _indexThimbleTracking)
        {
            return Mathf.Clamp(_snapGraspPose.GetData().indexClosure * snapGraspSensivity, 0.1f, 1.0f);
        }

        if (thimbleTraking == _middleThimbleTracking)
        {
            return Mathf.Clamp(_snapGraspPose.GetData().middleClosure * snapGraspSensivity, 0.1f, 1.0f);
        }

        if (thimbleTraking == _annularThimbleTracking)
        {
            return Mathf.Clamp(_snapGraspPose.GetData().annularClosure * snapGraspSensivity, 0.1f, 1.0f);
        }

        if (thimbleTraking == _pinkyThimbleTracking)
        {
            return Mathf.Clamp(_snapGraspPose.GetData().pinkyClosure * snapGraspSensivity, 0.1f, 1.0f);
        }

        return 1;
    }

    private bool IsTryingToSnapRelease()
    {
        if (_thumbThimbleTracking.Closure.Value < GetSnapFingerThreshold(_thumbThimbleTracking) || (_indexThimbleTracking.Closure.Value < GetSnapFingerThreshold(_indexThimbleTracking) && _middleThimbleTracking.Closure.Value < GetSnapFingerThreshold(_middleThimbleTracking) &&
            _annularThimbleTracking.Closure.Value < GetSnapFingerThreshold(_annularThimbleTracking) && _pinkyThimbleTracking.Closure.Value < GetSnapFingerThreshold(_pinkyThimbleTracking)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check the three grasping conditions
    /// </summary>
    private void CheckGraspingConditions()
    {
        if (_notAllowedToGrabSeconds > 0)
        {
            _notAllowedToGrabSeconds -= Time.deltaTime;
            return;
        }

        bool condA = false;
        bool condB = false;
        bool condC = false;

        if (_graspingState != GraspingState.Grabbed)
        {
            condA = ConditionA();

            if (condA)
            {
                condB = ConditionB();
            }
            if (condB)
            {
                condC = ConditionC();
            }
        }
        else
        {
            condA = true;
            condB = true;
            condC = true;
            CheckReleaseConditions();
        }
    }

    /// <summary>
    /// Condition is true if the Thumb or Palm are in contact with a touchable object and Index or Middle are also in contact with the same object
    /// </summary>
    /// <returns></returns>
    private bool ConditionA()
    {
        if (_thumbContactTouchables.Count > 0)
        {
            if (_indexContactTouchables.Count > 0)
            {
                return true;
            }

            if (_middleContactTouchables.Count > 0)
            {
                return true;
            }

            if (_palmContactTouchables.Count > 0)
            {
                return true;
            }

            if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
            {
                if (_annularContactTouchables.Count > 0)
                {
                    return true;
                }

                if (_pinkyContactTouchables.Count > 0)
                {
                    return true;
                }
            }
        }

        if (_palmContactTouchables.Count > 0)
        {
            if (_indexContactTouchables.Count > 0)
            {
                return true;
            }

            if (_middleContactTouchables.Count > 0)
            {
                return true;
            }

            if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
            {
                if (_annularContactTouchables.Count > 0)
                {
                    return true;
                }

                if (_pinkyContactTouchables.Count > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Condition is true if the user applies force on the object, making sure that the user truly wants to grab the object 
    /// </summary>
    /// <returns></returns>
    private bool ConditionB()
    {
        _isThumbTryingToGrasp = false;
        _isIndexTryingToGrasp = false;
        _isMiddleTryingToGrasp = false;
        _isAnnularTryingToGrasp = false;
        _isPinkyTryingToGrasp = false;

        bool anyFingerTryingToGrasp = false;

        if (_thumbThimbleTracking.IsBlocked)
        {
            if (_thumbThimbleTracking.Closure.Value > _handController.FingersMixers[0].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) + _intentionalGraspThreshold
                && _handController.FingersMixers[0].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > _tryingToGraspThreshhold)
            {
                _isThumbTryingToGrasp = true;
                anyFingerTryingToGrasp = true;
            }
        }

        if (_indexThimbleTracking.IsBlocked)
        {
            if (_indexThimbleTracking.Closure.Value > _handController.FingersMixers[1].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) + _intentionalGraspThreshold
                && _handController.FingersMixers[1].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > _tryingToGraspThreshhold)
            {
                _isIndexTryingToGrasp = true;
                anyFingerTryingToGrasp = true;
            }
        }

        if (_middleThimbleTracking.IsBlocked)
        {
            if (_middleThimbleTracking.Closure.Value > _handController.FingersMixers[2].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) + _intentionalGraspThreshold
                && _handController.FingersMixers[2].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > _tryingToGraspThreshhold)
            {
                _isMiddleTryingToGrasp = true;
                anyFingerTryingToGrasp = true;
            }
        }

        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
        {
            _isAnnularTryingToGrasp = false;
            _isPinkyTryingToGrasp = false;

            if (_annularThimbleTracking.IsBlocked)
            {
                if (_annularThimbleTracking.Closure.Value > _handController.FingersMixers[3].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) + _intentionalGraspThreshold
                    && _handController.FingersMixers[3].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > _tryingToGraspThreshhold)
                {
                    _isAnnularTryingToGrasp = true;
                    anyFingerTryingToGrasp = true;
                }
            }

            if (_pinkyThimbleTracking.IsBlocked)
            {
                if (_pinkyThimbleTracking.Closure.Value > _handController.FingersMixers[4].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) + _intentionalGraspThreshold
                    && _handController.FingersMixers[4].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > _tryingToGraspThreshhold)
                {
                    _isPinkyTryingToGrasp = true;
                    anyFingerTryingToGrasp = true;
                }
            }
        }

        return anyFingerTryingToGrasp;
    }

    /// <summary>
    ///  Condition is true if the Thumb or Palm are in contact with a touchable object and Index or Middle are also in contact with the same object and fingers are also blocked on the object and the user applies pressure on the object
    /// </summary>
    /// <returns></returns>
    private bool ConditionC()
    {
        if (_isThumbTryingToGrasp)
        {
            if (_indexThimbleTracking.IsBlocked)
            {
                foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                {
                    if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                        continue;



                    foreach (WeArtTouchableObject otherItem in _indexBlockingTouchables)
                    {
                        if (otherItem.GraspingType == GraspingType.Snap)
                            continue;

                        if (item == otherItem)
                        {
                            _thumbThimbleTracking.GraspedObject = item;
                            _thumbThimbleHaptic.GraspedObject = item;
                            _thumbThimbleTracking.IsGrasping = true;
                            _thumbThimbleHaptic.IsGrasping = true;

                            _indexThimbleTracking.GraspedObject = item;
                            _indexThimbleHaptic.GraspedObject = item;
                            _indexThimbleTracking.IsGrasping = true;
                            _indexThimbleHaptic.IsGrasping = true;

                            GraspTouchableObject(item);
                            return true;
                        }
                    }
                }
            }

            if (_middleThimbleTracking.IsBlocked)
            {
                foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                {
                    if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                        continue;

                    foreach (WeArtTouchableObject otherItem in _middleBlockingTouchables)
                    {
                        if (otherItem.GraspingType == GraspingType.Snap)
                            continue;

                        if (item == otherItem)
                        {
                            _thumbThimbleTracking.GraspedObject = item;
                            _thumbThimbleHaptic.GraspedObject = item;
                            _thumbThimbleTracking.IsGrasping = true;
                            _thumbThimbleHaptic.IsGrasping = true;

                            _middleThimbleTracking.GraspedObject = item;
                            _middleThimbleHaptic.GraspedObject = item;
                            _middleThimbleTracking.IsGrasping = true;
                            _middleThimbleHaptic.IsGrasping = true;

                            GraspTouchableObject(item);
                            return true;
                        }
                    }
                }
            }

            if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
            {
                if (_annularThimbleTracking.IsBlocked)
                {
                    foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                    {
                        if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                            continue;

                        foreach (WeArtTouchableObject otherItem in _annularBlockingTouchables)
                        {
                            if (otherItem.GraspingType == GraspingType.Snap)
                                continue;

                            if (item == otherItem)
                            {
                                _thumbThimbleTracking.GraspedObject = item;
                                _thumbThimbleHaptic.GraspedObject = item;
                                _thumbThimbleTracking.IsGrasping = true;
                                _thumbThimbleHaptic.IsGrasping = true;

                                _annularThimbleTracking.GraspedObject = item;
                                _annularThimbleHaptic.GraspedObject = item;
                                _annularThimbleTracking.IsGrasping = true;
                                _annularThimbleHaptic.IsGrasping = true;

                                GraspTouchableObject(item);
                                return true;
                            }
                        }
                    }
                }

                if (_pinkyThimbleTracking.IsBlocked)
                {
                    foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                    {
                        if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                            continue;

                        foreach (WeArtTouchableObject otherItem in _pinkyBlockingTouchables)
                        {
                            if (otherItem.GraspingType == GraspingType.Snap)
                                continue;

                            if (item == otherItem)
                            {
                                _thumbThimbleTracking.GraspedObject = item;
                                _thumbThimbleHaptic.GraspedObject = item;
                                _thumbThimbleTracking.IsGrasping = true;
                                _thumbThimbleHaptic.IsGrasping = true;

                                _pinkyThimbleTracking.GraspedObject = item;
                                _pinkyThimbleHaptic.GraspedObject = item;
                                _pinkyThimbleTracking.IsGrasping = true;
                                _pinkyThimbleHaptic.IsGrasping = true;

                                GraspTouchableObject(item);
                                return true;
                            }
                        }
                    }
                }
            }


            if  (_handController.FingersMixers[0].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) >= WeArtConstants.palmGraspClosureThreshold)
            {
                foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                {
                    if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                        continue;

                    foreach (WeArtTouchableObject otherItem in _palmContactTouchables)
                    {
                        if (otherItem.GraspingType == GraspingType.Snap)
                            continue;

                        if (item == otherItem)
                        {
                            _isGraspingWithPalm = true;
                            _palmThimbleHaptic.IsGrasping = true;
                            _palmThimbleHaptic.GraspedObject = item;

                            _thumbThimbleTracking.GraspedObject = item;
                            _thumbThimbleHaptic.GraspedObject = item;
                            _thumbThimbleTracking.IsGrasping = true;
                            _thumbThimbleHaptic.IsGrasping = true;

                            GraspTouchableObject(item);
                            return true;
                        }
                    }
                }
            }
        }

        if (_isIndexTryingToGrasp)
        {
            if (_thumbThimbleTracking.IsBlocked)
            {
                foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                {
                    if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                        continue;

                    foreach (WeArtTouchableObject otherItem in _indexBlockingTouchables)
                    {
                        if (otherItem.GraspingType == GraspingType.Snap)
                            continue;

                        if (item == otherItem)
                        {
                            _thumbThimbleTracking.GraspedObject = item;
                            _thumbThimbleHaptic.GraspedObject = item;
                            _thumbThimbleTracking.IsGrasping = true;
                            _thumbThimbleHaptic.IsGrasping = true;

                            _indexThimbleTracking.GraspedObject = item;
                            _indexThimbleHaptic.GraspedObject = item;
                            _indexThimbleTracking.IsGrasping = true;
                            _indexThimbleHaptic.IsGrasping = true;

                            GraspTouchableObject(item);
                            return true;
                        }
                    }
                }
            }

            if (_handController.FingersMixers[1].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) >= WeArtConstants.palmGraspClosureThreshold)
            {
                foreach (WeArtTouchableObject item in _indexBlockingTouchables)
                {
                    if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                        continue;

                    foreach (WeArtTouchableObject otherItem in _palmContactTouchables)
                    {
                        if (otherItem.GraspingType == GraspingType.Snap)
                            continue;

                        if (item == otherItem)
                        {
                            _isGraspingWithPalm = true;
                            _palmThimbleHaptic.IsGrasping = true;
                            _palmThimbleHaptic.GraspedObject = item;

                            _indexThimbleTracking.GraspedObject = item;
                            _indexThimbleHaptic.GraspedObject = item;
                            _indexThimbleTracking.IsGrasping = true;
                            _indexThimbleHaptic.IsGrasping = true;

                            GraspTouchableObject(item);
                            return true;
                        }
                    }
                }
            }
        }

        if (_isMiddleTryingToGrasp)
        {
            if (_thumbThimbleTracking.IsBlocked)
            {
                foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                {
                    if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                        continue;

                    foreach (WeArtTouchableObject otherItem in _middleBlockingTouchables)
                    {
                        if (otherItem.GraspingType == GraspingType.Snap)
                            continue;

                        if (item == otherItem)
                        {
                            _thumbThimbleTracking.GraspedObject = item;
                            _thumbThimbleHaptic.GraspedObject = item;
                            _thumbThimbleTracking.IsGrasping = true;
                            _thumbThimbleHaptic.IsGrasping = true;

                            _middleThimbleTracking.GraspedObject = item;
                            _middleThimbleHaptic.GraspedObject = item;
                            _middleThimbleTracking.IsGrasping = true;
                            _middleThimbleHaptic.IsGrasping = true;

                            GraspTouchableObject(item);
                            return true;
                        }
                    }
                }
            }

            if (_handController.FingersMixers[2].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) >= WeArtConstants.palmGraspClosureThreshold)
            {
                foreach (WeArtTouchableObject item in _middleBlockingTouchables)
                {
                    if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                        continue;

                    foreach (WeArtTouchableObject otherItem in _palmContactTouchables)
                    {
                        if (otherItem.GraspingType == GraspingType.Snap)
                            continue;

                        if (item == otherItem || item.GraspingType == GraspingType.Snap)
                        {
                            _isGraspingWithPalm = true;
                            _palmThimbleHaptic.IsGrasping = true;
                            _palmThimbleHaptic.GraspedObject = item;

                            _middleThimbleTracking.GraspedObject = item;
                            _middleThimbleHaptic.GraspedObject = item;
                            _middleThimbleTracking.IsGrasping = true;
                            _middleThimbleHaptic.IsGrasping = true;

                            GraspTouchableObject(item);
                            _palmThimbleHaptic.AddEffect(_graspedObject.GetEffect(_palmThimbleHaptic), _graspedObject);
                            _middleThimbleHaptic.AddEffect(_graspedObject.GetEffect(_middleThimbleHaptic), _graspedObject);
                            return true;
                        }
                    }
                }
            }
        }

        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
        {
            if (_isAnnularTryingToGrasp)
            {
                if (_thumbThimbleTracking.IsBlocked)
                {
                    foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                    {
                        if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                            continue;

                        foreach (WeArtTouchableObject otherItem in _annularBlockingTouchables)
                        {
                            if (otherItem.GraspingType == GraspingType.Snap)
                                continue;

                            if (item == otherItem)
                            {
                                _thumbThimbleTracking.GraspedObject = item;
                                _thumbThimbleHaptic.GraspedObject = item;
                                _thumbThimbleTracking.IsGrasping = true;
                                _thumbThimbleHaptic.IsGrasping = true;

                                _annularThimbleTracking.GraspedObject = item;
                                _annularThimbleHaptic.GraspedObject = item;
                                _annularThimbleTracking.IsGrasping = true;
                                _annularThimbleHaptic.IsGrasping = true;

                                GraspTouchableObject(item);
                                return true;
                            }
                        }
                    }
                }

                if (_handController.FingersMixers[3].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) >= WeArtConstants.palmGraspClosureThreshold)
                {
                    foreach (WeArtTouchableObject item in _annularBlockingTouchables)
                    {
                        if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                            continue;

                        foreach (WeArtTouchableObject otherItem in _palmContactTouchables)
                        {
                            if (otherItem.GraspingType == GraspingType.Snap)
                                continue;

                            if (item == otherItem)
                            {
                                _isGraspingWithPalm = true;
                                _palmThimbleHaptic.IsGrasping = true;
                                _palmThimbleHaptic.GraspedObject = item;


                                _annularThimbleTracking.GraspedObject = item;
                                _annularThimbleHaptic.GraspedObject = item;
                                _annularThimbleTracking.IsGrasping = true;
                                _annularThimbleHaptic.IsGrasping = true;

                                GraspTouchableObject(item);
                                return true;
                            }
                        }
                    }
                }
            }

            if (_isPinkyTryingToGrasp)
            {
                if (_thumbThimbleTracking.IsBlocked)
                {
                    foreach (WeArtTouchableObject item in _thumbBlockingTouchables)
                    {
                        if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                            continue;

                        foreach (WeArtTouchableObject otherItem in _pinkyBlockingTouchables)
                        {
                            if (otherItem.GraspingType == GraspingType.Snap)
                                continue;

                            if (item == otherItem)
                            {
                                _thumbThimbleTracking.GraspedObject = item;
                                _thumbThimbleHaptic.GraspedObject = item;
                                _thumbThimbleTracking.IsGrasping = true;
                                _thumbThimbleHaptic.IsGrasping = true;

                                _pinkyThimbleTracking.GraspedObject = item;
                                _pinkyThimbleHaptic.GraspedObject = item;
                                _pinkyThimbleTracking.IsGrasping = true;
                                _pinkyThimbleHaptic.IsGrasping = true;

                                GraspTouchableObject(item);
                                return true;
                            }
                        }
                    }
                }

                if (_handController.FingersMixers[4].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) >= WeArtConstants.palmGraspClosureThreshold)
                {
                    foreach (WeArtTouchableObject item in _pinkyBlockingTouchables)
                    {
                        if (!item.IsGraspable || item.GraspingType == GraspingType.Snap)
                            continue;

                        foreach (WeArtTouchableObject otherItem in _palmContactTouchables)
                        {
                            if (otherItem.GraspingType == GraspingType.Snap)
                                continue;

                            if (item == otherItem)
                            {
                                _isGraspingWithPalm = true;
                                _palmThimbleHaptic.IsGrasping = true;
                                _palmThimbleHaptic.GraspedObject = item;

                                _pinkyThimbleTracking.GraspedObject = item;
                                _pinkyThimbleHaptic.GraspedObject = item;
                                _pinkyThimbleTracking.IsGrasping = true;
                                _pinkyThimbleHaptic.IsGrasping = true;

                                GraspTouchableObject(item);
                                return true;
                            }
                        }
                    }
                }
            }

        }

        return false;
    }

    /// <summary>
    /// Check grasping release conditions, if not enough fingers are holding the object, release it
    /// </summary>
    private void CheckReleaseConditions()
    {
        bool isReleaseCheckGrasping = false;

        ConditionB();

        if (_isThumbTryingToGrasp && _thumbThimbleTracking.IsGrasping)
        {
            if (_indexThimbleTracking.IsGrasping)
            {
                isReleaseCheckGrasping = true;
            }

            if (_middleThimbleTracking.IsGrasping)
            {
                isReleaseCheckGrasping = true;
            }

            if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
            {
                if (_annularThimbleTracking.IsGrasping)
                {
                    isReleaseCheckGrasping = true;
                }

                if (_pinkyThimbleTracking.IsGrasping)
                {
                    isReleaseCheckGrasping = true;
                }
            }

            if (_isGraspingWithPalm && _handController.FingersMixers[0].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > WeArtConstants.palmGraspClosureThreshold)
                isReleaseCheckGrasping = true;

        }

        if (_isIndexTryingToGrasp && _indexThimbleTracking.IsGrasping)
        {
            if (_thumbThimbleTracking.IsGrasping)
            {
                isReleaseCheckGrasping = true;
            }

            if (_isGraspingWithPalm && _handController.FingersMixers[1].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > WeArtConstants.palmGraspClosureThreshold)

                isReleaseCheckGrasping = true;
        }

        if (_isMiddleTryingToGrasp && _middleThimbleTracking.IsGrasping)
        {
            if (_thumbThimbleTracking.IsGrasping)
            {
                isReleaseCheckGrasping = true;
            }

            if (_isGraspingWithPalm && _handController.FingersMixers[2].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > WeArtConstants.palmGraspClosureThreshold)
                isReleaseCheckGrasping = true;
        }

        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
        {
            if (_isAnnularTryingToGrasp && _annularThimbleTracking.IsGrasping)
            {
                if (_thumbThimbleTracking.IsGrasping)
                {
                    isReleaseCheckGrasping = true;
                }

                if (_isGraspingWithPalm && _handController.FingersMixers[3].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > WeArtConstants.palmGraspClosureThreshold)
                    isReleaseCheckGrasping = true;
            }

            if (_isPinkyTryingToGrasp && _pinkyThimbleTracking.IsGrasping)
            {
                if (_thumbThimbleTracking.IsGrasping)
                {
                    isReleaseCheckGrasping = true;
                }

                if (_isGraspingWithPalm && _handController.FingersMixers[4].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1) > WeArtConstants.palmGraspClosureThreshold)
                    isReleaseCheckGrasping = true;
            }
        }

        if (!isReleaseCheckGrasping)
            ReleaseTouchableObject();
    }

    /// <summary>
    /// Grasp a touchable object
    /// </summary>
    /// <param name="touchable"></param>
    private void GraspTouchableObject(WeArtTouchableObject touchable)
    {
        if (_otherGraspingSystem != null)
        {
            if (_otherGraspingSystem.GraspingState == GraspingState.Grabbed)
            {
                if (_otherGraspingSystem.GetGraspedObject() == touchable)
                {
                    _otherGraspingSystem.MakeTheHandUnableToGrab();
                    _otherGraspingSystem.ReleaseTouchableObject();
                }
            }
        }

        _graspingState = GraspingState.Grabbed;
        _graspedObject = touchable;
        OnGraspingEvent?.Invoke(_handSide, _graspedObject.gameObject);

        _graspedObject.GraspingHandController = _handController;
        _graspedObject.Grab(_grasper);

        if (_graspedObject.AnchoredObject == null)
        {
            CheckFingersGraspingState();
        }
        _surfaceExploration.ResetHapticObjectsPosition();

    }

    /// <summary>
    /// Release the grasped object
    /// </summary>
    public void ReleaseTouchableObject()
    {
        _graspingState = GraspingState.Released;
        _isGraspingWithPalm = false;
        _palmThimbleHaptic.IsGrasping = false;

        WeArtInterpolateFingers interpolateFingers = GetComponent<WeArtInterpolateFingers>();

        if (interpolateFingers != null)
        {
            Destroy(interpolateFingers);
        }

        if (_graspedObject != null)
        {
            WeArtInterpolateToPosition touchableInterpolate = _graspedObject.GetComponent<WeArtInterpolateToPosition>();
            if (touchableInterpolate != null)
            {
                Destroy(touchableInterpolate);
            }
            _graspedObject.Release();
            _graspedObject.GraspingHandController = null;
        }

        _thumbThimbleTracking.IsBlocked = false;
        _indexThimbleTracking.IsBlocked = false;
        _middleThimbleTracking.IsBlocked = false;
        _annularThimbleTracking.IsBlocked = false;
        _pinkyThimbleTracking.IsBlocked = false;

        _thumbThimbleTracking.IsGrasping = false;
        _indexThimbleTracking.IsGrasping = false;
        _middleThimbleTracking.IsGrasping = false;
        _annularThimbleTracking.IsGrasping = false;
        _pinkyThimbleTracking.IsGrasping = false;

        _thumbThimbleHaptic.IsGrasping = false;
        _indexThimbleHaptic.IsGrasping = false;
        _middleThimbleHaptic.IsGrasping = false;
        _annularThimbleHaptic.IsGrasping = false;
        _pinkyThimbleHaptic.IsGrasping = false;
        _palmThimbleHaptic.IsGrasping = false;

        _thumbThimbleTracking.GraspedObject = null;
        _indexThimbleTracking.GraspedObject = null;
        _middleThimbleTracking.GraspedObject = null;
        _annularThimbleTracking.GraspedObject = null;
        _pinkyThimbleTracking.GraspedObject = null;
        
        _thumbThimbleHaptic.GraspedObject = null;
        _indexThimbleHaptic.GraspedObject = null;
        _middleThimbleHaptic.GraspedObject = null;
        _annularThimbleHaptic.GraspedObject = null;
        _pinkyThimbleHaptic.GraspedObject = null;
        _palmThimbleHaptic.GraspedObject = null;

        _thumbBlockingTouchables.Clear();
        _indexBlockingTouchables.Clear();
        _middleBlockingTouchables.Clear();
        _annularBlockingTouchables.Clear();
        _pinkyBlockingTouchables.Clear();

        _thumbThimbleHaptic.RemoveEffect();
        _indexThimbleHaptic.RemoveEffect();
        _middleThimbleHaptic.RemoveEffect();
        if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
        {
            _annularThimbleHaptic.RemoveEffect();
            _pinkyThimbleHaptic.RemoveEffect();
            _palmThimbleHaptic.RemoveEffect();
        }

        OnReleaseEvent?.Invoke(_handSide, _graspedObject.gameObject);
        _graspedObject = null;
    }

    private void AnchoredGraspedDelayedCheck(WeArtTouchableObject touchableObject)
    {
        if (_graspingState == GraspingState.Grabbed && _graspedObject.AnchoredObject != null)
        {
            if (_thumbThimbleHaptic.IsGrasping && _thumbThimbleHaptic.GraspedObject == null && _thumbThimbleHaptic.TouchedObjects.Contains(_graspedObject)) 
            {
                _thumbThimbleTracking.IsGrasping = false;
                _thumbThimbleTracking.GraspedObject = null;
                _thumbThimbleTracking.IsBlocked = false;
            }

            if (_indexThimbleHaptic.IsGrasping && _indexThimbleHaptic.GraspedObject == null && _indexThimbleHaptic.TouchedObjects.Contains(_graspedObject))
            {
                _indexThimbleTracking.IsGrasping = false;
                _indexThimbleTracking.GraspedObject = null;
                _indexThimbleTracking.IsBlocked = false;
            }

            if (_middleThimbleHaptic.IsGrasping && _middleThimbleHaptic.GraspedObject == null && _middleThimbleHaptic.TouchedObjects.Contains(_graspedObject))
            {
                _middleThimbleTracking.IsGrasping = false;
                _middleThimbleTracking.GraspedObject = null;
                _middleThimbleTracking.IsBlocked = false;
            }

            if (_annularThimbleHaptic.IsGrasping && _annularThimbleHaptic.GraspedObject == null && _annularThimbleHaptic.TouchedObjects.Contains(_graspedObject))
            {
                _annularThimbleTracking.IsGrasping = false;
                _annularThimbleTracking.GraspedObject = null;
                _annularThimbleTracking.IsBlocked = false;
            }

            if (_pinkyThimbleHaptic.IsGrasping && _pinkyThimbleHaptic.GraspedObject == null && _pinkyThimbleHaptic.TouchedObjects.Contains(_graspedObject))
            {
                _pinkyThimbleTracking.IsGrasping = false;
                _pinkyThimbleTracking.GraspedObject = null;
                _pinkyThimbleTracking.IsBlocked = false;
            }

            if (_palmThimbleHaptic.IsGrasping && _palmThimbleHaptic.GraspedObject == null && _palmThimbleHaptic.TouchedObjects.Contains(_graspedObject))
            {
                PalmTriggerExitHandle(_graspedObject.GetComponent<Collider>());
            }
        }
    }

    /// <summary>
    /// Method used to check if the fingers respond correctly to grasping
    /// </summary>
    private void CheckFingersGraspingState()
    {
        CheckHapticObjectForGraspingState(_thumbThimbleTracking, _thumbThimbleHaptic, _thumbContactTouchables, _thumbBlockingTouchables);
        CheckHapticObjectForGraspingState(_indexThimbleTracking, _indexThimbleHaptic, _indexContactTouchables, _indexBlockingTouchables);
        CheckHapticObjectForGraspingState(_middleThimbleTracking, _middleThimbleHaptic, _middleContactTouchables, _middleBlockingTouchables);
        if (WeArtController.Instance.DeviceGeneration == DeviceGeneration.TD_Pro)
        {
            CheckHapticObjectForGraspingState(_annularThimbleTracking, _annularThimbleHaptic, _annularContactTouchables, _annularBlockingTouchables);
            CheckHapticObjectForGraspingState(_pinkyThimbleTracking, _pinkyThimbleHaptic, _pinkyContactTouchables, _pinkyBlockingTouchables);
            CheckHapticObjectForGraspingState(null, _palmThimbleHaptic, _palmContactTouchables, null);
        }
    }

    /// <summary>
    /// Individual method for checking fingers if they are in the grasping state
    /// </summary>
    /// <param name="trackingObject"></param>
    /// <param name="hapticObject"></param>
    /// <param name="contactTouchables"></param>
    /// <param name="blockingTouchables"></param>
    private void CheckHapticObjectForGraspingState(WeArtThimbleTrackingObject trackingObject, WeArtHapticObject hapticObject,List<WeArtTouchableObject> contactTouchables, List<WeArtTouchableObject> blockingTouchables)
    {
        if(trackingObject == null)
        {
            foreach (var item in contactTouchables)
            {
                if (item == _graspedObject)
                {
                    if (hapticObject.IsGrasping == false)
                        hapticObject.IsGrasping = true;
                    if (hapticObject.GraspedObject == null)
                        hapticObject.GraspedObject = _graspedObject;

                    return;
                }
            }
        }
        else
        {
            foreach (var item in blockingTouchables)
            {
                if (item == _graspedObject)
                {
                    if (trackingObject.IsGrasping == false)
                        trackingObject.IsGrasping = true;
                    if (trackingObject.GraspedObject == null)
                        trackingObject.GraspedObject = _graspedObject;
                    if(hapticObject.IsGrasping == false)
                        hapticObject.IsGrasping = true;
                    if(hapticObject.GraspedObject == null)
                        hapticObject.GraspedObject= _graspedObject;

                    return;
                }
            }
        }
    }

    /// <summary>
    /// Make the hand unable to grab objects for a set time
    /// </summary>
    public void MakeTheHandUnableToGrab()
    {
        _notAllowedToGrabSeconds = _notAllowedToGrabSecondsMax;
        _handController.SetSlowFingerAnimationTime( _handController.GetSlowFingerAnimationTimeMax());
    }

    /// <summary>
    /// When a touchable object is disabled, unblock the fingers that were holding it
    /// </summary>
    /// <param name="hapticObject"></param>
    /// <param name="touchable"></param>
    public void UnblockFingerOnDisable(WeArtHapticObject hapticObject, WeArtTouchableObject touchable)
    {
        if (_graspingState == GraspingState.Grabbed)
        {
            if (touchable == _graspedObject)
            {
                ReleaseTouchableObject();
                MakeTheHandUnableToGrab();
            }
        }

        if (hapticObject == _thumbThimbleHaptic)
        {
            ThumbGraspCheckExitHandle(touchable.GetComponent<Collider>());
            ThumbTriggerExitHandle(touchable.GetComponent<Collider>());
        }

        if (hapticObject == _indexThimbleHaptic)
        {
            IndexGraspCheckExitHandle(touchable.GetComponent<Collider>());
            IndexTriggerExitHandle(touchable.GetComponent<Collider>());
        }

        if (hapticObject == _middleThimbleHaptic)
        {
            MiddleGraspCheckExitHandle(touchable.GetComponent<Collider>());
            MiddleTriggerExitHandle(touchable.GetComponent<Collider>());
        }

        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
        {
            if (hapticObject == _annularThimbleHaptic)
            {
                AnnularGraspCheckExitHandle(touchable.GetComponent<Collider>());
                AnnularTriggerExitHandle(touchable.GetComponent<Collider>());
            }

            if (hapticObject == _pinkyThimbleHaptic)
            {
                PinkyGraspCheckExitHandle(touchable.GetComponent<Collider>());
                PinkyTriggerExitHandle(touchable.GetComponent<Collider>());
            }
        }
    }

    private void CheckForSnapGraspObjects()
    {
        _snapGraspPose = WeArtEasyGraspManager.Instance.GetClosestPoseInRange(_handSide, _snapGraspProximityTransform.position);
        if(_snapGraspPose != null)
        {
            _snapGraspClosestTouchable = _snapGraspPose.GetTouchable();
        }
        else
        {
            _snapGraspClosestTouchable = null;
        }
    }

    /// <summary>
    /// Check if the finger is stoped by a surface
    /// </summary>
    private void CheckFingerBlocking()
    {
        CheckIndividualFingerBlocking(_thumbThimbleTracking,_thumbThimbleHaptic, _thumbContactTouchables, _thumbProximityTouchables, _thumbBlockingTouchables, 0);
        CheckIndividualFingerBlocking(_indexThimbleTracking, _indexThimbleHaptic, _indexContactTouchables, _indexProximityTouchables, _indexBlockingTouchables, 1);
        CheckIndividualFingerBlocking(_middleThimbleTracking,_middleThimbleHaptic,_middleContactTouchables, _middleProximityTouchables, _middleBlockingTouchables, 2);
        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
        {
            CheckIndividualFingerBlocking(_annularThimbleTracking,_annularThimbleHaptic, _annularContactTouchables, _annularProximityTouchables, _annularBlockingTouchables, 3);
            CheckIndividualFingerBlocking(_pinkyThimbleTracking,_pinkyThimbleHaptic, _pinkyContactTouchables, _pinkyProximityTouchables, _pinkyBlockingTouchables, 4);
        }
    }

    #region Finger Collision Assigments

    // Commuon fingertip collision methods
    void FingerTriggerEnterHandle(Collider other, WeArtHapticObject hapticObject, WeArtThimbleTrackingObject thimbleTrackingObject, List<WeArtTouchableObject> contactTouchables, List<WeArtTouchableObject> graspCheckTouchables, int fingerNumber)
    {
        if (TryGetTouchableObjectFromCollider(other, out var touchable))
        {
            hapticObject.AddTouchedObject(touchable);

            if (!contactTouchables.Contains(touchable))
            {
                contactTouchables.Add(touchable);
            }

            if (!thimbleTrackingObject.IsBlocked && _graspingState == GraspingState.Grabbed && _graspedObject == touchable)
            {
                thimbleTrackingObject.IsBlocked = true;
                thimbleTrackingObject.BlockedClosureValue = _handController.FingersMixers[fingerNumber].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
                thimbleTrackingObject.GraspedObject = touchable;
                hapticObject.IsGrasping = true;
                hapticObject.GraspedObject = touchable;
                foreach (var item in graspCheckTouchables)
                {
                    if (item == touchable)
                        thimbleTrackingObject.IsGrasping = true;
                }

                WeArtTouchEffect effect = new WeArtTouchEffect();
                effect.Set(touchable.Temperature, hapticObject.CalculatePhysicalForce(touchable), touchable.Texture, null);
                hapticObject.AddEffect(effect, touchable);
            }

            hapticObject.HandleOnTriggerEnter(other);
        }
    }

    void FingerTriggerStayHandle(Collider other, WeArtHapticObject hapticObject)
    {
        hapticObject.HandleOnTriggerStay(other);
    }

    void FingerTriggerExitHandle(Collider other, WeArtHapticObject hapticObject, WeArtThimbleTrackingObject thimbleTrackingObject, List<WeArtTouchableObject> contactTouchables, List<WeArtTouchableObject> blockingTouchables)
    {
        if (TryGetTouchableObjectFromCollider(other, out var touchable))
        {
            hapticObject.RemoveTouchedObject(touchable);

            if (contactTouchables.Contains(touchable))
            {
                contactTouchables.Remove(touchable);
            }

            if (blockingTouchables.Contains(touchable))
            {
                blockingTouchables.Remove(touchable);
            }

            if (_graspingState == GraspingState.Grabbed)
            {
                if (touchable == _graspedObject)
                {
                    hapticObject.IsGrasping = false;
                    hapticObject.GraspedObject = null;
                    
                }
            }

            hapticObject.HandleOnTriggerExit(other);
        }

    }

    /// <summary>
    /// Thumb haptic entering a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void ThumbTriggerEnterHandle(Collider other)
    {
        FingerTriggerEnterHandle(other, _thumbThimbleHaptic, _thumbThimbleTracking, _thumbContactTouchables, _thumbProximityTouchables, 0);
    }

    /// <summary>
    /// Thumb haptic staying in a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void ThumbTriggerStayHandle(Collider other)
    {
        FingerTriggerStayHandle(other, _thumbThimbleHaptic);
    }

    /// <summary>
    /// Thumb haptic exiting a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void ThumbTriggerExitHandle(Collider other)
    {
        FingerTriggerExitHandle(other, _thumbThimbleHaptic,_thumbThimbleTracking,_thumbContactTouchables,_thumbBlockingTouchables);
    }

    /// <summary>
    /// Index haptic entering a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void IndexTriggerEnterHandle(Collider other)
    {
        FingerTriggerEnterHandle(other, _indexThimbleHaptic, _indexThimbleTracking, _indexContactTouchables, _indexProximityTouchables, 1);
    }

    /// <summary>
    /// Index haptic staying in a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void IndexTriggerStayHandle(Collider other)
    {
        FingerTriggerStayHandle(other, _indexThimbleHaptic);
    }

    /// <summary>
    /// Index haptic exiting a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void IndexTriggerExitHandle(Collider other)
    {
        FingerTriggerExitHandle(other, _indexThimbleHaptic, _indexThimbleTracking, _indexContactTouchables, _indexBlockingTouchables);
    }

    /// <summary>
    /// Middle haptic entering a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void MiddleTriggerEnterHandle(Collider other)
    {
        FingerTriggerEnterHandle(other, _middleThimbleHaptic, _middleThimbleTracking, _middleContactTouchables, _middleProximityTouchables, 2);
    }

    /// <summary>
    /// Middle haptic staying in a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void MiddleTriggerStayHandle(Collider other)
    {
        FingerTriggerStayHandle(other, _middleThimbleHaptic);
    }

    /// <summary>
    /// Middle haptic exiting a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void MiddleTriggerExitHandle(Collider other)
    {
        FingerTriggerExitHandle(other, _middleThimbleHaptic, _middleThimbleTracking, _middleContactTouchables, _middleBlockingTouchables);
    }

    /// <summary>
    /// Annular haptic entering a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void AnnularTriggerEnterHandle(Collider other)
    {
        FingerTriggerEnterHandle(other, _annularThimbleHaptic, _annularThimbleTracking, _annularContactTouchables, _annularProximityTouchables, 3);
    }

    /// <summary>
    /// Annular haptic staying in a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void AnnularTriggerStayHandle(Collider other)
    {
        FingerTriggerStayHandle(other, _annularThimbleHaptic);
    }

    /// <summary>
    /// Annular haptic exiting a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void AnnularTriggerExitHandle(Collider other)
    {
        FingerTriggerExitHandle(other, _annularThimbleHaptic, _annularThimbleTracking, _annularContactTouchables, _annularBlockingTouchables);
    }

    /// <summary>
    /// Pinky haptic entering a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void PinkyTriggerEnterHandle(Collider other)
    {
        FingerTriggerEnterHandle(other, _pinkyThimbleHaptic, _pinkyThimbleTracking, _pinkyContactTouchables, _pinkyProximityTouchables, 4);
    }

    /// <summary>
    /// Pinky haptic staying in a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void PinkyTriggerStayHandle(Collider other)
    {
        FingerTriggerStayHandle(other, _pinkyThimbleHaptic);
    }

    /// <summary>
    /// Pinky haptic exiting a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void PinkyTriggerExitHandle(Collider other)
    {
        FingerTriggerExitHandle(other, _pinkyThimbleHaptic, _pinkyThimbleTracking, _pinkyContactTouchables, _pinkyBlockingTouchables);
    }

    // Palm Collisions
    // TODO test refactor and add palm

    /// <summary>
    /// Palm haptic entering a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void PalmTriggerEnterHandle(Collider other)
    {
        if (TryGetTouchableObjectFromCollider(other, out var touchable))
        {
            _palmThimbleHaptic.AddTouchedObject(touchable);

            if (!_palmContactTouchables.Contains(touchable))
            {
                _palmContactTouchables.Add(touchable);

                if (_graspingState == GraspingState.Released && touchable.GetGraspingState() == GraspingState.Grabbed)
                {
                    //_palmThimbleHaptic.ForceOnColliderEnter(touchable.GetComponent<Collider>());
                }
            }
                _palmThimbleHaptic.HandleOnTriggerEnter(other);
        }
    }

    /// <summary>
    /// Palm haptic staying in a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void PalmTriggerStayHandle(Collider other)
    {
        if (TryGetTouchableObjectFromCollider(other, out var touchable))
        {
            _palmThimbleHaptic.HandleOnTriggerStay(other);
        }
    }

    /// <summary>
    /// Palm haptic exiting a touchable object collider
    /// </summary>
    /// <param name="other"></param>
    void PalmTriggerExitHandle(Collider other)
    {
        if (TryGetTouchableObjectFromCollider(other, out var touchable))
        {
            _palmThimbleHaptic.RemoveTouchedObject(touchable);

            if (_palmContactTouchables.Contains(touchable))
            {
                _palmContactTouchables.Remove(touchable);
            }
                _palmThimbleHaptic.HandleOnTriggerExit(other);
        }
    }

    #endregion

    #region Grasp Checkers 

    // Commun grasping proximity checking methods
    void ProximityGraspCheckEnterHandle(Collider other, List<WeArtTouchableObject> graspCheckTouchables)
    {
        if (other.isTrigger)
            return;

        if (TryGetTouchableObjectFromCollider(other, out var touchable))
        {
            if (!graspCheckTouchables.Contains(touchable))
            {
                graspCheckTouchables.Add(touchable);
            }

        }
    }

    void ProximityGraspCheckStayHandle(Collider other, List<WeArtTouchableObject> graspCheckTouchables, WeArtThimbleTrackingObject thimbleTrackingObject)
    {
        if (other.isTrigger)
            return;

        if (thimbleTrackingObject.IsBlocked)
            return;

        if (TryGetTouchableObjectFromCollider(other, out var touchable))
        {
            if (!graspCheckTouchables.Contains(touchable))
            {
                graspCheckTouchables.Add(touchable);
            }
        }
    }

    void ProximityGraspCheckExitHandle(Collider other, List<WeArtTouchableObject> graspCheckTouchables)
    {
        if (other.isTrigger)
            return;

        if (TryGetTouchableObjectFromCollider(other, out var touchable))
        {
            if (graspCheckTouchables.Contains(touchable))
            {
                graspCheckTouchables.Remove(touchable);
            }
        }
    }


    /// <summary>
    /// Thumb proximity check collides with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void ThumbGraspCheckEnterHandle(Collider other)
    {
        ProximityGraspCheckEnterHandle(other, _thumbProximityTouchables);
    }

    /// <summary>
    /// Thumb proximity check stays in contact with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void ThumbGraspCheckStayHandle(Collider other)
    {
        ProximityGraspCheckStayHandle(other, _thumbProximityTouchables, _thumbThimbleTracking);
    }

    /// <summary>
    /// Thumb proximity check exits a touchable object
    /// </summary>
    /// <param name="other"></param>
    void ThumbGraspCheckExitHandle(Collider other)
    {
        ProximityGraspCheckExitHandle(other, _thumbProximityTouchables);
    }

    /// <summary>
    /// Index proximity check collides with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void IndexGraspCheckEnterHandle(Collider other)
    {
        ProximityGraspCheckEnterHandle(other, _indexProximityTouchables);
    }

    /// <summary>
    /// Index proximity check stays in contact with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void IndexGraspCheckStayHandle(Collider other)
    {
        ProximityGraspCheckStayHandle(other, _indexProximityTouchables, _indexThimbleTracking);
    }

    /// <summary>
    /// Index proximity check exits a touchable object
    /// </summary>
    /// <param name="other"></param>
    void IndexGraspCheckExitHandle(Collider other)
    {
        ProximityGraspCheckExitHandle(other, _indexProximityTouchables);
    }

    /// <summary>
    /// Middle proximity check collides with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void MiddleGraspCheckEnterHandle(Collider other)
    {
        ProximityGraspCheckEnterHandle(other, _middleProximityTouchables);
    }

    /// <summary>
    /// Middle proximity check stays in contact with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void MiddleGraspCheckStayHandle(Collider other)
    {
        ProximityGraspCheckStayHandle(other, _middleProximityTouchables, _middleThimbleTracking);
    }

    /// <summary>
    /// Middle proximity check exits a touchable object
    /// </summary>
    /// <param name="other"></param>
    void MiddleGraspCheckExitHandle(Collider other)
    {
        ProximityGraspCheckExitHandle(other, _middleProximityTouchables);
    }

    /// <summary>
    /// Annular proximity check collides with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void AnnularGraspCheckEnterHandle(Collider other)
    {
        ProximityGraspCheckEnterHandle(other, _annularProximityTouchables);
    }

    /// <summary>
    /// Annular proximity check stays in contact with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void AnnularGraspCheckStayHandle(Collider other)
    {
        ProximityGraspCheckStayHandle(other, _annularProximityTouchables, _annularThimbleTracking);
    }

    /// <summary>
    /// Annular proximity check exits a touchable object
    /// </summary>
    /// <param name="other"></param>
    void AnnularGraspCheckExitHandle(Collider other)
    {
        ProximityGraspCheckExitHandle(other, _annularProximityTouchables);
    }

    /// <summary>
    /// Pinky proximity check collides with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void PinkyGraspCheckEnterHandle(Collider other)
    {
        ProximityGraspCheckEnterHandle(other, _pinkyProximityTouchables);
    }

    /// <summary>
    /// Pinky proximity check stays in contact with a touchable object
    /// </summary>
    /// <param name="other"></param>
    void PinkyGraspCheckStayHandle(Collider other)
    {
        ProximityGraspCheckStayHandle(other, _pinkyProximityTouchables, _pinkyThimbleTracking);
    }

    /// <summary>
    /// Pinky proximity check exits a touchable object
    /// </summary>
    /// <param name="other"></param>
    void PinkyGraspCheckExitHandle(Collider other)
    {
        ProximityGraspCheckExitHandle(other, _pinkyProximityTouchables);
    }

    #endregion

    /// <summary>
    /// Try to find a touchable object component on the collider
    /// </summary>
    /// <param name="collider">The collider<see cref="Collider"/>.</param>
    /// <param name="touchableObject">The touchableObject<see cref="WeArtTouchableObject"/>.</param>
    /// <returns>The <see cref="bool"/>.</returns>
    private static bool TryGetTouchableObjectFromCollider(Collider collider, out WeArtTouchableObject touchableObject)
    {
        touchableObject = collider.gameObject.GetComponent<WeArtTouchableObject>();
        if (collider.gameObject.GetComponent<WeArtChildCollider>() != null)
        {
            touchableObject = collider.gameObject.GetComponent<WeArtChildCollider>().ParentTouchableObject;
        }
        return touchableObject != null;
    }

    /// <summary>
    /// Try to get WeArtGraspPose script from current touchable object
    /// </summary>
    /// <param name="gameObject">Current grasped object</param>
    /// <param name="customPoses">Output of this result</param>
    /// <returns></returns>
    private static bool TryGetCustomPosesFromTouchable(GameObject gameObject, out WeArtGraspPose customPoses)
    {
        customPoses = gameObject.GetComponent<WeArtGraspPose>();
        return customPoses != null;
    }
}
