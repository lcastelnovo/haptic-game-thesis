using System;
using UnityEngine.Events;
using WeArt.GestureInteractions.Gestures;

namespace WeArt.GestureInteractions.Utils
{
    /// <summary>
    /// GestureEvents — utils class to subscribe on recognition events related to specific Gesture with possibility to set the recognition delay to avoid accidental recognitions.
    /// </summary>
    [Serializable]
    public class GestureEvents
    {
        public GestureName gestureName;
        public TrackingHand trackingHand;
        public float recognitionDelay;
        
        public UnityEvent onRecognized;
        public UnityEvent onStayRecognized;
        public UnityEvent onEndRecognized;
        
        [NonSerialized]
        public float RecognitionTime;
        
        [NonSerialized]
        public bool IsRecognized;
    }

}