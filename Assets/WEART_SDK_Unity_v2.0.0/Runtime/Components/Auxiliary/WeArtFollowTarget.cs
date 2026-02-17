using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Utils;

/// <summary>
/// This component can be used to follow a specified spatially tracked transform.
/// Add it to the root object of your physical object. Make sure to specify
/// the right spatial offset between the tracked object and the follower.
/// </summary>
public class WeArtFollowTarget : MonoBehaviour
{
    [SerializeField]
    internal Rigidbody _rigidBody;

    [SerializeField]
    internal Transform _followTarget;

    [SerializeField]
    internal float _followPower = 0.3f;

    [SerializeField]
    internal Vector3 _positionOffset = Vector3.zero;

    [SerializeField]
    internal Vector3 _rotationOffset = Vector3.zero;

    void Start()
    {
        // Get Rigid Body if not set
        if (_rigidBody == null)
        {
            _rigidBody = GetComponent<Rigidbody>();

            if (_rigidBody == null)
            {
                WeArtLog.Log($"Cannot use method without a {nameof(Rigidbody)}", LogType.Error);
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (_followTarget == null ||  _rigidBody == null)
            return;
        
        // Velocity and Rotation
        _rigidBody.velocity = (_followTarget.TransformPoint(_positionOffset) - transform.position) / Time.fixedDeltaTime * _followPower;
        
        // Absolute rotation 
        transform.rotation = _followTarget.rotation * Quaternion.Euler(_rotationOffset);
    }
}

