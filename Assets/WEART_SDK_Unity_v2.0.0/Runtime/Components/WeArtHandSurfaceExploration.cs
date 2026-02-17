using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using WEART;
using Texture = WeArt.Core.Texture;

[RequireComponent(typeof(WeArtHandController))]
public class WeArtHandSurfaceExploration : MonoBehaviour
{
    /// <summary>
    /// Difines the finger's haptic object origin
    /// </summary>
    [SerializeField]
    internal Transform _thumbOrigin, _indexOrigin, _middleOrigin, _palmOrigin, _annularOrigin, _pinkyOrigin;

    /// <summary>
    /// Difines the finger's haptic object offset
    /// </summary>
    [SerializeField]
    internal Transform _thumbOffset, _indexOffset, _middleOffset, _annularOffset, _pinkyOffset, _palmOffset;

    /// <summary>
    /// Difines the finger's haptic object component
    /// </summary>
    private WeArtHapticObject _thumbThimbleHaptic, _indexThimbleHaptic, _middleThimbleHaptic, _palmThimbleHaptic, _annularThimbleHaptic, _pinkyThimbleHaptic;

    /// <summary>
    /// Difines the finger's thimble tracking object component
    /// </summary>
    private WeArtThimbleTrackingObject _thumbThimbleTracking, _indexThimbleTracking, _middleThimbleTracking, _annularThimbleTracking, _pinkyThimbleTracking;

    // Haptic objects' origin position
    private Vector3 _thumbHapticPosition;
    private Vector3 _indexHapticPosition;
    private Vector3 _middleHapticPosition;
    private Vector3 _annularHapticPosition;
    private Vector3 _pinkyHapticPosition;
    private Vector3 _palmHapticPosition;

    // Haptic objects' offset position
    private Vector3 _thumbHapticExplorationPosition;
    private Vector3 _indexHapticExplorationPosition;
    private Vector3 _middleHapticExplorationPosition;
    private Vector3 _annularHapticExplorationPosition;
    private Vector3 _pinkyHapticExplorationPosition;
    private Vector3 _palmHapticExplorationPosition;

    /// <summary>
    /// Defines the hand's hand controller component
    /// </summary>
    private WeArtHandController _handController;

    /// <summary>
    /// Defines the hand's grasping system component
    /// </summary>
    private WeArtHandGraspingSystem _graspingSystem;

    /// <summary>
    /// Set up
    /// </summary>
    private void Awake()
    {
        _handController = GetComponent<WeArtHandController>();
        _graspingSystem = GetComponent<WeArtHandGraspingSystem>();

        _thumbThimbleHaptic = _handController._thumbThimbleHaptic;
        _indexThimbleHaptic = _handController._indexThimbleHaptic;
        _middleThimbleHaptic = _handController._middleThimbleHaptic;
        _annularThimbleHaptic = _handController._annularThimbleHaptic;
        _pinkyThimbleHaptic = _handController._pinkyThimbleHaptic;
        _palmThimbleHaptic = _handController._palmThimbleHaptic;

        _thumbThimbleTracking = _handController._thumbThimbleTracking;
        _indexThimbleTracking = _handController._indexThimbleTracking;
        _middleThimbleTracking = _handController._middleThimbleTracking;
        _annularThimbleTracking = _handController._annularThimbleTracking;
        _pinkyThimbleTracking = _handController._pinkyThimbleTracking;

        _thumbHapticPosition = _thumbThimbleHaptic.transform.localPosition;
        _indexHapticPosition = _indexThimbleHaptic.transform.localPosition;
        _middleHapticPosition = _middleThimbleHaptic.transform.localPosition;
        _annularHapticPosition = _annularThimbleHaptic.transform.localPosition;
        _pinkyHapticPosition = _pinkyThimbleHaptic.transform.localPosition;
        _palmHapticPosition = _palmThimbleHaptic.transform.localPosition;

        _thumbHapticExplorationPosition = _thumbOffset.localPosition;
        _indexHapticExplorationPosition = _indexOffset.localPosition;
        _middleHapticExplorationPosition = _middleOffset.localPosition;
        _annularHapticExplorationPosition = _annularOffset.localPosition;
        _pinkyHapticExplorationPosition = _pinkyOffset.localPosition;
        _palmHapticExplorationPosition = _palmOffset.localPosition;
    }

    /// <summary>
    /// Reset haptic objects to original posisitions
    /// </summary>
    public void ResetHapticObjectsPosition()
    {
        _thumbThimbleHaptic.transform.localPosition = _thumbHapticPosition;
        _indexThimbleHaptic.transform.localPosition = _indexHapticPosition;
        _middleThimbleHaptic.transform.localPosition = _middleHapticPosition;
        _annularThimbleHaptic.transform.localPosition = _annularHapticPosition;
        _pinkyThimbleHaptic.transform.localPosition = _pinkyHapticPosition;
        _palmThimbleHaptic.transform.localPosition = _palmHapticPosition;
    }

    /// <summary>
    /// If two of the five rayscasts that start from the fingertips hit a touchable object with the Surface Exploration flag true, displace the haptic objects for a better exploration experience
    /// </summary>
    public void SurfaceExplorationCheck()
    {
        if (_graspingSystem.GraspingState == GraspingState.Grabbed)
        {
            return;
        }

        float raycastDistance = 0.03f;
        RaycastHit[] hits;
        bool isThumbOnSurface = false;
        bool isIndexOnSurface = false;
        bool isMiddleOnSurface = false;
        bool isAnnularOnSurface = false;
        bool isPinkyOnSurface = false;

        bool thumbOnGraspable = false;
        bool indexOnGraspable = false;
        bool middleOnGraspable = false;
        bool annularOnGraspable = false;
        bool pinkyOnGraspable = false;

        hits = Physics.RaycastAll(_thumbOrigin.position, transform.up * -1, raycastDistance);
        foreach (var item in hits)
        {
            if (TryGetTouchableObjectFromCollider(item.collider, out var touchable))
            {
                if (touchable._surfaceExploration)
                {
                    isThumbOnSurface = true;
                }

                if (touchable.Graspable)
                {
                    thumbOnGraspable = true;
                }
            }
        }

        if (thumbOnGraspable)
            isThumbOnSurface = false;

        hits = Physics.RaycastAll(_indexOrigin.position, transform.up * -1, raycastDistance);
        foreach (var item in hits)
        {
            if (TryGetTouchableObjectFromCollider(item.collider, out var touchable))
            {
                if (touchable._surfaceExploration)
                {
                    isIndexOnSurface = true;
                }

                if (touchable.Graspable)
                {
                    indexOnGraspable = true;
                }
            }
        }

        if (indexOnGraspable)
            isIndexOnSurface = false;

        hits = Physics.RaycastAll(_middleOrigin.position, transform.up * -1, raycastDistance);
        foreach (var item in hits)
        {
            if (TryGetTouchableObjectFromCollider(item.collider, out var touchable))
            {
                if (touchable._surfaceExploration)
                {
                    isMiddleOnSurface = true;
                }
                if (touchable.Graspable)
                {
                    middleOnGraspable = true;
                }
            }
        }

        if (middleOnGraspable)
            isMiddleOnSurface = false;

        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
        {
            hits = Physics.RaycastAll(_annularOrigin.position, transform.up * -1, raycastDistance);
            foreach (var item in hits)
            {
                if (TryGetTouchableObjectFromCollider(item.collider, out var touchable))
                {
                    if (touchable._surfaceExploration)
                    {
                        isAnnularOnSurface = true;
                    }
                    if (touchable.Graspable)
                    {
                        annularOnGraspable = true;
                    }
                }
            }

            if (annularOnGraspable)
                isAnnularOnSurface = false;

            hits = Physics.RaycastAll(_pinkyOrigin.position, transform.up * -1, raycastDistance);
            foreach (var item in hits)
            {
                if (TryGetTouchableObjectFromCollider(item.collider, out var touchable))
                {
                    if (touchable._surfaceExploration)
                    {
                        isPinkyOnSurface = true;
                    }

                    if (touchable.Graspable)
                    {
                        pinkyOnGraspable = true;
                    }
                }
            }

            if (pinkyOnGraspable)
                isPinkyOnSurface = false;
        }

        if ((isThumbOnSurface && isIndexOnSurface) || (isThumbOnSurface && isMiddleOnSurface) || (isIndexOnSurface && isMiddleOnSurface) ||
        (isThumbOnSurface && isAnnularOnSurface) || (isThumbOnSurface && isPinkyOnSurface) || (isIndexOnSurface && isAnnularOnSurface) ||
        (isIndexOnSurface && isPinkyOnSurface) || (isMiddleOnSurface && isAnnularOnSurface) || (isMiddleOnSurface && isPinkyOnSurface) ||
        (isAnnularOnSurface && isPinkyOnSurface))
        {
            _thumbThimbleTracking.SafeUnblockSeconds =_handController.GetSafeUnlockFrames();
            _indexThimbleTracking.SafeUnblockSeconds = _handController.GetSafeUnlockFrames();
            _middleThimbleTracking.SafeUnblockSeconds = _handController.GetSafeUnlockFrames();
            _annularThimbleTracking.SafeUnblockSeconds = _handController.GetSafeUnlockFrames();
            _pinkyThimbleTracking.SafeUnblockSeconds = _handController.GetSafeUnlockFrames();

            _handController.SetSlowFingerAnimationTime(_handController.GetSafeUnlockFrames());

            _thumbThimbleHaptic.transform.localPosition = _thumbHapticExplorationPosition;
            _indexThimbleHaptic.transform.localPosition = _indexHapticExplorationPosition;
            _middleThimbleHaptic.transform.localPosition = _middleHapticExplorationPosition;
            _annularThimbleHaptic.transform.localPosition = _annularHapticExplorationPosition;
            _pinkyThimbleHaptic.transform.localPosition = _pinkyHapticExplorationPosition;
            _palmThimbleHaptic.transform.localPosition = _palmHapticExplorationPosition;
        }
        else
        {
            _thumbThimbleHaptic.transform.localPosition = _thumbHapticPosition;
            _indexThimbleHaptic.transform.localPosition = _indexHapticPosition;
            _middleThimbleHaptic.transform.localPosition = _middleHapticPosition;
            _annularThimbleHaptic.transform.localPosition = _annularHapticPosition;
            _pinkyThimbleHaptic.transform.localPosition = _pinkyHapticPosition;
            _palmThimbleHaptic.transform.localPosition = _palmHapticPosition;
        }

    }

    /// <summary>
    /// Try to find a touchable object component on the collider
    /// </summary>
    /// <param name="collider">The collider<see cref="Collider"/>.</param>
    /// <param name="touchableObject">The touchableObject<see cref="WeArtTouchableObject"/>.</param>
    /// <returns>The <see cref="bool"/>.</returns>
    private static bool TryGetTouchableObjectFromCollider(Collider collider, out WeArtTouchableObject touchableObject)
    {
        touchableObject = collider.gameObject.GetComponent<WeArtTouchableObject>();
        if (collider.gameObject.GetComponent<WeArtChildCollider>() != null)
        {
            touchableObject = collider.gameObject.GetComponent<WeArtChildCollider>().ParentTouchableObject;
        }

        return touchableObject != null;
    }
}
