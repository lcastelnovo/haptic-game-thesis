using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeArtInterpolateToPosition : MonoBehaviour
{
    private Vector3 _initialPos;
    private Vector3 _targetPos;
    private Quaternion _initialRot;
    private Quaternion _targetRot;
    private float _currentTime = 0;
    private float _completionTime = 0.1f;

    void Start()
    {
        
    }

    public void SetPositionAndRotation(Vector3 pos, Quaternion rot)
    {
        _initialPos = transform.localPosition;
        _initialRot = transform.localRotation;
        _targetPos = pos;
        _targetRot = rot;
    }

    void Update()
    {
        _currentTime += Time.deltaTime;

        if(_currentTime > _completionTime)
        {
            transform.localPosition = _targetPos;
            transform.localRotation = _targetRot;
            Destroy(this);
            return;
        }

        transform.localPosition = Vector3.Lerp(_initialPos, _targetPos, _currentTime/_completionTime);
        transform.localRotation = Quaternion.Lerp(_initialRot, _targetRot, _currentTime/_completionTime);

    }
}
