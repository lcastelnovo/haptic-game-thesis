using System;
using UnityEngine;
using WeArt.Utils;
using static UnityEngine.GraphicsBuffer;

namespace WeArt.Components
{
    /// <summary>
    /// This component can be used to follow a specified spatially tracked transform.
    /// Add it to the root object of your avatar virtual hand. Make sure to specify
    /// the right spatial offset between the tracked object and the WeArt device.
    /// </summary>
    public class WeArtDeviceTrackingObject : MonoBehaviour
    {
        /// <summary>
        /// Possible tracking follow behaviours
        /// </summary>
        public enum TrackingUpdateMethod
        {
            TransformUpdate,
            TransformLateUpdate,
            RigidbodyUpdate,
        }

        /// <summary>
        /// Defines the  ghost hand controller component located in the hand
        /// </summary>
        [SerializeField]
        internal WeArtGhostHandController _ghostHandDeviceTracking;

        /// <summary>
        /// Swithc tracking priority from FixedUpdate, Update and LateUpdate
        /// </summary>
        [SerializeField]
        internal TrackingUpdateMethod _updateMethod;

        /// <summary>
        /// Defines the object to follow
        /// </summary>
        [SerializeField]
        internal Transform _trackingSource;

        /// <summary>
        /// Defines the position offset of the hand
        /// </summary>
        [SerializeField]
        internal Vector3 _positionOffset;

        /// <summary>
        /// Defines the rotation offset of the hand
        /// </summary>
        [SerializeField]
        internal Vector3 _rotationOffset;

        /// <summary>
        /// Defines the rigidbody of the hand
        /// </summary>
        [NonSerialized]
        internal Rigidbody _rigidBody;

        /// <summary>
        /// Defines the ghost hand renderer component
        /// </summary>
        [SerializeField] 
        internal Renderer _ghostHandRenderer;

        /// <summary>
        /// Defines if the ghost hand should appear after a certain distance from the physical hand
        /// </summary>
        [SerializeField]
        internal bool _showGhostHand = true;

        /// <summary>
        /// Defines that minimum distance from ghost hand to physical hand required to show the ghost hand
        /// </summary>
        [SerializeField]
        internal float _ghostHandShowDistance = 0.05f;

        /// <summary>
        /// Defines the offset for srewing interactions
        /// </summary>
        [SerializeField]
        internal Transform _handAnchoredOffsetOrigin;

        /// <summary>
        /// Defines the ghost hand offset for srewing interactions
        /// </summary>
        [SerializeField]
        internal Transform _ghostHandAnchoredOffsetOrigin;

        /// <summary>
        /// Defines the hand controller
        /// </summary>
        private WeArtHandController _handController;

        /// <summary>
        /// Defines the invisible material for the ghost hand
        /// </summary>
        private Material _invisibleMaterial;

        /// <summary>
        /// Defines the visible material for the ghost hand
        /// </summary>
        private Material _visibleMaterial;

        /// <summary>
        /// Defines the ghost hand's speed for following the physical hand
        /// </summary>
        private float _handFollowSpeed = 0.3f;

        /// <summary>
        /// Defines the speed multiplier for following the physical hand
        /// </summary>
        private float _handFollowPowerDuringGrab = 3;

        /// <summary>
        /// Defines if the hand is grasping an anchored object
        /// </summary>
        private bool _isAnchored = false;

        private Vector3 _anchoredLocalPosition = new Vector3();
        private Quaternion _anchoredLocalRotation = new Quaternion();

        // Variables for slowly releaseing the hand from an object in order to not reach high velocities
        private float _safeReleaseSeconds = 0;
        private float _maxSafeReleaseSeconds = 0.25f;
        private float _safeReleaseVelocityPower = 0.2f;
        private float _safeReleaseRotationPower = 10f;

        /// <summary>
        /// Property reflecting if the hand is grasping an anchored touchable object
        /// </summary>
        public bool IsAnchored
        {
            get { return _isAnchored;}
            set 
            { 
                _isAnchored = value;
                _anchoredLocalPosition = transform.localPosition;
                _anchoredLocalRotation = transform.localRotation;
            }
        }

        /// <summary>
        /// The method to use in order to update the position and the rotation of this device
        /// </summary>
        public TrackingUpdateMethod UpdateMethod
        {
            get => _updateMethod;
            set => _updateMethod = value;
        }

        /// <summary>
        /// The transform attached to the tracked device object
        /// </summary>
        public Transform TrackingSource
        {
            get => _trackingSource;
            set => _trackingSource = value;
        }

        /// <summary>
        /// The position offset between this device and the tracked one
        /// </summary>
        public Vector3 PositionOffset
        {
            get => _positionOffset;
            set => _positionOffset = value;
        }

        /// <summary>
        /// The rotation offset between this device and the tracked one
        /// </summary>
        public Vector3 RotationOffset
        {
            get => _rotationOffset;
            set => _rotationOffset = value;
        }

        /// <summary>
        /// Set up
        /// </summary>
        private void Awake()
        {
                // Find ghost hand
                int childrenCount = transform.childCount;
                for (int i = 0; i < childrenCount; ++i)
                {
                    if (transform.GetChild(i).GetComponent<WeArtGhostHandController>() != null)
                    {
                        _ghostHandDeviceTracking = transform.GetChild(i).GetComponent<WeArtGhostHandController>();
                        break;
                    }
                }

                // Find ghost hand renderer
                childrenCount = _ghostHandDeviceTracking.transform.childCount;
                for (int i = 0; i < _ghostHandDeviceTracking.transform.childCount; ++i)
                {
                    if (_ghostHandDeviceTracking.transform.GetChild(i).GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        _ghostHandRenderer = _ghostHandDeviceTracking.transform.GetChild(i).GetComponent<SkinnedMeshRenderer>();
                        break;
                    }
                }
        }

        private void Start()
        {
            _handController = GetComponent<WeArtHandController>();
        }

        private void Update()
        {
           if(_safeReleaseSeconds > 0)
            {
                _safeReleaseSeconds -= Time.deltaTime;
            }

            if (UpdateMethod == TrackingUpdateMethod.TransformUpdate)
                UpdateHands();
        }

        private void LateUpdate()
        {
            if (UpdateMethod == TrackingUpdateMethod.TransformLateUpdate)
                UpdateHands();
        }

        private void FixedUpdate()
        {
            if (UpdateMethod == TrackingUpdateMethod.RigidbodyUpdate)
                UpdateHands();

            UpdateRigidbody();
        }

        /// <summary>
        /// Updated the ghost hands real position and rotation in the real world
        /// </summary>
        private void UpdateHands()
        {
            _ghostHandDeviceTracking.transform.position = TrackingSource.TransformPoint(_positionOffset);
            _ghostHandDeviceTracking.transform.rotation = TrackingSource.rotation * Quaternion.Euler(_rotationOffset);

            if (_showGhostHand)
            {
                float distance = Vector3.Distance(_ghostHandDeviceTracking.transform.position, transform.position);

                if (distance > _ghostHandShowDistance)
                {
                    _ghostHandRenderer.sharedMaterial = _handController.GetGhostHandTransparentMaterial();
                }
                else
                {
                    _ghostHandRenderer.sharedMaterial = _handController.GetGhostHandInvisibleMaterial();
                }
            }
        }

        /// <summary>
        /// Teleports the hand directly to the real controller position without intermolation
        /// </summary>
        public void TeleportHandToTrackingSource()
        {
            transform.position = TrackingSource.TransformPoint(_positionOffset);
            transform.rotation = TrackingSource.rotation * Quaternion.Euler(_rotationOffset);
        }

        /// <summary>
        /// Applies velocity to the visible hand to track the position and rotation of the ghost hand
        /// </summary>
        private void UpdateRigidbody()
        {
            if (TrackingSource == null)
                return;

            // Apply force to the grasped object instead of the hand when the object is anchored
            if (_isAnchored)
            {
                if (_handController.GetGraspingSystem().GetGraspedObject().GetComponent<ConfigurableJoint>() != null)
                {
                    _handController.GetGraspingSystem().GetGraspedObject().GetComponent<Rigidbody>().velocity = 
                        (TrackingSource.TransformPoint(_positionOffset) - transform.position) / Time.fixedDeltaTime * _handController.GetGraspingSystem().GetGraspedObject().AnchoredObject.GraspingVelocity;
                }
                else
                {
                    if (_handController.GetGraspingSystem().GetGraspedObject().AnchoredObject.IsUsingScrewingOrigin)
                    {
                        _handController.GetGraspingSystem().GetGraspedObject().GetComponent<Rigidbody>().AddForceAtPosition(
                                (_ghostHandAnchoredOffsetOrigin.position - _handAnchoredOffsetOrigin.position) * _handFollowPowerDuringGrab * _handController.GetGraspingSystem().GetGraspedObject().AnchoredObject.GraspingVelocity,
                                _handAnchoredOffsetOrigin.position, ForceMode.VelocityChange);
                    }
                    else
                    {
                        _handController.GetGraspingSystem().GetGraspedObject().GetComponent<Rigidbody>().AddForceAtPosition(
                                (TrackingSource.TransformPoint(_positionOffset) - transform.position) * _handFollowPowerDuringGrab * _handController.GetGraspingSystem().GetGraspedObject().AnchoredObject.GraspingVelocity,
                                transform.position, ForceMode.VelocityChange);
                    }
                }
                return;
            }

            // Get Rigid Body
            if (_rigidBody == null)
            {
                TryGetComponent<Rigidbody>(out Rigidbody rb);
                _rigidBody = rb;
                if (_rigidBody == null)
                {
                    WeArtLog.Log($"Cannot use {nameof(TrackingUpdateMethod.RigidbodyUpdate)} method without a {nameof(Rigidbody)}", LogType.Warning);
                    return;
                }
            }

            // Velocity and Rotation
            _rigidBody.velocity = (TrackingSource.TransformPoint(_positionOffset) - transform.position) / Time.fixedDeltaTime * (_safeReleaseSeconds>0? _safeReleaseVelocityPower:_handFollowSpeed);

            if (_handController.GetGraspingSystem().GraspingState == Core.GraspingState.Grabbed)
            {
                _rigidBody.velocity = _handFollowPowerDuringGrab * _rigidBody.velocity;

                if (_rigidBody.velocity.magnitude > 3)
                {
                    _rigidBody.velocity = _rigidBody.velocity.normalized * 3;
                }
            }
            else
            {

            }

            // Absolute rotation used when not grasping
            if (!_isAnchored)
            {
                if(_safeReleaseSeconds > 0)
                {
                    transform.rotation = Quaternion.Lerp(transform.rotation, TrackingSource.rotation * Quaternion.Euler(_rotationOffset), Time.deltaTime * _safeReleaseRotationPower);
                }
                else
                {
                transform.rotation = TrackingSource.rotation * Quaternion.Euler(_rotationOffset);
                }
            }
        }

        /// <summary>
        /// Starts the velocity restriction timer
        /// </summary>
        public void StartSafeRelease()
        {
            _safeReleaseSeconds = _maxSafeReleaseSeconds;
        }
    }
}