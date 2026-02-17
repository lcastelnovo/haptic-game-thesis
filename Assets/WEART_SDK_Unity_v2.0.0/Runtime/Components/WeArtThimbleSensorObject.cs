using System;
using UnityEngine;
using WeArt.Core;
using WeArt.Messages;

namespace WeArt.Components
{
    /// <summary>
    /// This components receives and exposes raw sensors data from the thimble hardware
    /// </summary>
    public class WeArtThimbleSensorObject : MonoBehaviour
    {
        [SerializeField]
        internal HandSide _handSide = HandSide.Left;

        [SerializeField]
        internal ActuationPoint _actuationPoint = ActuationPoint.Thumb;

        /// <summary>
        /// The hand side of the thimble
        /// </summary>
        public HandSide HandSide
        {
            get => _handSide;
            set { _handSide = value; ResetValues(); }
        }

        /// <summary>
        /// The actuation point of the thimble
        /// </summary>
        public ActuationPoint ActuationPoint
        {
            get => _actuationPoint;
            set { _actuationPoint = value; ResetValues(); }
        }

        /// <summary>
        /// Time of the last data sample received from this thimble
        /// </summary>
        public DateTime LastSampleTime { get; private set; }

        /// <summary>
        /// Accelerometer values (X,Y,Z) of the thimble
        /// </summary>
        public Vector3 Accelerometer { get; private set; }

        /// <summary>
        /// Gyroscope values (X,Y,Z) of the thimble
        /// </summary>
        public Vector3 Gyroscope { get; private set; }

        /// <summary>
        /// Distance (between 0 and 255) registered by the ToF sensor of the thimble
        /// </summary>
        public int TimeOfFlightDistance { get; private set; }

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gameObject.scene.IsValid())
                Init();
        }
#endif

        internal void OnConnectionChanged(bool connected)
        {
            ResetValues();
        }

        private void ResetValues()
        {
            LastSampleTime = default(DateTime);
            Accelerometer = new Vector3(0, 0, 0);
            Gyroscope = new Vector3(0, 0, 0);
            TimeOfFlightDistance = 0;
        }

        private void OnMessageReceived(WeArtClient.MessageType type, IWeArtMessage message)
        {
            if (type != WeArtClient.MessageType.MessageReceived)
                return;

            if (message is RawDataMessage rawDataMessage)
                OnTDRawDataReceived(rawDataMessage);

            if (message is RawDataTouchDiverPro rawDataMessageTDPro)
                OnTDProRawDataReceived(rawDataMessageTDPro);
        }

        private void OnTDRawDataReceived(RawDataMessage message)
        {
            if (message.HandSide != _handSide)
                return;

            SensorData data = GetTDSensorData(message);

            Accelerometer = new Vector3(data.Accelerometer.X, data.Accelerometer.Y, data.Accelerometer.Z);
            Gyroscope = new Vector3(data.Gyroscope.X, data.Gyroscope.Y, data.Gyroscope.Z);
            TimeOfFlightDistance = data.TimeOfFlight.Distance;
            LastSampleTime = message.Timestamp;
        }

        private void OnTDProRawDataReceived(RawDataTouchDiverPro message)
        {
            if (message.HandSide != _handSide)
                return;

            SensorDataG2 data = GetTDProSensorData(message);

            Accelerometer = new Vector3(data.Accelerometer.X, data.Accelerometer.Y, data.Accelerometer.Z);
            Gyroscope = new Vector3(data.Gyroscope.X, data.Gyroscope.Y, data.Gyroscope.Z);
            TimeOfFlightDistance = 0; // values not available on TDPro
            LastSampleTime = message.Timestamp;
        }

        private SensorData GetTDSensorData(RawDataMessage rawDataMessage)
        {
            switch(_actuationPoint)
            {
                case ActuationPoint.Index: return rawDataMessage.Index;
                case ActuationPoint.Thumb: return rawDataMessage.Thumb;
                case ActuationPoint.Middle: return rawDataMessage.Middle;
                case ActuationPoint.Palm: return rawDataMessage.Palm;
            }
            return new SensorData { };
        }

        private SensorDataG2 GetTDProSensorData(RawDataTouchDiverPro rawDataMessage)
        {
            switch (_actuationPoint)
            {
                case ActuationPoint.Index: return rawDataMessage.Index;
                case ActuationPoint.Thumb: return rawDataMessage.Thumb;
                case ActuationPoint.Middle: return rawDataMessage.Middle;
                case ActuationPoint.Annular: return rawDataMessage.Annular;
                case ActuationPoint.Pinky: return rawDataMessage.Pinky;
                case ActuationPoint.Palm: return rawDataMessage.Palm;
            }
            return new SensorDataG2 { };
        }
    }
}