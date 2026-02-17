using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class WeArtInterpolateFingers : MonoBehaviour
{
    [SerializeField]
    internal float _thumbClosureTarget;

    [SerializeField]
    internal float _thumbAbductionTarget;

    [SerializeField]
    internal float _indexClosureTarget;

    [SerializeField]
    internal float _middleClosureTarget;

    [SerializeField]
    internal float _annularClosureTarget;

    [SerializeField]
    internal float _pinkyClosureTarget;

    private float _totalTime = 0.1f;
    private float _currentTime = 0;

    private float _originalThumbClosure, _originalThumbAbduction, _originalIndexClosure, _originalMiddleClosure, _originalAnnularClosure, _originalPinkyClosure;
    private WeArtHandController _handController;
    private IReadOnlyList<AnimationLayerMixerPlayable> _fingexMixers;
    private WeArtTouchableObject.EasyGraspData _graspData;

    private void Awake()
    {
        _handController = GetComponent<WeArtHandController>();
    }

    public void SetTargetPose(WeArtTouchableObject.EasyGraspData targetPose)
    {
        _fingexMixers = _handController.FingersMixers;

        _originalThumbClosure = _fingexMixers[0].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
        _originalThumbAbduction = _fingexMixers[0].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(2);
        _originalIndexClosure = _fingexMixers[1].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
        _originalMiddleClosure = _fingexMixers[2].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
        _originalAnnularClosure = _fingexMixers[3].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);
        _originalPinkyClosure = _fingexMixers[4].GetInputWeight<UnityEngine.Animations.AnimationLayerMixerPlayable>(1);

        _thumbClosureTarget = targetPose.thumbClosure;
        _thumbAbductionTarget = targetPose.thumbAbduction;
        _indexClosureTarget = targetPose.indexClosure;
        _middleClosureTarget = targetPose.middleClosure;
        _annularClosureTarget = targetPose.annularClosure;
        _pinkyClosureTarget = targetPose.pinkyClosure;
    }

    void Update()
    {
        _graspData = new WeArtTouchableObject.EasyGraspData();
        _currentTime += Time.deltaTime;

        if(_currentTime >= _totalTime)
        {
            _graspData.thumbClosure = _thumbClosureTarget;
            _graspData.thumbAbduction = _thumbAbductionTarget;
            _graspData.indexClosure = _indexClosureTarget;
            _graspData.middleClosure = _middleClosureTarget;
            _graspData.annularClosure = _annularClosureTarget;
            _graspData.pinkyClosure = _pinkyClosureTarget;

            _handController.SetFingerAnimation(_graspData);
            Destroy(this);
        }
        _graspData.thumbClosure = Mathf.Lerp(_originalThumbClosure, _thumbClosureTarget,_currentTime/_totalTime);
        _graspData.thumbAbduction = Mathf.Lerp(_originalThumbAbduction, _thumbAbductionTarget,_currentTime/_totalTime);
        _graspData.indexClosure = Mathf.Lerp(_originalIndexClosure, _indexClosureTarget,_currentTime/_totalTime);
        _graspData.middleClosure = Mathf.Lerp(_originalMiddleClosure, _middleClosureTarget,_currentTime/_totalTime);
        _graspData.annularClosure = Mathf.Lerp(_originalAnnularClosure, _annularClosureTarget, _currentTime / _totalTime);
        _graspData.pinkyClosure = Mathf.Lerp(_originalPinkyClosure, _pinkyClosureTarget, _currentTime / _totalTime);

        _handController.SetFingerAnimation(_graspData);

    }
}
