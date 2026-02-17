using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Texture = WeArt.Core.Texture;

public class GestureRotationController : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _handTransform;
    [SerializeField] private Transform _camera;
    [SerializeField] private float _rotationForce = 1000;
    [SerializeField] private RotationSlider _rotationSlider;
    [SerializeField] private WeArtSlider _weArtSlider;
    private Vector3 _initialCameraPosition;
    private Vector3 _initialCameraRightVector;
    private Vector3 _initialTargetPosition;
    private bool _isRotation = false;
    private Quaternion _initialRotation;
    private float _initialSliderValue;


    private readonly WeArtTouchEffect _gestureEffect = new WeArtTouchEffect();
    [SerializeField]
    internal WeArtHapticObject _thumbThimbleHaptic, _indexThimbleHaptic, _middleThimbleHaptic;

    private float _rotationDifference;
    void Start()
    {

    }
    void Update()
    {
        if (_isRotation)
        {
            _rotationDifference = Vector3.Dot(_initialTargetPosition - _initialCameraPosition, _initialCameraRightVector) - Vector3.Dot(_handTransform.position - _initialCameraPosition, _initialCameraRightVector);

            _rotationDifference *= _rotationForce;

            float newSliderValue = _initialSliderValue - _rotationDifference / 180;

            if (newSliderValue < -1)
            {
                return;
            }

            if (newSliderValue > 1)
            {
                return;
            }

            _rotationSlider.SetLastSliderValue(newSliderValue);
            _weArtSlider.SetValue(newSliderValue);

            _target.transform.rotation = _initialRotation;
            _target.transform.Rotate(Vector3.up, _rotationDifference, Space.World);
        }
    }

    public void StartRotating()
    {
        _isRotation = true;
        _initialTargetPosition = _handTransform.position;
        _initialRotation = _target.transform.rotation;
        _initialCameraPosition = _camera.position;
        _initialCameraRightVector = _camera.right;
        _initialSliderValue = _weArtSlider.GetValue();

        Temperature temperature = Temperature.Default;

        Force force = Force.Default;
        force.Value = 1;
        force.Active = true;

        _gestureEffect.Set(temperature, force, Texture.Default, null);

        _thumbThimbleHaptic.AddEffect(_gestureEffect,null);
        _indexThimbleHaptic.AddEffect(_gestureEffect, null);
        _middleThimbleHaptic.AddEffect(_gestureEffect, null);
    }

    public void StopRotating()
    {
        if (_isRotation)
        {
            _thumbThimbleHaptic.RemoveEffect();
            _indexThimbleHaptic.RemoveEffect();
            _middleThimbleHaptic.RemoveEffect();
        }

        _isRotation = false;
    }
}

