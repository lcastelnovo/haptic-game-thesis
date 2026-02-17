using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;

public class WeArtSlider : MonoBehaviour
{
    [SerializeField] Transform _handle;
    [SerializeField] Transform _minTransform;
    [SerializeField] Transform _maxTransform;
    [SerializeField] WeArtTouchableObject _touchableObject;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleInput();
        HandleLimits();
    }

    public float GetValue()
    {
        Vector3 direction = _maxTransform.position - transform.position;
        Vector3 handleProgress = _handle.position - transform.position;

        float scaleDifference = 1.0f / direction.magnitude;
        direction *= scaleDifference;
        handleProgress *= scaleDifference;

        return Mathf.Clamp( Vector3.Dot(handleProgress, direction),-1f,1f);
    }

    public void SetValue(float pValue)
    {
        _handle.localPosition = Vector3.zero + _maxTransform.localPosition * pValue;
    }

    void HandleInput()
    {
        if(_touchableObject.AffectedHapticObjects.Count > 0)
        {
            bool found = false;
            foreach (var item in _touchableObject.AffectedHapticObjects)
            {
                if(item.HandSides != WeArt.Core.HandSideFlags.None)
                {
                    found = true; break;
                }
            }

            if (!found)
                return;

            Vector3 direction = _maxTransform.position - transform.position;
            Vector3 handPosition = new Vector3();

            bool isOnlyPalm = true;
            foreach (var item in _touchableObject.AffectedHapticObjects)
            {
                if (item.ActuationPoints != WeArt.Core.ActuationPointFlags.Palm)
                {
                    isOnlyPalm = false;
                    handPosition = item.transform.position;
                    break;
                }
            }

            if(isOnlyPalm)
            {
                return;
            }

            Vector3 handleProgress = handPosition - transform.position;

            float scaleDifference = 1.0f / direction.magnitude;
            direction *= scaleDifference;
            handleProgress *= scaleDifference;

            float offset = Vector3.Dot(handleProgress, direction);

            _handle.localPosition = new Vector3(_maxTransform.localPosition.x *offset, 0, 0);

        }
    }

    void HandleLimits()
    {
        if(_handle.localPosition.x < _maxTransform.localPosition.x)
        {
            _handle.localPosition = _maxTransform.localPosition;
        }

        if (_handle.localPosition.x > _minTransform.localPosition.x)
        {
            _handle.localPosition = _minTransform.localPosition;
        }
    }
}
