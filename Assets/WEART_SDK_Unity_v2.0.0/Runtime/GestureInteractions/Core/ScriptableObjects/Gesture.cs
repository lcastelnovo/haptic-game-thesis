using UnityEngine;
using WeArt.Core;
using WeArt.GestureInteractions.Utils;

namespace WeArt.GestureInteractions.Gestures
{
    /// <summary>
    /// Gesture — scriptable object to to set and keep the gesture data to be recognized runtime.
    /// Each new gesture has to have the unique name that has to be added to Gesture name enum.
    /// </summary>
    [CreateAssetMenu(fileName = "NewHandGesture", menuName = "ScriptableObjects/WeArtGestures/HandGesture", order = 1)]
    public class Gesture : ScriptableObject
    {
        public GestureName gestureName;
        public FingerData thumbData = new FingerData(ActuationPoint.Thumb);
        public FingerData indexData = new FingerData(ActuationPoint.Index);
        public FingerData middleData = new FingerData(ActuationPoint.Middle);
        public FingerData annularData = new FingerData(ActuationPoint.Annular);
        public FingerData pinkyData = new FingerData(ActuationPoint.Pinky);
        public bool ignoreHandRotation;
        public bool ignoreXHandRotation;
        public bool ignoreYHandRotation;
        public bool ignoreZHandRotation;
        public Vector3 handRotation;
    }
    
}