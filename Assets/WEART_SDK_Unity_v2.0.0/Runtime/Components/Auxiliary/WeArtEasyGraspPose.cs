using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Core;
using WeArt.Components;
using EasyGraspData = WeArt.Components.WeArtTouchableObject.EasyGraspData;

public class WeArtEasyGraspPose : MonoBehaviour
{
    [SerializeField]
    internal bool _interpolate = true;

    [SerializeField]
    internal HandSide _handSide;

    [SerializeField]
    internal Vector3 _touchablePosition;

    [SerializeField]
    internal Vector3 _handPosition;

    [SerializeField] 
    internal Quaternion _touchableRotation;

    [SerializeField]
    internal Quaternion _handRotation;

    [SerializeField]
    internal float _thumbClosure;

    [SerializeField]
    internal float _thumbAbduction;

    [SerializeField]
    internal float _indexClosure;

    [SerializeField]
    internal float _middleClosure;

    [SerializeField]
    internal float _annularClosure;

    [SerializeField]
    internal float _pinkyClosure;

    [SerializeField]
    internal Vector3 _graspOriginOffset;

    [SerializeField]
    internal float _graspOriginRadius = 0.1f;

    WeArtTouchableObject _touchableObject;

    private void Awake()
    {
        _touchableObject = GetComponent<WeArtTouchableObject>();
    }

    /// <summary>
    /// Return the toucha
    /// </summary>
    /// <returns></returns>
    public WeArtTouchableObject GetTouchable()
    {
        return _touchableObject;
    }

    void Start()
    {
        if (WeArtEasyGraspManager.Instance != null)
        {
            WeArtEasyGraspManager.Instance.AddSnapPose(this);
        }
    }

    /// <summary>
    /// Converts data to its own fields
    /// </summary>
    /// <param name="data"></param>
    public void SetData(EasyGraspData data)
    {
        _handSide = data.handSide;
        _touchablePosition = data.touchablePosition;
        _handPosition = data.handPosition;
        _touchableRotation = data.touchableRotation;
        _handRotation = data.handRotation;
        _thumbClosure = data.thumbClosure;
        _thumbAbduction = data.thumbAbduction;
        _indexClosure = data.indexClosure;
        _middleClosure = data.middleClosure;
        _annularClosure = data.annularClosure;
        _pinkyClosure = data.pinkyClosure;
        _graspOriginOffset = data.graspOriginOffset;
        _graspOriginRadius = data.graspOriginRadius;
    }

    /// <summary>
    /// Return its won data in EasyGraspDa
    /// </summary>
    /// <returns></returns>
    public EasyGraspData GetData()
    {
        EasyGraspData data = new EasyGraspData();
        data.handSide = _handSide;
        data.touchablePosition = _touchablePosition;
        data.handPosition = _handPosition;
        data.touchableRotation = _touchableRotation;
        data.handRotation = _handRotation;
        data.thumbClosure = _thumbClosure;
        data.thumbAbduction = _thumbAbduction;
        data.indexClosure = _indexClosure;
        data.middleClosure = _middleClosure;
        data.annularClosure = _annularClosure;
        data.pinkyClosure = _pinkyClosure;
        data.graspOriginOffset = _graspOriginOffset;
        data.graspOriginRadius= _graspOriginRadius;

        return data;
    }

    /// <summary>
    /// Returns if the snap grasping will have an interpolation effect
    /// </summary>
    /// <returns></returns>
    public bool GetIsInterpolating()
    {
        return _interpolate;
    }


    private void OnEnable()
    {
        if(WeArtEasyGraspManager.Instance != null)
        {
            WeArtEasyGraspManager.Instance.AddSnapPose(this);
        }
    }

    private void OnDisable()
    {
        if (WeArtEasyGraspManager.Instance != null)
        {
            WeArtEasyGraspManager.Instance.RemoveSnapPose(this);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws visual feedback of where the touchable object can be snap grasped
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if(_handSide == HandSide.Left)
        {
            Gizmos.color = Color.blue;
        }
        else
        {
            Gizmos.color = Color.cyan;
        }
        Gizmos.DrawWireSphere(transform.TransformPoint(_graspOriginOffset),_graspOriginRadius);
    }
#endif
}
