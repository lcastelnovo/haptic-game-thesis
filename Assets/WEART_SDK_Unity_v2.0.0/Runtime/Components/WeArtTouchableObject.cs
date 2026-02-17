using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UIElements;
using WeArt.Core;
using Texture = WeArt.Core.Texture;

namespace WeArt.Components
{
    /// <summary>
    /// Use this component to add haptic properties to an object. These properties will create
    /// the haptic effect on <see cref="WeArtHapticObject"/>s on collision (both objects
    /// need to have physical components such as a <see cref="Rigidbody"/> and at least one
    /// <see cref="Collider"/>).
    /// </summary>
    public class WeArtTouchableObject : MonoBehaviour
    {
        #region Fields

        /// <summary>
        /// Defines the _temperature.
        /// </summary>
        [SerializeField] internal Temperature _temperature = Temperature.Default;

        /// <summary>
        /// Defines the _stiffness.
        /// </summary>
        [SerializeField] internal Force _stiffness = Force.Default;

        /// <summary>
        /// Defines the _texture.
        /// </summary>
        [SerializeField] internal Texture _texture = Texture.Default;

        /// <summary>
        /// Defines if the object is graspable
        /// </summary>
        [SerializeField] internal bool _graspable = false;

        /// <summary>
        /// Defines if the object uses surface exploration
        /// </summary>
        [SerializeField] internal bool _surfaceExploration = false;

        /// <summary>
        /// Defines the grasping state used for this touchable object
        /// </summary>
        [SerializeField] internal GraspingType _graspingType = GraspingType.Physical;

        /// <summary>
        /// Defines the _touchedHapticsEffects.
        /// </summary>
        [NonSerialized]
        public Dictionary<WeArtHapticObject, WeArtTouchEffect> TouchedHapticsEffects =
            new Dictionary<WeArtHapticObject, WeArtTouchEffect>();

        /// <summary>
        /// Defines the original parent object
        /// </summary>
        private Transform _parentGameObject;

        /// <summary>
        /// Defines the _rigidbody.
        /// </summary>
        private Rigidbody _rigibody;

        /// <summary>
        /// Defines the original UseGravity
        /// </summary>
        private bool _originalUseGravity;

        /// <summary>
        /// Defines the original IsKinematic.
        /// </summary>
        private bool _originalIsKinematic;

        /// <summary>
        /// Defines the original Drag
        /// </summary>
        private float _originalDrag;

        /// <summary>
        /// Defines the original Angular Drag
        /// </summary>
        private float _originalAngularDrag;

        /// <summary>
        /// Defines the original Interpolate value of rigidbody
        /// </summary>
        private RigidbodyInterpolation _originalInterpolate;

        /// <summary>
        /// Defines the original Collision Detection value of rigidbody
        /// </summary>
        private CollisionDetectionMode _originalCollisionDetection;

        /// <summary>
        /// The original rigidbody constrains on instantiation
        /// </summary>
        private RigidbodyConstraints _originalRigidbodyConstraints;

        /// <summary>
        /// The original mass of the rigidbody on instantiation
        /// </summary>
        private float _originalRigidbodyMass = 1;

        /// <summary>
        /// Defines the grasping state of the object
        /// </summary>
        private GraspingState _graspingState = GraspingState.Released;

        /// <summary>
        /// The hand controller that is curently grasping the object
        /// </summary>
        private WeArtHandController _graspingHandController;

        /// <summary>
        /// The parent of the hand controller
        /// </summary>
        private Transform _graspingHandParent;

        /// <summary>
        /// Reference to the WeArtAnchoredObject component, if found
        /// </summary>
        private WeArtAnchoredObject _anchoredObject = null;

        /// <summary>
        /// Reference to self game object
        /// </summary>
        private GameObject _currentGameObject;

        /// <summary>
        /// A list of data that stores the poses on the touchable object
        /// </summary>
        private List<EasyGraspData> _easyGraspDataList = new List<EasyGraspData>();

        /// <summary>
        /// Public read only method to get the list of easy grasp posses
        /// </summary>
        public IReadOnlyList<EasyGraspData> EasyGraspDataList => _easyGraspDataList;

        /// Defines the colection of colliders present on the gameobject and on the children of this gameobject, recursively
        /// </summary>
        private List<Collider> _touchableColliders = new List<Collider>();

        /// <summary>
        /// Defines the first collider found on the gameobject, if there is none, it will start looking through children recursively.
        /// It represents the main collider of the touchable object used for interaction with haptic objects.
        /// </summary>
        private Collider _firstCollider;

        /// <summary>
        /// Defines if in the heirarchy there is a direct or indirect parent that is also a touchable object
        /// </summary>
        private bool _isChildOfAnotherTouchable = false;

        #endregion

        #region Events

        /// <summary>
        /// Called when the set of affected haptic objects changed after collision events.
        /// </summary>
        public event Action OnAffectedHapticObjectsUpdate;

        /// <summary>
        /// Called when grasping state is changed.
        /// </summary>
        public event Action<GraspingState> OnGraspingStateChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the grasping type of the touchable object
        /// </summary>
        public GraspingType GraspingType
        {
            get { return _graspingType; }
            set { _graspingType = value; }
        }

        /// <summary>
        /// Gets and sets the hand controller that is grasping the object
        /// </summary>
        public WeArtHandController GraspingHandController
        {
            get { return _graspingHandController; }
            set { _graspingHandController = value; }
        }

        /// <summary>
        /// The temperature of this object.
        /// </summary>
        public Temperature Temperature
        {
            get => _temperature;
            set
            {
                _temperature = value;
                UpdateTouchedHaptics();
            }
        }

        /// <summary>
        /// Gets the grasping state of the object
        /// </summary>
        public GraspingState GetGraspingState()
        {
            return _graspingState;
        }

        /// <summary>
        /// The stiffness of this object, that will translate as a haptic force.
        /// </summary>
        public Force Stiffness
        {
            get => _stiffness;
            set
            {
                _stiffness = value;
                UpdateTouchedHaptics();
            }
        }

        /// <summary>
        /// The haptic texture of this object.
        /// </summary>
        public Texture Texture
        {
            get => _texture;
            set
            {
                _texture.Active = value.Active;
                _texture.TextureType = value.TextureType;
                _texture.Volume = value.Volume;
                _texture.Velocity = value.Velocity;
                _texture.ForcedVelocity = value.ForcedVelocity;

                UpdateTouchedHaptics();
            }
        }

        /// <summary>
        /// Gets and sets the graspable state of the touchable object.
        /// </summary>
        public bool Graspable
        {
            get => _graspable;
            set => _graspable = value;
        }

        /// <summary>
        /// Returns the anchored object component if found, if it is a normal object it will return null
        /// </summary>
        public WeArtAnchoredObject AnchoredObject
        {
            get => _anchoredObject;
        }

        /// <summary>
        /// Gets the AffectedHapticObjects
        /// The collection of haptic objects currently touching this object.
        /// </summary>
        public IReadOnlyCollection<WeArtHapticObject> AffectedHapticObjects => TouchedHapticsEffects.Keys;

        /// <summary>
        /// Gets a value indicating whether IsGraspable.
        /// </summary>
        public bool IsGraspable
        {
            get => _graspable;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the first collider found on the gameobject or on its children
        /// </summary>
        /// <returns></returns>
        public Collider GetFirstCollider()
        {
            return _firstCollider;
        }

        /// <summary>
        /// Returns the cololiders of the gameobjects and its children
        /// </summary>
        /// <returns></returns>
        public List<Collider> GetTouchableColliders()
        {
            return _touchableColliders;
        }


        /// <summary>
        /// Method used when manually adding a collider to a touchable object 
        /// </summary>
        /// <param name="collider"></param>
        public void AddColliderToTouchableColliders(Collider collider)
        {
            if(!_touchableColliders.Contains(collider))
            {
                _touchableColliders.Add(collider);
            }
        }

        /// <summary>
        /// Invoke the delegate OnAffectedHapticObjectsUpdate
        /// </summary>
        public void CallOnAffectedHapticObjectsUpdate()
        {
            OnAffectedHapticObjectsUpdate?.Invoke();
        }

        /// <summary>
        /// The UpdateTouchedHaptics.
        /// </summary>
        private void UpdateTouchedHaptics()
        {
            foreach (var pair in TouchedHapticsEffects)
            {
                if (pair.Value != null)
                    pair.Value.Set(Temperature, pair.Key.CalculatePhysicalForce(this), Texture, null);
            }
        }

        /// <summary>
        /// Set initial values on Awake.
        /// </summary>
        private void Awake()
        {
            ReadInitialPoseData();
            
            SaveCurrentRigidbodyParams();
            
            _currentGameObject = gameObject;
            if (TryGetComponent<WeArtAnchoredObject>(out WeArtAnchoredObject anchoredObject))
            {
                _anchoredObject = anchoredObject;
            }

            foreach (var item in transform.GetComponents<Collider>())
            {
                _touchableColliders.Add(item);
            }

            if (!_isChildOfAnotherTouchable)
            {
                SetChildrenColliders(transform);
                CheckChildColliderList();
            }
        }

        /// <summary>
        /// Save the current rigibody parameters as original values
        /// </summary>
        public void SaveCurrentRigidbodyParams()
        {
            _rigibody = gameObject.GetComponent<Rigidbody>();

            _originalUseGravity = _rigibody.useGravity;
            _originalIsKinematic = _rigibody.isKinematic;
            _parentGameObject = gameObject.transform.parent;
            _originalRigidbodyConstraints = _rigibody.constraints;
            _originalRigidbodyMass = _rigibody.mass;
            _originalAngularDrag = _rigibody.angularDrag;
            _originalDrag = _rigibody.drag;
            _originalCollisionDetection = _rigibody.collisionDetectionMode;
            _originalInterpolate = _rigibody.interpolation;
        }

        /// <summary>
        /// Recursively find all colliders associated with the touchable object
        /// </summary>
        /// <param name="transform"></param>
        public void SetChildrenColliders(Transform transform)
        {
            foreach (Transform item in transform)
            {

                if (item.TryGetComponent<WeArtHandController>(out WeArtHandController handController))
                {
                    continue;
                }

                if(item.TryGetComponent<WeArtTouchableObject>(out WeArtTouchableObject touchableObject))
                {
                    touchableObject.IsChildOfAnotherTouchable = true;

                   if( touchableObject.transform.TryGetComponent<Rigidbody>(out Rigidbody touchableRigidbody))
                   {
                        touchableObject.SaveCurrentRigidbodyParams();
                        Destroy(touchableRigidbody);
                   }
                }

                if (!item.gameObject.TryGetComponent<WeArtChildCollider>(out WeArtChildCollider childColliderComponent))
                {
                    WeArtChildCollider childCollider = item.gameObject.AddComponent<WeArtChildCollider>();
                    childCollider.ParentTouchableObject = this;
                }
                else
                {
                    childColliderComponent.ParentTouchableObject = this;
                }

                Collider[] subColliders = item.GetComponents<Collider>();

                foreach (var subCollider in subColliders)
                {
                    if (!_touchableColliders.Contains(subCollider))
                        _touchableColliders.Add(subCollider);
                }

                SetChildrenColliders(item);
            }
        }

        /// <summary>
        /// Remove null references form the _touchableColliders list and find a new _firstCollider if it is null
        /// </summary>
        public void CheckChildColliderList()
        {

            for (int i = _touchableColliders.Count -1; i >= 0 ; i--)
            {
                if (_touchableColliders[i] == null)
                {
                    _touchableColliders.RemoveAt(i);
                }
            }

            if (_firstCollider == null && _touchableColliders.Count > 0)
                _firstCollider = _touchableColliders[0];

        }

        /// <summary>
        /// Checks if the parent is changes and reacts to it if the parent contains another touchable object, a hand controller or just a normal gameobject
        /// </summary>
        private void OnTransformParentChanged()
        {
            if (transform.parent == null)
            {
                BecomeIndependentTouchableObject();
                return;
            }

            if(transform.parent.parent!= null)
            {
                if(transform.parent.parent.GetComponent<WeArtHandController>() != null)
                {
                    return;
                }
            }

            if (transform.parent.TryGetComponent<WeArtChildCollider>(out WeArtChildCollider childCollider))
            {
                BecomeChildToTouchableObject(childCollider.ParentTouchableObject);
                return;
            }

            if (transform.parent.TryGetComponent<WeArtTouchableObject>(out WeArtTouchableObject touchable))
            {
                BecomeChildToTouchableObject(touchable);
                return;
            }

            BecomeIndependentTouchableObject();
        }

        /// <summary>
        /// Removes the rigidbody of the gameobject and prepares it to be used only for its colliders. The touchable object will be inactive in this state, it will not render its own effect
        /// </summary>
        /// <param name="touchable"></param>
        private void BecomeChildToTouchableObject(WeArtTouchableObject touchable)
        {
            if(_rigibody != null)
                Destroy(_rigibody);

            _isChildOfAnotherTouchable = true;
        }

        /// <summary>
        /// Gains back all properties. That includes its rigidbody and now the touchable object will use its own effect
        /// </summary>
        private void BecomeIndependentTouchableObject()
        {
            _parentGameObject = transform.parent;
            _isChildOfAnotherTouchable = false;

            if(transform.TryGetComponent<WeArtChildCollider>(out WeArtChildCollider childCollider))
            {
                Destroy(childCollider);
            }

            if (_rigibody == null)
            {
                _rigibody = transform.gameObject.AddComponent<Rigidbody>();

                _rigibody.isKinematic = _originalIsKinematic;
                _rigibody.useGravity = _originalUseGravity;
                _rigibody.constraints = _originalRigidbodyConstraints;
                _rigibody.mass = _originalRigidbodyMass;
                _rigibody.angularDrag = _originalAngularDrag;
                _rigibody.drag = _originalDrag;
                _rigibody.interpolation = _originalInterpolate;
                _rigibody.collisionDetectionMode = _originalCollisionDetection;
            }

            StartCoroutine(DelayedChildrenCheck());
        }

        /// <summary>
        /// Defines if the direct or indirect parent is another touchable object
        /// </summary>
        public bool IsChildOfAnotherTouchable
        {
            get { return _isChildOfAnotherTouchable; }
            set { _isChildOfAnotherTouchable = value; }
        }

        /// <summary>
        /// Method fired when the gameobject has a child added or removed from it
        /// </summary>
        private void OnTransformChildrenChanged()
        {
            StartCoroutine(DelayedChildrenCheck());
        }

        /// <summary>
        /// Next frame check for colliders and check the _touchableColliders list
        /// </summary>
        /// <returns></returns>
        private IEnumerator DelayedChildrenCheck()
        {
            yield return null;
            SetChildrenColliders(transform);
            CheckChildColliderList();
        }

        /// <summary>
        /// The OnDisable.
        /// </summary>
        private void OnDisable()
        {
            Invoke("DelayOnDisable", 0);
        }

        /// <summary>
        /// Coroutine used for setting the parent on the next frame
        /// </summary>
        /// <returns></returns>
        private void DelayOnDisable()
        {
            List<WeArtHapticObject> weArtHapticObjects = new List<WeArtHapticObject>();

            foreach (var haptic in TouchedHapticsEffects)
            {
                weArtHapticObjects.Add(haptic.Key);
            }

            foreach (var haptic in weArtHapticObjects)
            {
                haptic.UnblockFingerOnDisable(this);
            }

            TouchedHapticsEffects.Clear();
            OnAffectedHapticObjectsUpdate?.Invoke();
        }

        /// <summary>
        /// The TryGetHapticObjectFromCollider.
        /// </summary>
        /// <param name="collider">The collider<see cref="Collider"/>.</param>
        /// <param name="hapticObject">The hapticObject<see cref="WeArtHapticObject"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        private static bool TryGetHapticObjectFromCollider(Collider collider, out WeArtHapticObject hapticObject)
        {
            hapticObject = collider.gameObject.GetComponent<WeArtHapticObject>();
            return hapticObject != null;
        }

        /// <summary>
        /// The grab method called by the hand controller
        /// </summary>
        /// <param name="grasper">The grasper<see cref="GameObject"/>.</param>
        public void Grab(GameObject grasper)
        {
            if (_anchoredObject != null)
            {
                _graspingHandParent = _graspingHandController.transform.parent;
                _graspingHandController.GetGraspingSystem()
                    .AddOrRemoveRigidbodiesFromFinggers(true, _firstCollider);
                _graspingHandController.transform.parent = transform;
                Destroy(_graspingHandController.GetComponent<Rigidbody>());
                _graspingHandController.GetComponent<WeArtDeviceTrackingObject>().IsAnchored = true;
                _graspingState = GraspingState.Grabbed;
                OnGraspingStateChanged?.Invoke(_graspingState);

                return;
            }

            _rigibody.useGravity = false;
            _rigibody.isKinematic = false;
            _rigibody.constraints = RigidbodyConstraints.FreezeAll;
            transform.parent = grasper.transform;

            _graspingHandController.GetGraspingSystem().AddOrRemoveRigidbodiesFromFinggers(true, _firstCollider);
            Destroy(_rigibody);

            //FixedJoint joint = gameObject.AddComponent<FixedJoint>();
            //joint.connectedBody = grasper.transform.parent.GetComponent<Rigidbody>();

            _graspingState = GraspingState.Grabbed;
            OnGraspingStateChanged?.Invoke(_graspingState);

        }

        /// <summary>
        /// The release called by the hand controller
        /// </summary>
        public void Release()
        {
            _graspingHandController.GetComponent<WeArtDeviceTrackingObject>().StartSafeRelease();

            if (_anchoredObject != null)
            {
                if(_graspingHandController.gameObject.GetComponent<WeArtInterpolateToPosition>()!= null)
                {
                    Destroy(_graspingHandController.gameObject.GetComponent<WeArtInterpolateToPosition>());
                }

                _graspingHandController.GetComponent<WeArtDeviceTrackingObject>().IsAnchored = false;
                Rigidbody rb = _graspingHandController.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.mass = 1;
                rb.useGravity = false;
                _graspingHandController.GetGraspingSystem()
                    .AddOrRemoveRigidbodiesFromFinggers(false, _firstCollider);
                _graspingHandController.transform.parent = _graspingHandParent;
                _graspingState = GraspingState.Released;
                OnGraspingStateChanged?.Invoke(_graspingState);
                return;
            }

            if (GetComponent<FixedJoint>() != null)
            {
                GetComponent<FixedJoint>().connectedBody = null;
                Destroy(GetComponent<FixedJoint>());
            }

            transform.parent = _parentGameObject;

            _rigibody.useGravity = _originalUseGravity;
            _rigibody.isKinematic = _originalIsKinematic;
            _rigibody.constraints = _originalRigidbodyConstraints;
            _rigibody.angularDrag = _originalAngularDrag;
            _rigibody.drag = _originalDrag;
            _rigibody.mass = _originalRigidbodyMass;
            _rigibody.interpolation = _originalInterpolate;
            _rigibody.collisionDetectionMode = _originalCollisionDetection;

            _graspingState = GraspingState.Released;

            _graspingHandController.GetGraspingSystem().AddOrRemoveRigidbodiesFromFinggers(false, _firstCollider);

            OnGraspingStateChanged?.Invoke(_graspingState);
        }

        /// <summary>
        /// Return the touch effect of this touchable object
        /// </summary>
        /// <param name="hapticObject"></param>
        /// <returns></returns>
        public WeArtTouchEffect GetEffect(WeArtHapticObject hapticObject)
        {
            WeArtTouchEffect touchEffect = new WeArtTouchEffect();
            touchEffect.Set(Temperature, hapticObject.CalculatePhysicalForce(this), Texture,
                new WeArtTouchEffect.WeArtImpactInfo()
                {
                    Position = hapticObject.transform.position,
                    Time = Time.time
                });

            return touchEffect;
        }

        /// <summary>
        /// The CompareInstanceID.
        /// </summary>
        /// <param name="instanceID">The instanceID<see cref="int"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public bool CompareInstanceID(int instanceID)
        {
            return gameObject.GetInstanceID().Equals(instanceID);
        }

        #region Setters

        /// <summary>
        /// Sets haptic force (stiffness) of this object.
        /// </summary>
        /// <param name="value"></param>
        public void SetHapticForce(float value)
        {
            _stiffness.Value = value;
            UpdateTouchedHaptics();
        }

        /// <summary>
        /// Sets temperature of this object.
        /// </summary>
        /// <param name="value"></param>
        public void SetTemperature(float value)
        {
            _temperature.Value = value;
            UpdateTouchedHaptics();
        }

        /// <summary>
        /// Sets <see cref="TextureType"/> to this object. Other texture's settings remain the same.
        /// </summary>
        /// <param name="typeID"></param>
        public void SetTextureType(TextureType typeID)
        {
            _texture.TextureType = typeID;
            UpdateTouchedHaptics();
        }

        /// <summary>
        /// Sets Texture Velocity of this object. Other texture's settings remain the same.
        /// </summary>
        /// <param name="value"></param>
        public void SetTextureVelocity(float value)
        {
            _texture.Velocity = value;
            UpdateTouchedHaptics();
        }

        /// <summary>
        /// Sets Texture Volume of this object. Other texture's settings remain the same.
        /// </summary>
        /// <param name="value"></param>
        public void SetTextureVolume(float value)
        {
            _texture.Volume = value;
            UpdateTouchedHaptics();
        }

        /// <summary>
        /// Read the initial snap poses placed on this gameobject
        /// </summary>
        private void ReadInitialPoseData()
        {
            Component[] easyGraspPoses = gameObject.GetComponents<WeArtEasyGraspPose>();
            foreach (var item in easyGraspPoses)
            {
                _easyGraspDataList.Add(((WeArtEasyGraspPose)item).GetData());
            }
        }

        /// <summary>
        /// Save the easy grasp pose that was recorded and makes sure it stays persistent when closing playmode
        /// </summary>
        public void SaveEasyGraspPose()
        {
            if (_graspingState == GraspingState.Released)
                return;

            if (_graspingHandController == null)
                return;

            HandSide controllerHandside = _graspingHandController._handSide;

            EasyGraspData data = new EasyGraspData();
            data.handSide = controllerHandside;
            data.graspOriginOffset = transform.InverseTransformPoint(_graspingHandController.GetGraspingSystem()._snapGraspProximityTransform.transform.position);
            data.graspOriginRadius = 0.1f;

            if (_anchoredObject == null)
            {
                data.touchablePosition = transform.localPosition;
                data.touchableRotation = transform.localRotation;
            }
            else
            {
                data.handPosition = _graspingHandController.transform.localPosition;
                data.handRotation = _graspingHandController.transform.localRotation;
            }

            data.thumbClosure = _graspingHandController.FingersMixers[0].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
            data.thumbAbduction = _graspingHandController.FingersMixers[0].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(2);
            data.indexClosure = _graspingHandController.FingersMixers[1].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
            data.middleClosure = _graspingHandController.FingersMixers[2].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
            data.annularClosure = _graspingHandController.FingersMixers[3].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
            data.pinkyClosure = _graspingHandController.FingersMixers[3].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);

            bool foundExistingPose = false;

            foreach (var item in _easyGraspDataList)
            {
                if(item.handSide == controllerHandside)
                {
                    foundExistingPose = true;
                    item.touchablePosition = data.touchablePosition;
                    item.touchableRotation = data.touchableRotation;
                    item.handPosition = data.handPosition;
                    item.handRotation = data.handRotation;
                    item.thumbClosure = data.thumbClosure;
                    item.thumbAbduction = data.thumbAbduction;
                    item.indexClosure = data.indexClosure;
                    item.middleClosure = data.middleClosure;
                    item.annularClosure= data.annularClosure;
                    item.pinkyClosure= data.pinkyClosure;
                    item.graspOriginOffset = data.graspOriginOffset;
                    item.graspOriginRadius = data.graspOriginRadius;
                }
            }

            if(!foundExistingPose)
                _easyGraspDataList.Add(data);

            if( WeArtEasyGraspManager.Instance != null )
            {
                WeArtEasyGraspManager.Instance.AddTouchableToSave(gameObject, _easyGraspDataList.ToList());
#if UNITY_EDITOR
                WeArtEasyGraspManager.Instance.SetEasyGraspPoses(gameObject, _easyGraspDataList.ToList());
#endif
            }
        }

        [Serializable]
        public class EasyGraspData
        {
            public HandSide handSide;
            public Vector3 touchablePosition, handPosition, graspOriginOffset;
            public Quaternion touchableRotation, handRotation;
            public float thumbClosure, indexClosure, middleClosure, annularClosure, pinkyClosure;
            public float thumbAbduction;
            public float graspOriginRadius;
        }

        #endregion

        #endregion

    }
}
