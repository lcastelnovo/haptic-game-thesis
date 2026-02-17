using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System;
using System.Text;
using System.Timers;
using WeArt.Core;
using WeArt.Utils.LogEnums;
using UnityEngine;
using WeArt.Components;

namespace WeArt.Utils
{
    /// <summary>
    /// Utility class used to log events and messages in the <see cref="WeArt"/> framework
    /// </summary>
    public static class WeArtLog
    {
        private static LogLevel _currentProjectLogLevel;
        private static float _saveInterval = 30f;

        private static string Timestamp => $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}";
        private static StringBuilder _log = new StringBuilder();
        private static string _folderPath;
        private static string _filePath;
        
        private static int _pauseEventCounter;
        private static int _pauseEventsToSkip = 2;
        private static bool _isLogging;
        private static int _maxLogFiles = 5;

        private static Timer _timer;
        
        /// <summary>
        /// Starts the logging data.
        /// </summary>
        /// <param name="projectLogLevel">Max Log Level data that will be saved in the file.</param>
        public static void StartLogging(LogLevel projectLogLevel)
        {
            _currentProjectLogLevel = projectLogLevel;
            _folderPath = Path.Combine(Application.persistentDataPath, "LogFiles");

            if (_filePath is null)
            {
                var fileName = $"LogFile_{Timestamp}.txt";
                _filePath = Path.Combine(_folderPath, fileName);
                
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);

                if (!File.Exists(_filePath)) CheckLogFilesQuantity();
            }

            StartPeriodicLogFileSavings();
        }

        /// <summary>
        /// Stops logging data.
        /// </summary>
        public static void StopLogging()
        {
            LogFile(LogLevel.DEBUG, PackTag.EVENTS, "Logging is stopped...");
            SaveLogFile();
            _isLogging = false;
            _timer.Enabled = false;
            _timer.Dispose();
        }

        /// <summary>
        /// Handles the logging state for OnPause Event from Unity Engine.
        /// </summary>
        /// <param name="pauseStatus"></param>
        public static void LogPauseHandler(bool pauseStatus)
        {
            if (_pauseEventCounter < _pauseEventsToSkip)
            {
                _pauseEventCounter++;
                return;
            }
            
            string content = pauseStatus ? $"Application is closing or pausing..." : "Application resumed";
            LogFile(LogLevel.DEBUG, PackTag.EVENTS, content);
            
            if (pauseStatus) SaveLogFile();
        }
        
        /// <summary>
        /// Logs the data to the internal logfile. 
        /// </summary>
        /// <param name="logLevel">Enum of log level.</param> 
        /// <param name="packTag">Enum of packing tag.</param> 
        /// <param name="content">Main content of log.</param>
        /// <param name="deviceID">Device ID that generated the log content. Optional.</param>
        /// <param name="packDescription"> Optional.</param>
        public static void LogFile(LogLevel logLevel, PackTag packTag, string content, DeviceID deviceID = DeviceID.None, PackDescription packDescription = PackDescription.None)
        {
            if (_filePath is null) StartLogging(WeArtController.Instance._projectLogLevel);
            
            if (!IsApplicableLogLevel(logLevel)) return;

            string logDescription = String.Empty;
            string logContent;

            switch (deviceID)
            {
                case DeviceID.None:
                    if (packDescription == PackDescription.None)
                    {
                        logContent = $"[{logLevel}] [{packTag}]  {content}\n\n";
                        break;
                    }
                    
                    logDescription = $"[{logLevel}] [{packTag}]  {packDescription}\n";
                    logContent = $"[{logLevel}] [PACK] {content}\n\n";
                    break;

                default:
                    if (packDescription == PackDescription.None)
                    {
                        logContent = $"[{logLevel}] [{(int)deviceID}] [{packTag}]  {content}\n\n";
                        break;
                    }
                    
                    logDescription = $"[{logLevel}] [{(int)deviceID}] [{packTag}]  {packDescription}\n";
                    logContent = $"[{logLevel}] [{(int)deviceID}] [PACK] {content}\n\n";
                    break;
            }
            
            if (!String.IsNullOrEmpty(logDescription))
            {
                _log.Append($"[{Timestamp}] " + logDescription);
            }
            
            _log.Append($"[{Timestamp}] " + logContent);
            
            Debug.Log($"[WEART] {logDescription + logContent}");
        }
        
        /// <summary>
        /// Periodic saves the log data to the log file at the device.
        /// </summary>
        private static void StartPeriodicLogFileSavings()
        {
            if (_isLogging) return;
            
            _isLogging = true;
            _timer = new Timer(_saveInterval * 1000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            
        }

        /// <summary>
        /// Special method for timer what to do when times is elapsed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            SaveLogFile();
        }
        
        /// <summary>
        /// Saves the logfile to device.
        /// </summary>
        private static void SaveLogFile()
        {
            if (_log.Length < 1) return;
            
            File.AppendAllText(_filePath, _log.ToString());

            _log.Clear();
        }

        /// <summary>
        /// Checks the quantity of saved log files at the device and deletes the eldest file if their quantity is greater than _maxLogFiles.
        /// </summary>
        private static void CheckLogFilesQuantity()
        {
            var files = new DirectoryInfo(_folderPath).GetFiles();

            if (files.Length < _maxLogFiles) return;
            
            var eldestFile = files[0];

            for (int i = 1; i < files.Length; i++)
            {
                if (files[i].CreationTime < eldestFile.CreationTime)
                {
                    eldestFile = files[i];
                }
            }
                    
            File.Delete(eldestFile.FullName);
        }
        
        /// <summary>
        /// Checks the loglevel of new entity to figure out if it can be written to logfile.
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        private static bool IsApplicableLogLevel(LogLevel logLevel)
        {
            return (int)_currentProjectLogLevel >= (int)logLevel;
        } 
        
        /// <summary>Logs a message in the debug console</summary>
        /// <param name="message">The string message</param>
        /// <param name="logType">The kind of log</param>
        /// <param name="onlyInDevelopmentBuild">True if this log should be ignored in normal builds</param>
        /// <param name="callerPath">The path of the caller (optional)</param>
        public static void Log(
            object message,
            LogType logType = LogType.Log,
            bool onlyInDevelopmentBuild = false,
            [CallerFilePath] string callerPath = "")
        {
            if (onlyInDevelopmentBuild && Debug.isDebugBuild)
                return;

            string logMessage = $"{PathToContextString(callerPath)}: {message}";

            if (logType == LogType.Log)
                Debug.Log(logMessage);

            else if (logType == LogType.Warning)
                Debug.LogWarning(logMessage);

            else if (logType == LogType.Error || logType == LogType.Exception)
                Debug.LogError(logMessage);

            else if (logType == LogType.Assert)
                Debug.LogAssertion(logMessage);
        }
        
        private static readonly Dictionary<string, string> PathToContext = new Dictionary<string, string>();

        private static string PathToContextString(string path)
        {
            if (PathToContext.TryGetValue(path, out string context))
                return context;

            context = Path.GetFileNameWithoutExtension(path);
            context = $"<b>[{context}]</b>";

            PathToContext[path] = context;
            return context;
        }
    }
}