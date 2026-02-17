using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using WeArt.Messages;
using WeArt.Utils;
using static WEART.Communication;
using static WEART.WeartLogger;
using PackTag = WeArt.Utils.LogEnums.PackTag;
using LogLevel = WeArt.Utils.LogEnums.LogLevel;
using PackDescription = WeArt.Utils.LogEnums.PackDescription;
using System.Text;
using System.Threading.Tasks;
using WeArt.Components;

namespace WeArt.Core 
{
    #region WeArtClient

    /// <summary>
    /// Client class for comunicating between SDK and PC Middleware
    /// </summary>
    public class WeArtClient
    {
        /// <summary>
        /// Possible message types
        /// </summary>
        ///[JsonConverter(typeof(JsonStringEnumConverter))]
        public enum MessageType
        {
            MessageSent, MessageReceived
        }

        /// <summary>
        /// Possible error types
        /// </summary>
        public enum ErrorType
        {
            ConnectionError, SendMessageError, ReceiveMessageError
        }


        // Threading
        public CancellationTokenSource _cancellation;
        public readonly SynchronizationContext _mainThreadContext = SynchronizationContext.Current;

        // Networking
        public string _ipAddress;
        public int _port;
        public Socket _socket;

        // Messaging
        public static readonly char _messagesSeparator = '~';
        public readonly WeArtMessageSerializer _messageSerializer = new WeArtMessageCustomSerializer();
        public byte[] _messageReceivedBuffer = new byte[1024];
        public string _trailingText = string.Empty;


        /// <summary>
        /// Called when the connection has been established (true) and when it is closed (false)
        /// </summary>
        public event Action<bool> OnConnectionStatusChanged;

        /// <summary>
        /// Called when a <see cref="IWeArtMessage"/> is sent or received
        /// </summary>
        public event Action<MessageType, IWeArtMessage> OnMessage;

        /// <summary>
        /// Called when a <see cref="IWeArtMessage"/> is serialized or deserialized
        /// </summary>
        public event Action<MessageType, string> OnTextMessage;

        /// <summary>
        /// Called when an error occurs
        /// </summary>
        public event Action<ErrorType, Exception> OnError;

        /// <summary>
        /// Called to reset the hand closures after connections or disconnections
        /// </summary>
        public event Action<bool> OnMessageResetHandClosure;


        /// <summary>
        /// True if a connection to the middleware has been established
        /// </summary>
        public bool IsConnected => _socket != null && _socket.Connected;


        /// <summary>
        /// The IP address of the middleware network endpoint
        /// </summary>
        public string IpAddress
        {
            get => _ipAddress;
            set => _ipAddress = value;
        }

        /// <summary>
        /// The port of the middleware network endpoint
        /// </summary>
        public int Port
        {
            get => _port;
            set => _port = value;
        }

        /// <summary>
        /// Stops the middleware and the established connection
        /// </summary>
        public void Stop()
        {
            SendMessage(new StopFromClientMessage());
            StopConnection();
        }

        public void SendStartDevice(TrackingType trackingType)
        {
            // Send the request to start
            SendMessage(new StartFromClientMessage { TrackingType = trackingType });
        }

        /// <summary>
        /// Start Calibration Finger Tracking algorithm process
        /// </summary>
        public void StartCalibration()
        {
            SendMessage(new StartCalibrationMessage());
        }

        /// <summary>
        /// Stop and cancel the Calibration of Finger Tracking algorithm process
        /// </summary>
        public void StopCalibration()
        {
            SendMessage(new StopCalibrationMessage());
        }

        /// <summary>
        /// Reset calibration procedure sent to tracking algorithm
        /// </summary>
        public void ResetCalibration()
        {
            SendMessage(new ResetCalibrationMessage());
        }

        /// <summary>
        /// Asks the middleware to start sending raw data events to the sdk
        /// </summary>
        public void StartRawData()
        {
            SendMessage(new RawDataOnMessage());
        }

        /// <summary>
        /// Tells the middleware to stop sending raw data events
        /// </summary>
        public void StopRawData()
        {
            SendMessage(new RawDataOffMessage());
        }


        /// <summary>
        /// Sends a message to the middleware
        /// </summary>
        /// <param name="message">The message</param>
        public virtual void SendMessage(IWeArtMessage message)
        {
            if (IsConnected)
            {
                try
                {
                    _messageSerializer.Serialize(message, out string text);
                    text += _messagesSeparator;
                    _messageSerializer.Serialize(text, out byte[] bytes);
                    _socket.Send(bytes);
                    OnMessageHandler(MessageType.MessageSent, message);
                    OnTextMessageHandler(MessageType.MessageSent, text);
                }
                catch (Exception e)
                {
                    OnErrorHandler(ErrorType.SendMessageError, e);
                }
            }
        }

        public void ResetHandClosure ()
        {
            WeArtController.Instance.IsCalibrated = false;
            OnMessageResetHandClosure?.Invoke(true);
        }

        /// <summary>
        /// Called internally to parse eventual incoming messages
        /// </summary>
        /// <param name="messages">Correctly parsed messages</param>
        /// <returns>True if at least one message has been received</returns>
        public bool ReceiveMessages(out IWeArtMessage[] messages)
        {
            if (IsConnected)
            {
                try
                {
                    int numBytes = _socket.Receive(_messageReceivedBuffer);
                    if (numBytes > 0)
                    {
                        _messageSerializer.Deserialize(_messageReceivedBuffer, numBytes, out string bufferText);

                        bufferText = _trailingText + bufferText;
                        int lastSeparatorIndex = bufferText.LastIndexOf(_messagesSeparator);

                        if (lastSeparatorIndex < 0)
                        {
                            _trailingText = bufferText;
                            messages = null;
                            return false;
                        }
                        
                        string text = bufferText.Substring(0, lastSeparatorIndex);
                        _trailingText = bufferText.Substring(lastSeparatorIndex + 1);

                        if (string.IsNullOrEmpty(text))
                        {
                            messages = null;
                            return false;
                        }

                        string[] split = text.Split(_messagesSeparator);
                        messages = new IWeArtMessage[split.Length];
                        for (int i = 0; i < messages.Length; i++)
                        {
                            _messageSerializer.Deserialize(split[i], out messages[i]);
                            OnMessageHandler(MessageType.MessageReceived, messages[i]);
                            OnTextMessageHandler(MessageType.MessageReceived, split[i]);
                        }
                        return true;
                    }
                }
                catch (SocketException e)
                {
                    // Raise error only if socket was closed/error from outside
                    if (!_cancellation.IsCancellationRequested)
                    {
                        WeArtLog.Log($"Socket error (\"{e.SocketErrorCode}\"), closing connection", LogType.Error);
                        OnErrorHandler(ErrorType.ReceiveMessageError, e);
                        StopConnection();
                    }
                }
                catch (Exception e)
                {
                    if (IsConnected)
                        OnErrorHandler(ErrorType.ReceiveMessageError, e);
                }
            }

            messages = null;
            return false;
        }

        /// <summary>
        /// Called internally to stop the connection if the middleware requests it
        /// </summary>
        /// <param name="msg">A received message</param>
        public void OnMessageReceived(IWeArtMessage msg)
        {
            if (msg is ExitMessage ||
                msg is DisconnectMessage)
            {
                Stop();
            }
        }

        /// <summary>
        /// Called internally to stop the connection task and the socket
        /// </summary>
        public void StopConnection()
        {
            _cancellation?.Cancel();
            _socket?.Close();
        }

        /// <summary>
        /// Socket management for receiving messages from Middleware
        /// </summary>
        public virtual void Start()
        {
            _cancellation = new CancellationTokenSource();
            Task.Run(() =>
            {
                // Connection loop
                while (!_cancellation.IsCancellationRequested)
                {
                    try
                    {
                        // Create the socket
                        IPAddress ipAddr = IPAddress.Parse(_ipAddress);

                        IPEndPoint localEndPoint = new IPEndPoint(ipAddr, _port);
                        _socket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                        // Connect to it
                        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                        _socket.Connect(localEndPoint);

                        _mainThreadContext.Post(state => OnConnectionStatusChanged?.Invoke(IsConnected), this);
                    }
                    catch (Exception e)
                    {
                        OnErrorHandler(ErrorType.ConnectionError, e);
                        StopConnection();
                    }

                    // Prova 

                    // Message receiving loop
                    while (!_cancellation.IsCancellationRequested)
                    {
                        if (ReceiveMessages(out var messages))
                            foreach (var message in messages)
                                OnMessageReceived(message);
                    }
                }

                // Connection stop
                StopConnection();
                _mainThreadContext.Post(state => OnConnectionStatusChanged?.Invoke(false), this);

            }, _cancellation.Token);
        }


        /// <summary>
        /// Method to trigger the OnError event
        /// </summary>
        /// <param name="errorType"></param>
        /// <param name="e"></param>
        protected virtual void OnErrorHandler(ErrorType errorType, Exception e)
        {
            OnError?.Invoke(errorType, e);
        }

        /// <summary>
        /// Method to trigger the OnMessage event
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="weArtMessage"></param>
        protected virtual void OnMessageHandler(MessageType messageType, IWeArtMessage weArtMessage)
        {
            OnMessage?.Invoke(messageType, weArtMessage);
        }

        /// <summary>
        /// Method to trigger the OnMessage event
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="message"></param>
        protected virtual void OnTextMessageHandler(MessageType messageType, string message)
        {
            OnTextMessage?.Invoke(messageType, message);
        }
    }
    #endregion

    #region WeArtClientBLE

    /// <summary>
    /// BLE Client class implementations
    /// </summary>
    public class WeArtClientBLE : WeArtClient
    {
        public override void Start()
        {
            try
            {
                if (_weartCommunication != null)
                {
                    _weartCommunication.MessageReceivedFromMiddleware += OnReceivedMessages;

                }
            }
            catch (Exception e)
            {
                OnErrorHandler(ErrorType.ConnectionError, e);
            }

            //Register callback to BLE stack communication to read data
            LogInfoReady += LogFromMiddleware;
        }


        /// <summary>
        /// Callback for messages received from Middleware
        /// </summary>
        /// <param name="messageString"></param>
        private void OnReceivedMessages(string messageString)
        {
            if (ReceiveMessagesDLL(messageString, out IWeArtMessage[] messagesWEART))
            {
                foreach (var message in messagesWEART)
                    OnMessageReceived(message);
            }
        }


        /// <summary>
        /// Sends a message to the middleware
        /// </summary>
        /// <param name="message">The message</param>
        public override void SendMessage(IWeArtMessage message)
        {
            try
            {
                _messageSerializer.Serialize(message, out string text);
                text += _messagesSeparator;
                _messageSerializer.Serialize(text, out byte[] bytes);

                if (_weartCommunication != null)
                {
                    _weartCommunication.SendFromSDKToMiddleware(bytes);
                }
                OnMessageHandler(MessageType.MessageSent, message);

                OnTextMessageHandler(MessageType.MessageSent, text);
            }
            catch (Exception e)
            {
                OnErrorHandler(ErrorType.SendMessageError, e);
            }
        }

        /// <summary>
        /// Called internally to parse eventual incoming messages
        /// </summary>
        /// <param name="messages">Correctly parsed messages</param>
        /// <returns>True if at least one message has been received</returns>
        private bool ReceiveMessagesDLL(string messageArrived, out IWeArtMessage[] messages)
        {
            try
            {
                _messageReceivedBuffer = Encoding.UTF8.GetBytes(messageArrived);
                int numBytes = _messageReceivedBuffer.Length;
                if (numBytes > 0)
                {
                    _messageSerializer.Deserialize(_messageReceivedBuffer, numBytes, out string bufferText);

                    bufferText = _trailingText + bufferText;
                    int lastSeparatorIndex = bufferText.LastIndexOf(_messagesSeparator);
                    string text = bufferText.Substring(0, lastSeparatorIndex);

                    _trailingText = bufferText.Substring(lastSeparatorIndex + 1);

                    if (string.IsNullOrEmpty(text))
                    {
                        messages = null;
                        return false;
                    }

                    string[] split = text.Split(_messagesSeparator);

                    messages = new IWeArtMessage[split.Length];
                    for (int i = 0; i < messages.Length; i++)
                    {
                        if (_messageSerializer.Deserialize(split[i], out messages[i])) 
                        {                        
                            OnMessageHandler(MessageType.MessageReceived, messages[i]);
                            OnTextMessageHandler(MessageType.MessageReceived, split[i]);
                        }
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                if (IsConnected)
                    OnErrorHandler(ErrorType.ReceiveMessageError, e);
            }
            messages = null;
            return false;
        }

        /// <summary>
        /// Event from middleware DLL for logging
        /// </summary>
        /// <param name="logInformation"></param>
        private static void LogFromMiddleware(LogInformation logInformation)
        {
            WeArtLog.LogFile((LogLevel)logInformation.LogLevel, (PackTag)logInformation.PackTag, logInformation.Content, (DeviceID)logInformation.DeviceID, (PackDescription)logInformation.PackDescription);
        }
    }
    #endregion
}