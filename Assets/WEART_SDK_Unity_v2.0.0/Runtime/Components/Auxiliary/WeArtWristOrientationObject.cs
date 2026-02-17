using UnityEngine;
using WeArt.Core;
using WeArt.Messages;

namespace WeArt.Components
{
    /// <summary>
    /// This component receives and exposes the wrist orientation data from the hardware
    /// </summary>
    public class WeArtWristOrientationObject : MonoBehaviour
    {
        [SerializeField] internal HandSide _handSide = HandSide.Left;
        [SerializeField] internal Transform _initialRotationTarget;
        [SerializeField] internal bool _freezeXRotation;
        [SerializeField] internal bool _freezeYRotation;
        [SerializeField] internal bool _freezeZRotation;
        
        /// <summary>
        /// The hand side of the wrist
        /// </summary>
        public HandSide HandSide
        {
            get => _handSide;
            set => _handSide = value;
        }

        private Quaternion _initialRotation;
        private Quaternion _handledRotation;
        private Quaternion _currentRotation;

        private bool _receivedFirstMessage;
        
        private void Start()
        {
            _initialRotation = _currentRotation = _initialRotationTarget ? _initialRotationTarget.rotation : transform.rotation;
            Init();
        }

        private void Update()
        {
            UpdateWristOrientation();
        }
        
        private void Init()
        {
            var client = WeArtController.Instance.Client;
            client.OnConnectionStatusChanged -= OnConnectionChanged;
            client.OnConnectionStatusChanged += OnConnectionChanged;
            client.OnMessage -= OnMessageReceived;
            client.OnMessage += OnMessageReceived;

            client.OnMessageResetHandClosure += ResetHandClosure;
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
            //transform.rotation = _initialRotation;
        }

        private void OnMessageReceived(WeArtClient.MessageType type, IWeArtMessage message)
        {
            if (type != WeArtClient.MessageType.MessageReceived)
                return;
            
            if (message is TrackingMessageG2 trackingMessageG2)
            {
                if (trackingMessageG2.HandSide != _handSide) return;

                 HandleReceivedOrientationData(trackingMessageG2.Wrist.Quaternion);
                
                return;
            }
        }
        
        public void ResetHandClosure(bool reset)
        {
            if (!reset) return;
            
            transform.rotation = _initialRotation;
        }

        private void UpdateWristOrientation()
        {
            if (!_receivedFirstMessage) return;
            
            _currentRotation = transform.rotation;
            transform.rotation = _handledRotation;
        }
        
        private void HandleReceivedOrientationData(Quaternion receivedData)
        {
            var rightOrder = new Quaternion(x: receivedData.y, y: receivedData.z, z: receivedData.w, w: receivedData.x);
            
            var x = _freezeXRotation ? _currentRotation.x : -rightOrder.x;
            var y = _freezeYRotation ? _currentRotation.y : -rightOrder.z;
            var z = _freezeZRotation ? _currentRotation.z : -rightOrder.y;

            var unityRotation = new Quaternion(x, y, z, rightOrder.w);
            
            _handledRotation = _initialRotation * unityRotation;

            if (!_receivedFirstMessage) _receivedFirstMessage = true;
        }
        
    }
}