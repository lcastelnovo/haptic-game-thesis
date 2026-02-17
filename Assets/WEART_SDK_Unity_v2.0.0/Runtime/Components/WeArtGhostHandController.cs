using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WeArt.Components;
using WeArt.Core;

public class WeArtGhostHandController : MonoBehaviour
{
    /// <summary>
    /// Defines the _openedHandState.
    /// </summary>
    [SerializeField]
    internal AnimationClip _openedHandState;

    /// <summary>
    /// Defines the _closedHandState.
    /// </summary>
    [SerializeField]
    internal AnimationClip _closedHandState;

    /// <summary>
    /// Defines the _abductionHandState.
    /// </summary>
    [SerializeField]
    internal AnimationClip _abductionHandState;

    /// <summary>
    /// Defines the _animator.
    /// </summary>
    private Animator _animator;

    /// <summary>
    /// Defines the _fingers.
    /// </summary>
    private AvatarMask[] _fingers;

    /// <summary>
    /// Defines the _thimbles.
    /// </summary>
    private WeArtThimbleTrackingObject[] _thimbles;

    /// <summary>
    /// Defines the _thumbThimble, _indexThimble, _middleThimble.
    /// </summary>
    [SerializeField]
    internal WeArtThimbleTrackingObject _thumbThimbleTracking, _indexThimbleTracking, _middleThimbleTracking, _annularThimbleTracking, _pinkyThimbleTracking;

    /// <summary>
    /// Defines the _thumbMask, _indexMask, _middleMask, _ringMask, _pinkyMask.
    /// </summary>
    [SerializeField]
    internal AvatarMask _thumbMask, _indexMask, _middleMask, _ringMask, _pinkyMask;

    private PlayableGraph _graph;

    private AnimationLayerMixerPlayable[] _fingersMixers;

    private void Awake()
    {
        // Set references on awake
        _animator = GetComponent<Animator>();
        _fingers = new AvatarMask[] { _thumbMask, _indexMask, _middleMask, _ringMask, _pinkyMask };
        _thimbles = new WeArtThimbleTrackingObject[] {
                _thumbThimbleTracking,
                _indexThimbleTracking,
                _middleThimbleTracking,
                _annularThimbleTracking,
                _pinkyThimbleTracking
            };
    }

    private void OnEnable()
    {
        // Initialize the animation graph
        _graph = PlayableGraph.Create(nameof(WeArtHandController));

        var fingersLayerMixer = AnimationLayerMixerPlayable.Create(_graph, _fingers.Length);
        _fingersMixers = new AnimationLayerMixerPlayable[_fingers.Length];

        for (uint i = 0; i < _fingers.Length; i++)
        {
            var fingerMixer = AnimationLayerMixerPlayable.Create(_graph, 3);
            _graph.Connect(AnimationClipPlayable.Create(_graph, _openedHandState), 0, fingerMixer, 0);
            _graph.Connect(AnimationClipPlayable.Create(_graph, _closedHandState), 0, fingerMixer, 1);
            _graph.Connect(AnimationClipPlayable.Create(_graph, _abductionHandState), 0, fingerMixer, 2);

            fingerMixer.SetLayerAdditive(0, false);
            fingerMixer.SetLayerMaskFromAvatarMask(0, _fingers[i]);
            fingerMixer.SetInputWeight(0, 1);
            fingerMixer.SetInputWeight(1, 0);
            _fingersMixers[i] = fingerMixer;

            fingersLayerMixer.SetLayerAdditive(i, false);
            fingersLayerMixer.SetLayerMaskFromAvatarMask(i, _fingers[i]);
            _graph.Connect(fingerMixer, 0, fingersLayerMixer, (int)i);
            fingersLayerMixer.SetInputWeight((int)i, 1);
        }

        var handMixer = AnimationMixerPlayable.Create(_graph, 2);
        _graph.Connect(fingersLayerMixer, 0, handMixer, 0);
        handMixer.SetInputWeight(0, 1);
        var playableOutput = AnimationPlayableOutput.Create(_graph, nameof(WeArtHandController), _animator);
        playableOutput.SetSourcePlayable(handMixer);
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        _graph.Play();
    }

    private void Update()
    {
        _graph.Evaluate();
        
        // Animate the ghost hand
        for (int i = 0; i < _fingers.Length; i++)
        {
            float weight;
            weight = _thimbles[i].Closure.Value;

            if (i > 2 && WeArtController.Instance._deviceGeneration == DeviceGeneration.TD)
            {
                _fingersMixers[i].SetInputWeight(0, 1 - _fingersMixers[2].GetInputWeight(1));
                _fingersMixers[i].SetInputWeight(1, _fingersMixers[2].GetInputWeight(1));
            }
            else
            {
                _fingersMixers[i].SetInputWeight(0, 1 - weight);
                _fingersMixers[i].SetInputWeight(1, weight);
            }

            if (_thimbles[i].ActuationPoint == ActuationPoint.Thumb)
            {
                float abduction;
                abduction = _thimbles[i].Abduction.Value;

                _fingersMixers[i].SetInputWeight(2, abduction);
            }
        }
    }
}
