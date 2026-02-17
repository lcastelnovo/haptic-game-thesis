using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WeArt.Core;
using Texture = WeArt.Core.Texture;

using static WeArt.Components.WeArtTouchableObject;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WeArt.Utils;

namespace WeArt.Components
{
    /// <summary>
    /// This component is able to animate a virtual hand using closure data coming from
    /// a set of <see cref="WeArtThimbleTrackingObject"/> components.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class WeArtHandController : MonoBehaviour
    {
        #region Fields

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
        /// Defines the finger animation mask.
        /// </summary>
        [SerializeField]
        internal AvatarMask _thumbMask, _indexMask, _middleMask, _ringMask, _pinkyMask;

        /// <summary>
        /// Defines the finger's thimble tracking component
        /// </summary>
        [SerializeField]
        internal WeArtThimbleTrackingObject _thumbThimbleTracking, _indexThimbleTracking, _middleThimbleTracking, _annularThimbleTracking, _pinkyThimbleTracking;

        /// <summary>
        /// Defines the finger's hapric object
        /// </summary>
        [SerializeField]
        internal WeArtHapticObject _thumbThimbleHaptic, _indexThimbleHaptic, _middleThimbleHaptic, _palmThimbleHaptic, _annularThimbleHaptic, _pinkyThimbleHaptic;

        /// <summary>
        /// Defines the visible hand's mesh renderer
        /// </summary>
        [SerializeField]
        internal SkinnedMeshRenderer _handSkinnedMeshRenderer;

        /// <summary>
        /// Defines the logo on the hand
        /// </summary>
        [SerializeField]
        internal GameObject _logoHandPanel;

        /// <summary>
        /// Defines the invisible material for the ghost hand
        /// </summary>
        [SerializeField]
        internal Material _ghostHandInvisibleMaterial;

        /// <summary>
        /// Defines the transparent material for the ghost hand
        /// </summary>
        [SerializeField]
        internal Material _ghostHandTransparentMaterial;

        /// <summary>
        /// If you use custom poses, you have to add WeArtGraspPose.cs
        /// </summary>
        [SerializeField]
        internal bool _useCustomPoses;

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
        /// Defines the player graph
        /// </summary>
        private PlayableGraph _graph;

        /// <summary>
        /// Defines the animation layers
        /// </summary>
        private AnimationLayerMixerPlayable[] _fingersMixers;

        /// <summary>
        /// Returns a read only list of the animation layers
        /// </summary>
        public IReadOnlyList<AnimationLayerMixerPlayable> FingersMixers => _fingersMixers.ToList();

        /// <summary>
        /// Define the hand side
        /// </summary>
        [SerializeField]
        internal HandSide _handSide;

        // Variables for finger animation
        private float _fingersAnimationSpeed = 10f;
        private float _thumbAnimationSpeed = 20f;
        private float _fingersSlideSpeed = 1.5f;
        private float _extraFingerSpeed = 10f;
        private float _safeUnlockFrames = 0.2f;
        private float _slowFingerAnimationTime;
        private float _slowFingerAnimationTimeMax = 1f;

        /// <summary>
        /// Defines the grasping system component
        /// </summary>
        private WeArtHandGraspingSystem _graspingSystem;

        /// <summary>
        /// Defines the surface exploration component
        /// </summary>
        private WeArtHandSurfaceExploration _surfaceExploration;

        /// <summary>
        /// Defines the opposite hand in the scene
        /// </summary>
        private WeArtHandController _otherHand;

        #endregion

        #region Methods

        /// <summary>
        /// Initial set up
        /// </summary>
        private void Awake()
        {
            // Setup components
            _graspingSystem = GetComponent<WeArtHandGraspingSystem>();
            _surfaceExploration = GetComponent<WeArtHandSurfaceExploration>();

            if (_graspingSystem == null)
            {
                WeArtLog.Log("Does Not Have The Grasping System Component", LogType.Error);
            }

            // Setup animation components
            _animator = GetComponent<Animator>();
            _fingers = new AvatarMask[] { _thumbMask, _indexMask, _middleMask, _ringMask, _pinkyMask };
            _thimbles = new WeArtThimbleTrackingObject[] {
                _thumbThimbleTracking,
                _indexThimbleTracking,
                _middleThimbleTracking,
                _annularThimbleTracking,
                _pinkyThimbleTracking
            };

            _thumbThimbleHaptic.SetIsUsedByController(true);
            _thumbThimbleHaptic.HandController = this;
            _indexThimbleHaptic.SetIsUsedByController(true);
            _indexThimbleHaptic.HandController = this;
            _middleThimbleHaptic.SetIsUsedByController(true);
            _middleThimbleHaptic.HandController = this;
            _annularThimbleHaptic.SetIsUsedByController(true);
            _annularThimbleHaptic.HandController = this;
            _pinkyThimbleHaptic.SetIsUsedByController(true);
            _pinkyThimbleHaptic.HandController = this;
            _palmThimbleHaptic.SetIsUsedByController(true);
            _palmThimbleHaptic.HandController = this;

            WeArtHandController[] components = GameObject.FindObjectsOfType<WeArtHandController>();
            foreach (var component in components)
            {
                if (component._handSide != _handSide)
                {
                    _otherHand = component;
                }
            }
        }

        /// <summary>
        /// The OnEnable.
        /// </summary>
        private void OnEnable()
        {
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

            // Subscribe custom finger closure behaviour during the grasp
            _graspingSystem.OnGraspingEvent += UpdateFingerClosure;
        }

        /// <summary>
        /// Handle the behaviour of all fingers during the grasp
        /// </summary>
        private void UpdateFingerClosure(HandSide hand, GameObject gameObject)
        {
            if (_useCustomPoses)
            {
                // In this case you have to use WeArtGraspPose on your touchable object to handle the fingers poses
                if (TryGetCustomPosesFromTouchable(gameObject, out var customPoses))
                {
                    StopAllCoroutines();

                    for (int i = 0; i < customPoses.fingersClosure.Length; i++)
                    {
                        var weight = customPoses.fingersClosure[i];

                        StartCoroutine(LerpPoses(_fingersMixers[i], 0, 1 - weight, customPoses.lerpTime));
                        StartCoroutine(LerpPoses(_fingersMixers[i], 1, weight, customPoses.lerpTime));
                    }
                }
            }
        }

        /// <summary>
        /// Get the hand controller's grasping system
        /// </summary>
        /// <returns></returns>
        public WeArtHandGraspingSystem GetGraspingSystem()
        { return _graspingSystem; }

        /// <summary>
        /// Get the other Hand Controller
        /// </summary>
        /// <returns></returns>
        public WeArtHandController GetOtherHand()
        { return _otherHand; }
        /// <summary>
        /// Get time in seconds of the time of animation for moving the fingers slowly after release
        /// </summary>
        /// <returns></returns>
        public float GetSafeUnlockFrames()
        { return _safeUnlockFrames; }

        /// <summary>
        /// Set time left to move the fingers slowly to avoid glitch collisions
        /// </summary>
        /// <param name="time"></param>
        public void SetSlowFingerAnimationTime(float time)
        { _slowFingerAnimationTime = time; }

        public float GetSlowFingerAnimationTimeMax()
        { return _slowFingerAnimationTimeMax; }


        /// <summary>
        /// Getr the invisible material
        /// </summary>
        /// <returns></returns>
        public Material GetGhostHandInvisibleMaterial()
        {
            return _ghostHandInvisibleMaterial;
        }

        /// <summary>
        /// Get the transparent material
        /// </summary>
        /// <returns></returns>
        public Material GetGhostHandTransparentMaterial()
        {
            return _ghostHandTransparentMaterial;
        }

        /// <summary>
        /// Get the hand side in flag format
        /// </summary>
        /// <returns></returns>
        public HandSideFlags GetHandSideFlag()
        {
            if (_handSide == HandSide.Right)
                return HandSideFlags.Right;
            else
                return HandSideFlags.Left;
        }

        /// <summary>
        /// Debug method
        /// </summary>
        private void OnDrawGizmos()
        {
        }

        /// <summary>
        /// The Update.
        /// </summary>
        private void Update()
        {
            if (_graspingSystem.GraspingState == GraspingState.Grabbed)
            {
                if(_graspingSystem.GetGraspedObject() != null)
                {
                    if(_graspingSystem.GetGraspedObject().GraspingType == GraspingType.Snap)
                    {
                        _graspingSystem.UpdateGraspingSystem();
                        return;
                    }
                }
            }

                AnimateFingers();

                _surfaceExploration.SurfaceExplorationCheck();

                _graspingSystem.UpdateGraspingSystem();
            
        }

        /// <summary>
        /// Method that takes care of finger animation
        /// </summary>
        private void AnimateFingers()
        {
            _graph.Evaluate();

            if (_useCustomPoses && _graspingSystem.GraspingState == GraspingState.Grabbed)
            {
                // In this case the fingers not follow the tracking but are driven by WeArtGraspPose
                // The behaviour is called in this script in -> UpdateFingerClousure
            }
            else // Otherwise fingers behaviour works as always
            {
                for (int i = 0; i < _fingers.Length; i++)
                {
                    if (!_thimbles[i].IsBlocked)
                    {
                        bool isGettingCloseToGrasp = false;

                        if (i == 0 && _graspingSystem.ThumbGraspCheckTouchables.Count > 0 && _thimbles[i].Closure.Value > _fingersMixers[i].GetInputWeight(1))
                            isGettingCloseToGrasp = true; // Finger is getting close to an object that it can grab and slows the speed in order to ensure a perfect position on the touchable object

                        if (i == 1 && _graspingSystem.IndexGraspCheckTouchables.Count > 0 && _thimbles[i].Closure.Value > _fingersMixers[i].GetInputWeight(1))
                            isGettingCloseToGrasp = true;

                        if (i == 2 && _graspingSystem.MiddleGraspCheckTouchables.Count > 0 && _thimbles[i].Closure.Value > _fingersMixers[i].GetInputWeight(1))
                            isGettingCloseToGrasp = true;

                        if (WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
                        {
                            if (i == 3 && _graspingSystem.AnnularGraspCheckTouchables.Count > 0 && _thimbles[i].Closure.Value > _fingersMixers[i].GetInputWeight(1))
                                isGettingCloseToGrasp = true;

                            if (i == 4 && _graspingSystem.PinkyGraspCheckTouchables.Count > 0 && _thimbles[i].Closure.Value > _fingersMixers[i].GetInputWeight(1))
                                isGettingCloseToGrasp = true;
                        }

                        float weight;
                        if (!isGettingCloseToGrasp)
                        {
                            weight = _thimbles[i].Closure.Value;
                        }
                        else // If in proximity, move slower in order to avoid clipping through colliders at high movement speed per frame
                        {
                            weight = Mathf.Lerp(_fingersMixers[i].GetInputWeight(1), _thimbles[i].Closure.Value,
                               Time.deltaTime * (_thimbles[i].SafeUnblockSeconds > 0 ? _fingersSlideSpeed : _fingersAnimationSpeed));
                        }

                        if (_slowFingerAnimationTime > 0)
                        {
                            weight = Mathf.Lerp(_fingersMixers[i].GetInputWeight(1), _thimbles[i].Closure.Value,
                               Time.deltaTime * _fingersSlideSpeed * _extraFingerSpeed);
                        }

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

                        // Thumb has an extra field called abduction that allows the finger to move up and down (non closing motion)
                        if (_thimbles[i].ActuationPoint == ActuationPoint.Thumb)
                        {
                            float abduction;
                            if (!isGettingCloseToGrasp)
                            {
                                abduction = _thimbles[i].Abduction.Value;
                            }
                            else
                            {
                                abduction = Mathf.Lerp(_fingersMixers[i].GetInputWeight(2), _thimbles[i].Abduction.Value,
                                Time.deltaTime * (_thimbles[i].SafeUnblockSeconds > 0 ? _fingersSlideSpeed : _fingersAnimationSpeed));
                            }

                            if (_slowFingerAnimationTime > 0)
                            {
                                abduction = Mathf.Lerp(_fingersMixers[i].GetInputWeight(2), _thimbles[i].Abduction.Value,
                                Time.deltaTime * (_thimbles[i].SafeUnblockSeconds > 0 ? _fingersSlideSpeed * _extraFingerSpeed : _thumbAnimationSpeed));
                            }

                            _fingersMixers[i].SetInputWeight(2, abduction);
                        }

                        if (_thimbles[i].SafeUnblockSeconds > 0)
                            _thimbles[i].SafeUnblockSeconds -= Time.deltaTime;
                    }
                }
            }


            // Slow finger animation countdown
            if (_slowFingerAnimationTime > 0)
                _slowFingerAnimationTime -= Time.deltaTime;
        }

        public void SetFingerAnimation(EasyGraspData data)
        {
            //Thumb
            _fingersMixers[0].SetInputWeight(0, 1 - data.thumbClosure);
            _fingersMixers[0].SetInputWeight(1, data.thumbClosure);
            _fingersMixers[0].SetInputWeight(2, data.thumbAbduction);

            //Index
            _fingersMixers[1].SetInputWeight(0, 1 - data.indexClosure);
            _fingersMixers[1].SetInputWeight(1, data.indexClosure);

            //Middle
            _fingersMixers[2].SetInputWeight(0, 1 - data.middleClosure);
            _fingersMixers[2].SetInputWeight(1, data.middleClosure);

            //Annular
            _fingersMixers[3].SetInputWeight(0, 1 - data.annularClosure);
            _fingersMixers[3].SetInputWeight(1, data.annularClosure);

            //Pinky
            _fingersMixers[4].SetInputWeight(0, 1 - data.pinkyClosure);
            _fingersMixers[4].SetInputWeight(1, data.pinkyClosure);
        }

        /// <summary>
        /// The OnDisable
        /// </summary>
        private void OnDisable()
        {
            // Disable animation
            _graph.Destroy();

            _graspingSystem.OnGraspingEvent -= UpdateFingerClosure;
        }


        /// <summary>
        /// Calibration manager signaling its success
        /// </summary>
        public void CalibrationSuccessful()
        {
            _thumbThimbleHaptic.RemoveAllEffects();
            _indexThimbleHaptic.RemoveAllEffects();
            _middleThimbleHaptic.RemoveAllEffects();

            if(WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
            {
                _annularThimbleHaptic.RemoveAllEffects();
                _pinkyThimbleHaptic.RemoveAllEffects();
                _palmThimbleHaptic.RemoveAllEffects();
            }
        }

        /// <summary>
        /// Enables/Disables thimble haptic objects at the hand.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableThimbleHaptic(bool enable)
        {
            _thumbThimbleHaptic.EnablingActuating(enable);
            _indexThimbleHaptic.EnablingActuating(enable);
            _middleThimbleHaptic.EnablingActuating(enable);

            if(WeArtController.Instance._deviceGeneration == DeviceGeneration.TD_Pro)
            {
                _annularThimbleHaptic.EnablingActuating(enable);
                _pinkyThimbleHaptic.EnablingActuating(enable);
                _palmThimbleHaptic.EnablingActuating(enable);
            }
        }

        /// <summary>
        /// Gets WeArtHapticObject related to actuation point.
        /// </summary>
        /// <param name="actuationPoint"></param>
        /// <returns></returns>
        public WeArtHapticObject GetHapticObject(ActuationPoint actuationPoint)
        {
            switch (actuationPoint)
            {
                case ActuationPoint.Thumb:
                    return _thumbThimbleHaptic;
                case ActuationPoint.Index:
                    return _indexThimbleHaptic;
                case ActuationPoint.Middle:
                    return _middleThimbleHaptic;
                case ActuationPoint.Palm:
                    return _palmThimbleHaptic;
                case ActuationPoint.Annular:
                    return _annularThimbleHaptic;
                case ActuationPoint.Pinky:
                    return _pinkyThimbleHaptic;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets WeArtThimbleTrackingObject related to actuation point.
        /// </summary>
        /// <param name="actuationPoint"></param>
        /// <returns></returns>
        public WeArtThimbleTrackingObject GetThimbleTrackingObject(ActuationPoint actuationPoint)
        {
            switch (actuationPoint)
            {
                case ActuationPoint.Thumb:
                    return _thumbThimbleTracking;
                case ActuationPoint.Index:
                    return _indexThimbleTracking;
                case ActuationPoint.Middle:
                    return _middleThimbleTracking;
                case ActuationPoint.Palm:
                    return null;
                case ActuationPoint.Annular:
                    return _annularThimbleTracking;
                case ActuationPoint.Pinky:
                    return _pinkyThimbleTracking;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Try to find a touchable object component on the collider
        /// </summary>
        /// <param name="collider">The collider<see cref="Collider"/>.</param>
        /// <param name="touchableObject">The touchableObject<see cref="WeArtTouchableObject"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        private static bool TryGetTouchableObjectFromCollider(Collider collider, out WeArtTouchableObject touchableObject)
        {
            touchableObject = collider.gameObject.GetComponent<WeArtTouchableObject>();
            return touchableObject != null;
        }

        /// <summary>
        /// Try to get WeArtGraspPose script from current touchable object
        /// </summary>
        /// <param name="gameObject">Current grasped object</param>
        /// <param name="customPoses">Output of this result</param>
        /// <returns></returns>
        private static bool TryGetCustomPosesFromTouchable(GameObject gameObject, out WeArtGraspPose customPoses)
        {
            customPoses = gameObject.GetComponent<WeArtGraspPose>();
            return customPoses != null;
        }


        #endregion

        #region Coroutines

        /// <summary>
        /// Interpolate the finger pose during grasp when using WeArtGraspPose.cs
        /// </summary>
        /// <param name="finger"></param>
        /// <param name="inputIndex"></param>
        /// <param name="closure"></param>
        /// <param name="lerpTime"></param>
        /// <returns></returns>
        private IEnumerator LerpPoses(AnimationLayerMixerPlayable finger, int inputIndex, float closure, float lerpTime)
        {
            float t = 0f;
            float to = finger.GetInputWeight(inputIndex);

            while (t < lerpTime)
            {
                float lerp;
                lerp = Mathf.Lerp(to, closure, t / lerpTime);
                t += Time.deltaTime;

                finger.SetInputWeight(inputIndex, lerp);
                yield return null;
            }
        }
        #endregion

    }
}