using System.Collections;
using UnityEngine;
using WeArt.Core;
using WeArt.Messages;
using UnityEngine.Events;
using System.Collections.Generic;

namespace WeArt.Components
{
    public class WeArtTrackingCalibration : MonoBehaviour
    {
        [System.Serializable]
        public class TrackingEvent : UnityEvent<HandSide> { }

        // Events
        [Header("Tracking Events")]

        // Unity Events
        [SerializeField]
        internal TrackingEvent _OnCalibrationStart;
        [SerializeField]
        internal TrackingEvent _OnCalibrationFinish;
        [SerializeField]
        internal TrackingEvent _OnCalibrationResultSuccess;
        [SerializeField]
        internal TrackingEvent _OnCalibrationResultFail;

        private Queue<IWeArtMessage> _messagesQueue = new Queue<IWeArtMessage>();
        private bool _handlingQueue;
        
        private void Init()
        {
            var client = WeArtController.Instance.Client;
            client.OnConnectionStatusChanged -= OnConnectionChanged;
            client.OnConnectionStatusChanged += OnConnectionChanged;
            client.OnMessage -= OnMessageReceived;
            client.OnMessage += OnMessageReceived;
        }

        private void OnEnable()
        {
            Init();
        }

        private void Update()
        {
            if (_messagesQueue.Count > 0 && !_handlingQueue)
            {
                StartCoroutine(MessageQueueHandler());
            }
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

        }

        private void HandleTrackingCalibrationStatus(TrackingCalibrationStatus status)
        {
            switch (status.Status)
            {
                case Core.CalibrationStatus.Calibrating:
                    _OnCalibrationStart?.Invoke(status.HandSide);
                    break;
                    
                case Core.CalibrationStatus.Running:
                    _OnCalibrationFinish?.Invoke(status.HandSide);
                    break;
            }
        }

        private void HandleTrackingCalibrationResult(TrackingCalibrationResult result)
        {
            if (result.Success)
            {
                _OnCalibrationResultSuccess?.Invoke(result.HandSide);
                return;
            }
            
            _OnCalibrationResultFail?.Invoke(result.HandSide);
            
        }

        private void OnMessageReceived(WeArtClient.MessageType type, IWeArtMessage message)
        {
            if (type != WeArtClient.MessageType.MessageReceived) return;
            if (message is TrackingCalibrationStatus || message is TrackingCalibrationResult)
            {
                _messagesQueue.Enqueue(message);
            }
        }

        private IEnumerator MessageQueueHandler()
        {
            _handlingQueue = true;
            
            while (_messagesQueue.Count > 0)
            {
                MessageHandler(_messagesQueue.Dequeue());
                
                yield return null;
            }

            _handlingQueue = false;
        }
        
        private void MessageHandler(IWeArtMessage message)
        {
            if (message is TrackingCalibrationStatus trackingCalibrationStatus)
            {
                HandleTrackingCalibrationStatus(trackingCalibrationStatus);
                return;
            }

            if (message is TrackingCalibrationResult trackingCalibrationResult)
            {
                HandleTrackingCalibrationResult(trackingCalibrationResult);
            }     
        }
    }
}
