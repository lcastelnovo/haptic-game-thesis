using System;
using UnityEngine;
using WeArt.Core;
using WeArt.Messages;

namespace WeArt.Components
{
    /// <summary>
    /// This component receives and exposes tracking data from the hardware
    /// </summary>
    public class WeArtThimbleTrackingObject : MonoBehaviour
    {
        [SerializeField]
        internal HandSide _handSide = HandSide.Left;

        [SerializeField]
        internal ActuationPoint _actuationPoint = ActuationPoint.Thumb;

        private bool _isGrasping;
        private bool _isBlocked;
        private float _blockedClosureValue;
        private float _safeUnblockSeconds = 0;
        private WeArtTouchableObject _graspedObject;

        /// <summary>
        /// Check the touchable object that was grasped
        /// </summary>
        public WeArtTouchableObject GraspedObject
        {
            get => _graspedObject;
            set => _graspedObject = value;
        }

        /// <summary>
        /// Check if the finger is grasping
        /// </summary>
        public bool IsGrasping
        {
            get => _isGrasping; 
            set => _isGrasping = value;
        }

        /// <summary>
        /// Smooth transition frames after unlock
        /// </summary>
        public float SafeUnblockSeconds
        {
            get => _safeUnblockSeconds;
            set => _safeUnblockSeconds = value;
        }

        /// <summary>
        /// The value where the finger stopped.
        /// </summary>
        public float BlockedClosureValue
        {
            get => _blockedClosureValue;
            set => _blockedClosureValue= value;
        }

        /// <summary>
        /// Is the finger blocked by an object
        /// </summary>
        public bool IsBlocked 
        { 
            get => _isBlocked;
            set => _isBlocked = value;
        }

        /// <summary>
        /// The hand side of the thimble
        /// </summary>
        public HandSide HandSide
        {
            get => _handSide;
            set => _handSide = value;
        }

        /// <summary>
        /// The actuation point of the thimble
        /// </summary>
        public ActuationPoint ActuationPoint
        {
            get => _actuationPoint;
            set => _actuationPoint = value;
        }

        /// <summary>
        /// The closure measure received from the hardware
        /// </summary>
        public Closure Closure
        {
            get;
            private set;
        }

        /// <summary>
        /// The abduction measure received from the hardware (if any)
        /// </summary>
        public Abduction Abduction { get; private set; }

        private void Init()
        {
            var client = WeArtController.Instance.Client;
            client.OnConnectionStatusChanged -= OnConnectionChanged;
            client.OnConnectionStatusChanged += OnConnectionChanged;
            client.OnMessage -= OnMessageReceived;
            client.OnMessage += OnMessageReceived;

            client.OnMessageResetHandClosure += ResetHandClosure;
        }

        private void OnEnable()
        {
            Init();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gameObject.scene.IsValid())
                Init();
        }
#endif

        internal void OnConnectionChanged(bool connected)
        {
            Closure = new Closure() { Value = 0f };
            Abduction = new Abduction() { Value = WeArtConstants.defaultAbduction };
        }

        private void OnMessageReceived(WeArtClient.MessageType type, IWeArtMessage message)
        {
            if (type != WeArtClient.MessageType.MessageReceived)
                return;

            if (message is TrackingMessage trackingMessage)
            {
                Closure = trackingMessage.GetClosure(HandSide, ActuationPoint);
                Abduction = trackingMessage.GetAbduction(HandSide, ActuationPoint);
                return;
            }

            if (message is TrackingMessageG2 trackingMessageG2)
            {
                if (trackingMessageG2.HandSide != _handSide) return;

                ThimbleData thimbleData = GetThimbleData(trackingMessageG2);

                Closure = new Closure { Value = thimbleData.Closure };
                Abduction = new Abduction { Value = thimbleData.Abduction };
                return;
            }
        }

        private ThimbleData GetThimbleData(TrackingMessageG2 trackingMessageG2)
        {
            switch (_actuationPoint)
            {
                case ActuationPoint.Thumb:
                    return trackingMessageG2.Thumb;
                case ActuationPoint.Index:
                    return trackingMessageG2.Index;
                case ActuationPoint.Middle:
                    return trackingMessageG2.Middle;
                case ActuationPoint.Annular:
                    return trackingMessageG2.Annular;
                case ActuationPoint.Pinky:
                    return trackingMessageG2.Pinky;
                case ActuationPoint.Palm:
                    return trackingMessageG2.Palm;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_actuationPoint));
            }
        }

        public void ResetHandClosure(bool reset)
        {
            if (reset)
            {
                Closure = new Closure() { Value = 0f };
                Abduction = new Abduction() { Value = WeArtConstants.defaultAbduction };
            }
        }
    }
}