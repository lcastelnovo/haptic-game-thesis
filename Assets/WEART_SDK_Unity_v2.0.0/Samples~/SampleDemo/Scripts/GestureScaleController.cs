using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GestureScaleController : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _handTransform;
    [SerializeField] private float _scaleForce =2;
    [SerializeField] private float _minScale =0.5f;
    [SerializeField] private float _maxScale =1.5f;
    [SerializeField] private WeArtSlider _WeArtSlider;
    [SerializeField] private ScaleSlider _scaleSlider;
    private Vector3 _initialTargetPosition;
    private bool _isScaling = false;
    private Vector3 _initialScale;

    private float _sizeDifference;
    private Vector3 _finalScale;
    void Start()
    {
        
    }

    
    void Update()
    {
        if( _isScaling )
        {
            _sizeDifference = _handTransform.position.y - _initialTargetPosition.y;
            _sizeDifference *= _scaleForce;
            _finalScale = _initialScale + new Vector3( _sizeDifference, _sizeDifference, _sizeDifference );

            if(_finalScale.x < _minScale)
            {
                _target.transform.localScale = new Vector3(_minScale, _minScale, _minScale);
                SetSliderHandle(_target.transform.localScale.x);
                return;
            }

            if (_finalScale.x > _maxScale)
            {
                _target.transform.localScale = new Vector3(_maxScale, _maxScale, _maxScale);
                SetSliderHandle(_target.transform.localScale.x);
                return;
            }

            _target.transform.localScale = _finalScale;
            SetSliderHandle(_target.transform.localScale.x);
        }
    }

    private void SetSliderHandle(float pScale)
    {
        // transform from range [0.5,1.5] to range [-1,1]
        pScale -= 0.5f;
        pScale *= 2;
        pScale -= 1;
        _WeArtSlider.SetValue(pScale);
        _scaleSlider.SetLastSliderValue(pScale);
    }

    public void StartScaling()
    {
        _isScaling = true;
        _initialTargetPosition = _handTransform.position;
        _initialScale = _target.transform.localScale;
    }

    public void StopScaling()
    {
        _isScaling=false;
    }
}
