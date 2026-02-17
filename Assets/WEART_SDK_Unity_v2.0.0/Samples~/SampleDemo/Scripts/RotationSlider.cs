using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationSlider : MonoBehaviour
{
    [SerializeField] Transform _target;
    [SerializeField] WeArtSlider _slider;
    private float _lastSliderValue =0;

    void Start()
    {
        _lastSliderValue = _slider.GetValue();
    }

    void Update()
    {
        if (_lastSliderValue == _slider.GetValue())
            return;

        _target.Rotate(Vector3.up, (_lastSliderValue - _slider.GetValue()) * 180, Space.World);
        _lastSliderValue = _slider.GetValue();
        _target.localRotation = Quaternion.identity * Quaternion.Euler(0, _slider.GetValue() * -180, 0);

        _lastSliderValue = _slider.GetValue();
    }

    public void SetLastSliderValue(float pValue)
    {
        
    }
}
