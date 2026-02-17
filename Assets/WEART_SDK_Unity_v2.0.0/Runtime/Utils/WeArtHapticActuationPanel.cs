using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WeArt.Components;
using WeArt.Core;

namespace WeArt.Utils.Actuation
{
    /// <summary>
    /// WeArtHapticActuationPanel — contains the Actuation data of haptic elements of set hands. Useful for debug.  
    /// </summary>
    public class WeArtHapticActuationPanel : MonoBehaviour
    {
        [SerializeField] internal WeArtHandController leftHandObject;
        [SerializeField] internal WeArtHandController rightHandObject;

        [SerializeField] internal List<WeArtHapticActuationElement> leftActuationElements;
        [SerializeField] internal List<WeArtHapticActuationElement> rightActuationElements;

        private WaitForSeconds _waiter = new WaitForSeconds(0.25f);
        
        private void Awake()
        {
            if (leftHandObject) SetHandHapticElementsForTacking(leftActuationElements, leftHandObject);
            if (rightHandObject) SetHandHapticElementsForTacking(rightActuationElements, rightHandObject);
        }

        private void Start()
        {
            AdjustPanelToTrackingType();
        }

        /// <summary>
        /// Shows/Hides this panel
        /// </summary>
        public void ShowHidePanel()
        {
            gameObject.SetActive(!isActiveAndEnabled);
        }

        /// <summary>
        /// Gets the Active Haptic Objects from scene for indicated handSide.
        /// Useful when there are a several hands/the same haptic objects that enables and disables at the scene.
        /// </summary>
        /// <param name="handSide"></param>
        public void GetActualHapticObjectsFromScene()
        {
            StartCoroutine(GetHapticObjectsCor());
        }

        private IEnumerator GetHapticObjectsCor()
        {
            yield return _waiter;
            
            var hapticObjects = FindObjectsOfType<WeArtHapticObject>();
            
            foreach (var haptic in hapticObjects)
            {
                if (!haptic.gameObject.activeInHierarchy) continue;

                switch (haptic.HandSides)
                {
                    case HandSideFlags.Left:
                        SetHapticElementToPanel(leftActuationElements, haptic);
                        break;
                    case HandSideFlags.Right:
                        SetHapticElementToPanel(rightActuationElements, haptic);
                        break;
                }
            }
        }
        
        private void SetHapticElementToPanel(List<WeArtHapticActuationElement>  actuationElements, WeArtHapticObject haptic)
        {
            switch (haptic.ActuationPoints)
            {
                case ActuationPointFlags.Thumb:
                    actuationElements[0].SetHapticObject(haptic);
                    break;
                case ActuationPointFlags.Index:
                    actuationElements[1].SetHapticObject(haptic);
                    break;
                case ActuationPointFlags.Middle:
                    actuationElements[2].SetHapticObject(haptic);
                    break;
                case ActuationPointFlags.Annular:
                    actuationElements[3].SetHapticObject(haptic);
                    break;
                case ActuationPointFlags.Pinky:
                    actuationElements[4].SetHapticObject(haptic);
                    break;
                case ActuationPointFlags.Palm:
                    actuationElements[5].SetHapticObject(haptic);
                    break;
            }
        }
        
        /// <summary>
        /// Links the haptic elements from hands to actuation fields at the panel. 
        /// </summary>
        /// <param name="hapticPanelElements"></param>
        /// <param name="hand"></param>
        private void SetHandHapticElementsForTacking(List<WeArtHapticActuationElement> hapticPanelElements,
            WeArtHandController hand)
        {
            hapticPanelElements[0].SetHapticObject(hand._thumbThimbleHaptic);
            hapticPanelElements[1].SetHapticObject(hand._indexThimbleHaptic);
            hapticPanelElements[2].SetHapticObject(hand._middleThimbleHaptic);
            hapticPanelElements[3].SetHapticObject(hand._annularThimbleHaptic);
            hapticPanelElements[4].SetHapticObject(hand._pinkyThimbleHaptic);
            hapticPanelElements[5].SetHapticObject(hand._palmThimbleHaptic);
        }

        /// <summary>
        /// Hide the debug elements not required for G2 devices.
        /// </summary>
        private void AdjustPanelToTrackingType()
        {
            if (WeArtController.Instance._deviceGeneration != DeviceGeneration.TD_Pro)
            {
                for (int i = 3; i < leftActuationElements.Count; i++)
                {
                    leftActuationElements[i].gameObject.SetActive(false);
                    rightActuationElements[i].gameObject.SetActive(false);
                }
            }

            StartCoroutine(UpdateLayout());
        }

        /// <summary>
        /// Rebuilds the layout of canvas;
        /// </summary>
        /// <returns></returns>
        private IEnumerator UpdateLayout()
        {
            foreach (Transform child in transform)
            {
                yield return null;

                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)child);
            }
        }
    }
}
