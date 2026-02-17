using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using UnityEngine.UI;
using System;

namespace WEART
{
    public class G2ModeSwitcher : MonoBehaviour
    {
        [SerializeField] private WeArtHandController _leftHand;
        [SerializeField] private WeArtHandController _rightHand;

        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private Button restoreButton;
        
        private bool isModeActivated;


        private void Start()
        {
            leftButton.onClick.AddListener(()=>ActivateG2DevelopmentMode(HandSide.Left));
            rightButton.onClick.AddListener(()=>ActivateG2DevelopmentMode(HandSide.Right));
            restoreButton.onClick.AddListener(RestoreActuationPoints);
        }
        
        
        public void ActivateG2DevelopmentMode(HandSide handSide)
        {
            if (isModeActivated) RestoreActuationPoints();
            
            if (handSide == HandSide.Right)
            {
                SetHapticObjectProperties(_rightHand, ActuationPoint.Palm, ActuationPointFlags.Thumb, HandSideFlags.Left);
                SetHapticObjectProperties(_rightHand, ActuationPoint.Annular, ActuationPointFlags.Middle, HandSideFlags.Left);
                SetHapticObjectProperties(_rightHand, ActuationPoint.Pinky, ActuationPointFlags.Index, HandSideFlags.Left);
            }
            else
            {
                SetHapticObjectProperties(_leftHand, ActuationPoint.Palm, ActuationPointFlags.Thumb, HandSideFlags.Right);
                SetHapticObjectProperties(_leftHand, ActuationPoint.Annular, ActuationPointFlags.Middle, HandSideFlags.Right);
                SetHapticObjectProperties(_leftHand, ActuationPoint.Pinky, ActuationPointFlags.Index, HandSideFlags.Right);
            }

            isModeActivated = true;
        }

        public void RestoreActuationPoints()
        {
            SetHapticObjectProperties(_rightHand, ActuationPoint.Palm, ActuationPointFlags.Palm, HandSideFlags.Right);
            SetHapticObjectProperties(_rightHand, ActuationPoint.Annular, ActuationPointFlags.Annular, HandSideFlags.Right);
            SetHapticObjectProperties(_rightHand, ActuationPoint.Middle, ActuationPointFlags.Middle, HandSideFlags.Right);
            
            SetHapticObjectProperties(_leftHand, ActuationPoint.Palm, ActuationPointFlags.Palm, HandSideFlags.Left);
            SetHapticObjectProperties(_leftHand, ActuationPoint.Annular, ActuationPointFlags.Annular, HandSideFlags.Left);
            SetHapticObjectProperties(_leftHand, ActuationPoint.Pinky, ActuationPointFlags.Pinky, HandSideFlags.Left);

            isModeActivated = false;
        }
        
        private void SetHapticObjectProperties(WeArtHandController hand, ActuationPoint actuationPoint, ActuationPointFlags newActuationPointFlags, HandSideFlags newHandSideFlags)
        {
            WeArtHapticObject hapticObject = hand.GetHapticObject(actuationPoint);
            WeArtThimbleTrackingObject trackingObject = hand.GetThimbleTrackingObject(actuationPoint);
            
            hapticObject.ActuationPoints = newActuationPointFlags;
            hapticObject.HandSides = newHandSideFlags;
            
            if (!trackingObject) return;
            
            trackingObject.ActuationPoint = GetEnumFromFlags<ActuationPoint, ActuationPointFlags>(newActuationPointFlags);
            trackingObject.HandSide = GetEnumFromFlags<HandSide, HandSideFlags>(newHandSideFlags);
            }
        
        private TEnum GetEnumFromFlags<TEnum, TFlags>(TFlags flags) where TEnum : Enum where TFlags : Enum
        {
            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                TFlags flagValue = (TFlags)Enum.Parse(typeof(TFlags), value.ToString());
                if (flags.HasFlag(flagValue))
                {
                    return value;
                }
            }
            return default;
        }
    }
}
