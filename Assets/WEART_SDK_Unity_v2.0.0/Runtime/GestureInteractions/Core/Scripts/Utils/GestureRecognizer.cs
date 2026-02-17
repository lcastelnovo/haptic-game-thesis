using UnityEngine;
using WeArt.Components;
using WeArt.GestureInteractions.Gestures;
using System.Collections.Generic;
using WeArt.Utils;

namespace WeArt.GestureInteractions.Utils
{
    /// <summary>
    /// GestureRecognizer - utility static class that recognizes the gestures.
    /// </summary>
    public static class GestureRecognizer
    {
        private const float RotationSensitivity = 40f;
        private const float ClosureSensitivity = 0.3f;
        private const float AbductionSensitivity = 0.5f;

        private const float AngleModifier = 90f;

        private static readonly Dictionary<GestureName, List<FingerData>> Gestures = new Dictionary<GestureName, List<FingerData>>();
        
        /// <summary>
        /// Checks if the WeArtHandController conforms to the Gesture.
        /// </summary>
        /// <param name="gestureName">Gesture to check.</param>
        /// <param name="handController">WeArtHandController to check</param>
        /// <returns></returns>
        public static bool CheckMatchGesture(GestureName gestureName, WeArtHandController handController)
        {
            if (Gestures.Count < 1) CreateGestureDataDictionary();

            var gesture = GestureProvider.GetGesture(gestureName);
            if (!gesture)
            {
                WeArtLog.Log("NO FOUND SUCH GESTURE SCRIPTABLE OBJECT IN THE PROJECT! PLEASE, CHECK THE GESTURE PROVIDER", LogType.Error);
                return false;
            }

            if (!handController) return false;
            
            if (!CompareHandRotation(gesture, handController)) return false;

            foreach (var fingerData in Gestures[gestureName])
            {
                if (!CompareFingerData(fingerData, handController)) return false;
            }
            
            return true;
        }

        /// <summary>
        /// Debugs the hand data: provides the current hand tracking data and provides the comparing with goal gesture data for each parameter.
        /// </summary>
        /// <param name="gestureName"></param>
        /// <param name="handController"></param>
        /// <returns>String with debug data</returns>
        public static string GetDebugHandData(GestureName gestureName, WeArtHandController handController)
        {
            var gesture = GestureProvider.GetGesture(gestureName);
            
            if (!handController || !gesture) return "No gesture at project or handController at scene";
            
            var handTransform = handController.transform;
            var handEulerAngleZ = NormalizeAngle(handTransform.eulerAngles.z);
            var handForward = handTransform.forward;
            var cameraTransform = Camera.allCameras[0].transform;
            
            var angleX = Vector3.Angle(handForward, cameraTransform.up) - AngleModifier;
            var angleY = Vector3.Angle(handForward, cameraTransform.right) - AngleModifier;
            
            angleX = NormalizeAngle(angleX);
            angleY = NormalizeAngle(angleY);

            var angleXDiff = Mathf.Abs(Mathf.DeltaAngle(angleX, gesture.handRotation.x));
            var angleYDiff = Mathf.Abs(Mathf.DeltaAngle(angleY, gesture.handRotation.y));
            var angleZDiff = Mathf.Abs(Mathf.DeltaAngle(handEulerAngleZ,gesture.handRotation.z));

            string data = "ROTATION\n";
            data += $"Hand AngleX (UP/DOWN): Current {angleX}, Goal: {gesture.handRotation.x}\n";
            data += $"Hand AngleX difference: {angleXDiff}\n";
            data += $"Hand AngleY (LEFT/RIGHT): Current {angleY}, Goal: {gesture.handRotation.y}\n";
            data += $"Hand AngleY difference: {angleYDiff}\n";
            data += $"Hand AngleZ (Rotation): Current {handEulerAngleZ} Goal: {gesture.handRotation.z}\n";
            data += $"Hand AngleZ difference: {angleZDiff}\n";
            data += "CLOSURES\n";
            data += $"Index Closure: {handController._indexThimbleTracking.Closure.Value}\n";
            data += $"Thumb Closure: {handController._thumbThimbleTracking.Closure.Value}\n";
            data += $"Middle Closure: {handController._middleThimbleTracking.Closure.Value}\n";
            data += $"Annular Closure: {handController._annularThimbleTracking.Closure.Value}\n";
            data += $"Pinky Closure: {handController._pinkyThimbleTracking.Closure.Value}\n";

            return data;
        }
        
        
        /// <summary>
        /// Compares HandController rotation with gesture hand rotation.
        /// </summary>
        /// <param name="gesture"></param>
        /// <param name="handController"></param>
        /// <returns></returns>
        private static bool CompareHandRotation(Gesture gesture, WeArtHandController handController)
        {
            if (gesture.ignoreHandRotation) return true;

            var cameraTransform = Camera.allCameras[0].transform;
            var handTransform = handController.transform;
            var handEulerAngleZ = NormalizeAngle(handTransform.eulerAngles.z);
            var handForward = handTransform.forward;
            
            if (!gesture.ignoreXHandRotation)
            {
                var angleX = Vector3.Angle(handForward, cameraTransform.up) - AngleModifier;
                angleX = NormalizeAngle(angleX);
                
                if (Mathf.Abs(Mathf.DeltaAngle(angleX, gesture.handRotation.x)) > RotationSensitivity) return false;
            }

            if (!gesture.ignoreYHandRotation)
            {
                var angleY = Vector3.Angle(handForward, cameraTransform.right) - AngleModifier;
                angleY = NormalizeAngle(angleY);
                
                if (Mathf.Abs(Mathf.DeltaAngle(angleY, gesture.handRotation.y)) > RotationSensitivity) return false;
            }

            if (!gesture.ignoreZHandRotation)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(handEulerAngleZ,gesture.handRotation.z)) > RotationSensitivity) return false;
            }
            
            return true;
        }

        /// <summary>
        /// Compares the data from ThimbleTrackingObject with goal gesture data.
        /// </summary>
        /// <param name="fingerData"></param>
        /// <param name="handController"></param>
        /// <returns></returns>
        private static bool CompareFingerData(FingerData fingerData, WeArtHandController handController)
        {
            if (fingerData.ignore) return true;
            
            var thimbleTracking = handController.GetThimbleTrackingObject(fingerData.actuationPoint);
            
            if (Mathf.Abs(fingerData.abduction - thimbleTracking.Abduction.Value) > AbductionSensitivity) return false;
            if (Mathf.Abs(fingerData.closure - thimbleTracking.Closure.Value) > ClosureSensitivity) return false;

            return true;
        }

        /// <summary>
        /// Normalizes the angle to 360 degrees.
        /// </summary>
        /// <param name="angle">Angle to normalize</param>
        /// <returns>Normalized angle</returns>
        private static float NormalizeAngle(float angle)
        {
            angle %= 360;
            if (angle < 0) angle += 360;
            return angle;
        }
        
        /// <summary>
        /// Creates the buffer gesture finger data list for internal calculations.
        /// </summary>
        /// <param name="gesture"></param>
        /// <returns></returns>
        private static List<FingerData> CreateFingerDataList(Gesture gesture)
        {
            List<FingerData> list = new List<FingerData>
            {
                gesture.thumbData,
                gesture.indexData,
                gesture.middleData,
                gesture.annularData,
                gesture.pinkyData
            };

            return list;
        }

        /// <summary>
        /// Creates the buffer gesture dict with normalized angles for internal calculations.
        /// </summary>
        private static void CreateGestureDataDictionary()
        {
            if (GestureProvider.Instance.gestures.Length < 1)
            {
                WeArtLog.Log("NO GESTURES ARE IN THE PROJECT! PLEASE, CHECK THE GESTURE PROVIDER", LogType.Error);
                return;
            }

            foreach (var gesture in GestureProvider.Instance.gestures)
            {
                gesture.handRotation.x = NormalizeAngle(gesture.handRotation.x);
                gesture.handRotation.y = NormalizeAngle(gesture.handRotation.y);
                gesture.handRotation.z = NormalizeAngle(gesture.handRotation.z);
                Gestures.Add(gesture.gestureName, CreateFingerDataList(gesture));
            }
        }
        
    }
}
