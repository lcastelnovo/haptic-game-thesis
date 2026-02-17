using System;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using WeArt.GestureInteractions.Utils;

namespace WeArt.GestureInteractions.Gestures
{
    /// <summary>
    /// WeArtGestureManager — allows to subscribe the events OnGestureRecognition, OnStayRecognition and OnEndRecognition.
    /// </summary>
    public class WeArtGestureManager : MonoBehaviour
    {
        [SerializeField] private List<GestureEvents> trackingGestures = new List<GestureEvents>();

        private WeArtHandController _leftHand;
        private WeArtHandController _rightHand;
        
        private void Start()
        {
            WeArtController.Instance.GetComponent<WeArtTrackingCalibration>()._OnCalibrationResultSuccess.AddListener(SetHandController);
        }
        
        private void Update()
        {
            TrackGestures();
        }

        /// <summary>
        /// Sets WEART hand controller to recognize the gestures.
        /// </summary>
        /// <param name="handSide"></param>
        private void SetHandController(HandSide handSide)
        {
            if (handSide == HandSide.Left) _leftHand = WeArtController.Instance.GetHandController(handSide);
            if (handSide == HandSide.Right) _rightHand = WeArtController.Instance.GetHandController(handSide);
        }
        
        /// <summary>
        /// Checks all set gestures if they conform to the hand tracking data.
        /// </summary>
        private void TrackGestures()
        {
            if (!WeArtController.Instance._allowGestures) return;
            if (!_leftHand && !_rightHand) return;
            
            foreach (var gesture in trackingGestures)
            {
                CheckGesture(gesture);
            }
        }

        /// <summary>
        /// Checks the gesture if the tracking data of set hand conform to the gesture.
        /// </summary>
        /// <param name="gestureEvent"></param>
        private void CheckGesture(GestureEvents gestureEvent)
        {
            switch (gestureEvent.trackingHand)
            {
                case TrackingHand.Left:
                    if (!_leftHand) break;
                    CheckOneHandRecognition(gestureEvent, _leftHand);
                    break;
                case TrackingHand.Right:
                    if (!_rightHand) break;
                    CheckOneHandRecognition(gestureEvent, _rightHand);
                    break;
                case TrackingHand.Both:
                    CheckBothHandsRecognition(gestureEvent);
                    break;
                case TrackingHand.Any:
                    CheckAnyHandsRecognition(gestureEvent);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        /// <summary>
        /// Checks the hand tracking data conformity to the gesture data  and invokes the related events.
        /// </summary>
        /// <param name="gestureEvent"></param>
        /// <param name="hand"></param>
        private void CheckOneHandRecognition(GestureEvents gestureEvent, WeArtHandController hand)
        {
            if (!GestureRecognizer.CheckMatchGesture(gestureEvent.gestureName, hand))
            {
                CheckEndRecognition(gestureEvent);
                return;
            }

            if (!gestureEvent.IsRecognized)
            {
                CheckDelayTimeRecognition(gestureEvent);
                return;
            }
            gestureEvent.onStayRecognized.Invoke();
        }
        
        /// <summary>
        /// Checks if both hands conform to the gesture data and invokes the related events.
        /// </summary>
        /// <param name="gestureEvent"></param>
        private void CheckBothHandsRecognition(GestureEvents gestureEvent)
        {
            if (GestureRecognizer.CheckMatchGesture(gestureEvent.gestureName, _leftHand) && GestureRecognizer.CheckMatchGesture(gestureEvent.gestureName, _rightHand))
            {
                if (!gestureEvent.IsRecognized)
                {
                    CheckDelayTimeRecognition(gestureEvent);
                    return;
                }
                
                gestureEvent.onStayRecognized.Invoke();
                return;
            }
            
            CheckEndRecognition(gestureEvent);
        }
        
        /// <summary>
        /// Checks if any hand tracking data conforms to the gesture data and invokes the related events.
        /// </summary>
        /// <param name="gestureEvent"></param>
        private void CheckAnyHandsRecognition(GestureEvents gestureEvent)
        {
            if (GestureRecognizer.CheckMatchGesture(gestureEvent.gestureName, _leftHand) || GestureRecognizer.CheckMatchGesture(gestureEvent.gestureName, _rightHand))
            {
                if (!gestureEvent.IsRecognized)
                {
                    CheckDelayTimeRecognition(gestureEvent);
                    return;
                }
                
                gestureEvent.onStayRecognized.Invoke();
                return;
            }
            
            CheckEndRecognition(gestureEvent);
        }
        
        /// <summary>
        /// Checks the delay recognition, if the gesture still recognized it calls OnRecognized.
        /// </summary>
        /// <param name="gestureEvent"></param>
        private void CheckDelayTimeRecognition(GestureEvents gestureEvent)
        {
            gestureEvent.RecognitionTime += Time.deltaTime;

            if (gestureEvent.recognitionDelay > gestureEvent.RecognitionTime) return;
            gestureEvent.IsRecognized = true;
            
            gestureEvent.onRecognized.Invoke();
        }

        /// <summary>
        /// Checks if the not recognized gesture was recognized earlier and calls the OnEndRecognized if it was.
        /// </summary>
        /// <param name="gestureEvent"></param>
        private void CheckEndRecognition(GestureEvents gestureEvent)
        {
            gestureEvent.RecognitionTime = 0f;
            
            if (!gestureEvent.IsRecognized) return;
            
            gestureEvent.IsRecognized = false;

            gestureEvent.onEndRecognized.Invoke();
        }
    }
    
}
