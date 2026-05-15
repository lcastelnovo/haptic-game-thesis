using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;

public class ViveTrackerCalibrationManager : MonoBehaviour
{
    public HandSide calibrationHandSide;
    public bool freezeFingersClosure = false;
    [Header("Hand Tracking Rotation Options")]
    public bool allowOnlyLateralRotation = false;
    public bool freezeHeight = false;
    public bool freezeAllRotation = false;

    [Header("Vive Tracker Transforms")]
    public Transform RightViveTracker;
    public Transform RightViveTrackerTarget;
    public Transform RightViveTrackerTransformation;
    public Transform LeftViveTracker;
    public Transform LeftViveTrackerTarget;
    public Transform LeftViveTrackerTransformation;
    public WeArtDeviceTrackingObject TrackingObjectRight;
    public WeArtDeviceTrackingObject TrackingObjectLeft;
    public Transform RightLateralRotationObject;
    public Transform LeftLateralRotationObject;
    public Transform OffsetOrigin;
    [Header("Fingers Closure")]
    public List<WeArtThimbleTrackingObject> fingersTrackingObjects;

    Vector3 _translateVector;
    float _angleOffset;
    Vector3 _positionOffset;

    float _rightRecordedHeight;
    float _leftRecordedHeight;
    bool _isHeightFrozen= false;
    bool _isRotationFrozen = false;

    void Start()
    {
        foreach (var finger in fingersTrackingObjects)
        {
            finger.IsBlocked = freezeFingersClosure;
            finger.BlockedClosureValue = finger.Closure.Value;
        }
    }

    void Update()
    {
        LookAtLateralDirection();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (calibrationHandSide == HandSide.Right)
            {
                _angleOffset = RightViveTrackerTarget.rotation.eulerAngles.y - RightViveTrackerTransformation.rotation.eulerAngles.y;
                OffsetOrigin.Rotate(0, _angleOffset, 0, Space.Self);

                _positionOffset = RightViveTrackerTarget.position - RightViveTracker.position;
                OffsetOrigin.position += _positionOffset;
            }
            else
            {
                _angleOffset = LeftViveTrackerTarget.rotation.eulerAngles.y - LeftViveTrackerTransformation.rotation.eulerAngles.y;
                OffsetOrigin.Rotate(0, _angleOffset, 0, Space.Self);

                _positionOffset = LeftViveTrackerTarget.position - LeftViveTracker.position;
                OffsetOrigin.position += _positionOffset;
            }

            LookAtLateralDirection();

            if (allowOnlyLateralRotation)
            {
                RightLateralRotationObject.position = RightViveTracker.position;
                RightLateralRotationObject.rotation = RightViveTracker.rotation;
                LeftLateralRotationObject.position = LeftViveTracker.position;
                LeftLateralRotationObject.rotation = LeftViveTracker.rotation;

                RightLateralRotationObject.parent = RightViveTrackerTransformation;
                LeftLateralRotationObject.parent = LeftViveTrackerTransformation;

                TrackingObjectRight.TrackingSource = RightLateralRotationObject;
                TrackingObjectLeft.TrackingSource = LeftLateralRotationObject;

            }

            if (freezeHeight)
            {
                _rightRecordedHeight = RightViveTracker.position.y;
                _leftRecordedHeight = LeftViveTracker.position.y;
                _isHeightFrozen = true;
            }

            if (freezeAllRotation)
            {
                _isRotationFrozen = true;
            }
        }
    }

    void LookAtLateralDirection()
    {
        RightViveTrackerTransformation.position = RightViveTracker.position;
        _translateVector = RightViveTrackerTransformation.position + RightViveTracker.up;
        _translateVector.y = RightViveTrackerTransformation.position.y;
        RightViveTrackerTransformation.LookAt(_translateVector, Vector3.up);

        LeftViveTrackerTransformation.position = LeftViveTracker.position;
        _translateVector = LeftViveTrackerTransformation.position + LeftViveTracker.up;
        _translateVector.y = LeftViveTrackerTransformation.position.y;
        LeftViveTrackerTransformation.LookAt(_translateVector, Vector3.up);

        if(_isHeightFrozen)
        {
            RightViveTrackerTransformation.position = new Vector3(RightViveTrackerTransformation.position.x, _rightRecordedHeight, RightViveTrackerTransformation.position.z);
            LeftViveTrackerTransformation.position = new Vector3(LeftViveTrackerTransformation.position.x, _leftRecordedHeight, LeftViveTrackerTransformation.position.z);
        }

        if(_isRotationFrozen)
        {
            RightViveTrackerTransformation.LookAt(RightViveTrackerTransformation.position + RightViveTrackerTarget.forward, Vector3.up);
            LeftViveTrackerTransformation.LookAt(LeftViveTrackerTransformation.position + LeftViveTrackerTarget.forward, Vector3.up);
        }
    }
}
