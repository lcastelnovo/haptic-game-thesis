using System;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Core;
using WeArt.Messages;
using static WeArt.Core.WeArtUtility;
using Texture = WeArt.Core.Texture;

namespace WeArt.Components
{
    /// <summary>
    /// This component controls the haptic actuators of one or more hardware thimbles.
    /// The haptic control can be issued:
    /// 1) Manually from the Unity inspector
    /// 2) When a <see cref="WeArtTouchableObject"/> collides with this object
    /// 3) On custom haptic effects added or removed
    /// 4) On direct value set, through the public properties
    /// </summary>
    public class WeArtHapticObject : MonoBehaviour
    {
        /// <summary>
        /// The side of the hand that defines the hand controller
        /// </summary>
        [SerializeField]
        internal HandSideFlags _handSides = HandSideFlags.None;

        /// <summary>
        /// Actuation points that will get called
        /// </summary>
        [SerializeField]
        internal ActuationPointFlags _actuationPoints = ActuationPointFlags.None;

        /// <summary>
        /// Target used for calculating the dynamic force
        /// </summary>
        [SerializeField]
        internal Transform _distanceForceTarget;

        /// <summary>
        /// Temperature sent to the touchdiver
        /// </summary>
        [SerializeField]
        internal Temperature _temperature = Temperature.Default;

        /// <summary>
        /// Force sent to the touchdiver
        /// </summary>
        [SerializeField]
        internal Force _force = Force.Default;

        /// <summary>
        /// Texture sent to the touchdiver
        /// </summary>
        [SerializeField]
        internal Texture _texture = Texture.Default;

        /// <summary>
        /// The current active effect appied to thimbles
        /// </summary>
        [NonSerialized]
        internal IWeArtEffect _activeEffect;

        /// <summary>
        /// The hand controller that owns this haptic object
        /// </summary>
        private WeArtHandController _handController;

        /// <summary>
        /// List of touchable objects in contact with the haptic object
        /// </summary>
        private List<WeArtTouchableObject> _touchedObjects = new List<WeArtTouchableObject>();

        //Delegates for Trigger events
        public Action<Collider> TriggerEnter = delegate { };

        public Action<Collider> TriggerStay = delegate { };

        public Action<Collider> TriggerExit = delegate { };

        /// <summary>
        /// The force of touchable object that the haptic object is in contact with without any extra calculations
        /// </summary>
        private float _graspedTouchableObjectForce;

        /// <summary>
        /// The touchable objeect that hand controller is grasping if this finger is touching it
        /// </summary>
        private WeArtTouchableObject _graspedObject;

        /// <summary>
        /// If the parent hand controller is grasping something and this haptic object is touchingit
        /// </summary>
        private bool _isGrasping = false;

        /// <summary>
        /// Defines if it is inside a trigger touchable object
        /// </summary>
        private bool _isAffectedByTrigger = false;

        /// <summary>
        /// Number of touchable objects in contact with the haptic objects that are not triggers
        /// </summary>
        private int _nonTriggerTouchedObjects = 0;

        // Used for texture velocity calculations
        private Vector3 _lastPosition;
        private float _lastTime;

        /// <summary>
        /// Defines if the haptic objects is used by a hand controller
        /// </summary>
        private bool _isUsedByHandController = false;

        /// <summary>
        /// Defines if the haptic object sends data to the Touch Diver
        /// </summary>
        private bool isActuating = false;

        /// <summary>
        /// Last touchable object to add effect
        /// </summary>
        private WeArtTouchableObject _lastTocuhableObject;

        /// <summary>
        /// In case of the hand controller is grasping something, if the  finger touches it, set the touched object
        /// </summary>
        /// <param name="tObject"></param>
        
        public WeArtTouchableObject GraspedObject {
            get { return _graspedObject;}
            set 
            {
                _graspedObject = value;
            }
        }

        /// <summary>
        /// Set if the setup is based on a hand controller
        /// </summary>
        /// <param name="value"></param>
        public void SetIsUsedByController(bool value)
        {
            _isUsedByHandController = value;
        }

        /// <summary>
        /// Get and set the hand controller
        /// </summary>
        public WeArtHandController HandController
        {
            get { return _handController; }
            set { _handController = value; }
        }

        /// <summary>
        /// Get touched object, grasped by the hand controller 
        /// </summary>
        /// <returns></returns>
        public WeArtTouchableObject GetGrabbedTouchableObject()
        {
            return _graspedObject;
        }


        /// <summary>
        /// Get if the haptic object is inside a trigger touchable object
        /// </summary>
        /// <returns></returns>
        public bool GetIsAffectedByTrigger()
        {
            return _isAffectedByTrigger;
        }

        /// <summary>
        /// Called when the resultant haptic effect changes because of the influence
        /// caused by the currently active effects
        /// </summary>
        public event Action OnActiveEffectsUpdate;

        /// <summary>
        /// Calculates the dynamic force
        /// </summary>
        /// <param name="touchable"></param>
        /// <returns></returns>
        public Force CalculatePhysicalForce(WeArtTouchableObject touchable)
        {
            float pressure_force;

            _graspedTouchableObjectForce = touchable.Stiffness.Value;

            Force dynamicForce = Force.Default;
            dynamicForce.Active = touchable.Stiffness.Active;

            if (_distanceForceTarget != null)
            {
                float _distance = Vector3.Distance(transform.position, _distanceForceTarget.position);
                // This will calculate the maximum distance to cover before saturation, basing on act point, stiffness. 
                float max_distance_covered = GetInitialMaxDistanceForActPoint(_actuationPoints, _graspedTouchableObjectForce, _isGrasping);

                if (max_distance_covered > 0) // Deviding by zero exception could occur
                {
                    pressure_force = _distance / max_distance_covered;
                    pressure_force = Mathf.Clamp(pressure_force, 0.2f, 1.0f);
                }
                else 
                    pressure_force = 0.0f;
                if (_isAffectedByTrigger)
                    pressure_force = _graspedTouchableObjectForce;
            }
            else
            {
                pressure_force = _graspedTouchableObjectForce;
            }

            dynamicForce.Value = pressure_force;
            return dynamicForce;
        }

        private void Start()
        {
            Vector3 _lastPosition = transform.position;
            float _lastTime = Time.time;
            
            EnablingActuating(WeArtController.Instance.IsCalibrated);
        }

        private void Update()
        {
            if (_isGrasping)
            {
                _isAffectedByTrigger = false;
            }
        }

        /// <summary>
        /// The hand sides to control with this component
        /// </summary>
        public HandSideFlags HandSides
        {
            get => _handSides;
            set
            {
                if (value != _handSides)
                {
                    var sidesToStop = _handSides ^ value & _handSides;
                    _handSides = sidesToStop;
                    StopControl();

                    _handSides = value;
                    StartControl();
                }
            }
        }

        /// <summary>
        /// The thimbles to control with this component
        /// </summary>
        public ActuationPointFlags ActuationPoints
        {
            get => _actuationPoints;
            set
            {
                if (value != _actuationPoints)
                {
                    var pointsToStop = _actuationPoints ^ value & _actuationPoints;
                    _actuationPoints = pointsToStop;
                    StopControl();

                    _actuationPoints = value;
                    StartControl();
                }
            }
        }

        /// <summary>
        /// Sets and Gets if the hand haptic objeect if it is currently grasping
        /// </summary>
        public bool IsGrasping
        {
            get => _isGrasping;
            set 
            { 
                _isGrasping = value;

                if (_texture.Active)
                    SendSetTexture();
                else
                    SendStopTexture();
            }
        }

        /// <summary>
        /// The current temperature of the specified thimbles
        /// </summary>
        public Temperature Temperature
        {
            get => _temperature;
            set
            {
                if (!_temperature.Equals(value))
                {
                    _temperature = value;

                    if (value.Active)
                        SendSetTemperature();
                    else
                        SendStopTemperature();
                }
            }
        }

        /// <summary>
        /// The current pressing force of the specified thimbles
        /// </summary>
        public Force Force
        {
            get => _force;
            set
            {
                if (!_force.Equals(value))
                {
                    _force = value;

                    if (value.Active)
                        SendSetForce();
                    else
                        SendStopForce();
                }
            }
        }

        /// <summary>
        /// The current texture feeling applied on the specified thimbles
        /// </summary>
        public Texture Texture
        {
            get => _texture;
            set
            {
                if (!_texture.Equals(value))
                {
                    _texture = value;
                    if (value.Active)
                        SendSetTexture();
                    else
                        SendStopTexture();
                }
            }
        }

        /// <summary>
        /// The currently active effects on this object
        /// </summary>
        public IWeArtEffect ActiveEffect => _activeEffect;

        /// <summary>
        /// The currently active effects on this object
        /// </summary>
        public IReadOnlyList<WeArtTouchableObject> TouchedObjects => _touchedObjects;

        /// <summary>
        /// Add to the touched objects list only if the object does not exist already in the list
        /// </summary>
        /// <param name="obj"></param>
        public void AddTouchedObject(WeArtTouchableObject obj)
        {
            if(!_touchedObjects.Contains(obj))
            {
                _touchedObjects.Add(obj);
            }
        }

        /// <summary>
        /// Removes form the touched objects list only if the object exists already in the list
        /// </summary>
        /// <param name="obj"></param>
        public void RemoveTouchedObject(WeArtTouchableObject obj)
        {
            if (_touchedObjects.Contains(obj))
            {
                _touchedObjects.Remove(obj);
            }
        }

        /// <summary>
        /// Clears all touched objects
        /// </summary>
        public void ClearTouchedObjects()
        {
            _touchedObjects.Clear();
        }

        /// <summary>
        /// When the touchable object gets disabled, signal to the hand controller and request a release
        /// </summary>
        /// <param name="touchable"></param>
        public void UnblockFingerOnDisable(WeArtTouchableObject touchable)
        {
            if(_handController != null)
            {
                _handController.GetGraspingSystem().UnblockFingerOnDisable(this, touchable);
            }
        }

        /// <summary>
        /// Adds a haptic effect to this object. This effect will have an influence
        /// as long as it is not removed or the haptic properties are programmatically
        /// forced to have a specified value.
        /// </summary>
        /// <param name="effect">The haptic effect to add to this object</param>
        /// <param name="touchable">touchable object that holds the effect</param>
        public void AddEffect(IWeArtEffect effect, WeArtTouchableObject touchable = null)
        {
            if (_activeEffect != null)
                _activeEffect.OnUpdate -= UpdateEffects;

            _activeEffect = effect;
            _graspedTouchableObjectForce = effect.Force.Value;
            effect.OnUpdate += UpdateEffects;

            if (touchable != null)
            {
                touchable.TouchedHapticsEffects[this] = (WeArtTouchEffect)effect;
                touchable.CallOnAffectedHapticObjectsUpdate();
                _lastTocuhableObject = touchable;
            }

            UpdateEffects();
        }

        /// <summary>
        /// Removes a haptic effect from the set of influencing effects
        /// </summary>
        /// <param name="effect">The haptic effect to remove</param>
        public void RemoveEffect()
        {
            if (_activeEffect != null)
                _activeEffect.OnUpdate -= UpdateEffects;

            _lastTocuhableObject = null;

            if (_touchedObjects.Count > 0)
            {
                if (!_isGrasping)
                {
                    WeArtTouchEffect touchEffect = new WeArtTouchEffect();
                    touchEffect.Set(_touchedObjects[0].Temperature, CalculatePhysicalForce(_touchedObjects[0]), _touchedObjects[0].Texture, new WeArtTouchEffect.WeArtImpactInfo()
                    {
                        Position = transform.position,
                        Time = Time.time
                    });
                    AddEffect(touchEffect, _touchedObjects[0]);
                }
            }
            else
            {
                WeArtTouchEffect touchEffect = new WeArtTouchEffect();
                touchEffect.Set(Temperature.Default, Force.Default, Texture.Default, null);
                AddEffect(touchEffect);
            }

            UpdateEffects();
        }

        /// <summary>
        /// On realease removes all touchable objects and returns to the starting state
        /// </summary>
        /// <param name="effect"></param>
        public void RemoveAllEffects()
        {
            if (_activeEffect != null)
                _activeEffect.OnUpdate -= UpdateEffects;

            _touchedObjects.Clear();
            WeArtTouchEffect touchEffect = new WeArtTouchEffect();
            touchEffect.Set(Temperature.Default, Force.Default, Texture.Default, null);
            AddEffect(touchEffect);

            UpdateEffects();
        }

        /// <summary>
        /// Get the active effect
        /// </summary>
        /// <returns></returns>
        public IWeArtEffect GetEffect()
        {
            return _activeEffect;
        }

        /// <summary>
        /// Internally updates the resultant haptic effect caused by the set of active effects.
        /// </summary>
        public void UpdateEffects()
        {
            var lastTemperature = Temperature.Default;
            if (_activeEffect != null)
            {
                lastTemperature = _activeEffect.Temperature;
            }

            Temperature = lastTemperature;

            var lastForce = Force.Default;
            if (_activeEffect != null)
            {
                lastForce = _activeEffect.Force;
            }
            Force = lastForce;

            var lastTexture = Texture.Default;
            if (_activeEffect != null)
            {
                lastTexture = _activeEffect.Texture;
            }

            if (Texture.Active == false && lastTexture.Active == false)
            {
                // Don't change texture
            }
            else
            {
                Texture = lastTexture;
            }

            OnActiveEffectsUpdate?.Invoke();
        }

        /// <summary>
        /// Initialize
        /// </summary>
        private void Init()
        {
            var client = WeArtController.Instance.Client;
            client.OnConnectionStatusChanged -= OnConnectionChanged;
            client.OnConnectionStatusChanged += OnConnectionChanged;
        }

        /// <summary>
        /// The OnEnable
        /// </summary>
        private void OnEnable()
        {
            Init();
        }

        /// <summary>
        /// The OnCollisionEnter.
        /// </summary>
        /// <param name="collision">The collision<see cref="Collision"/>.</param>
        private void OnCollisionEnter(Collision collision) => OnTriggerEnter(collision.collider);

        /// <summary>
        /// The OnCollisionStay.
        /// </summary>
        /// <param name="collision">The collision<see cref="Collision"/>.</param>
        private void OnCollisionStay(Collision collision) => OnTriggerStay(collision.collider);

        /// <summary>
        /// The OnCollisionExit.
        /// </summary>
        /// <param name="collision">The collision<see cref="Collision"/>.</param>
        private void OnCollisionExit(Collision collision) => OnTriggerExit(collision.collider);

        /// <summary>
        /// The OnColliderEnter.
        /// </summary>
        /// <param name="collider">The collider<see cref="Collider"/>.</param>
        private void OnTriggerEnter(Collider collider)
        {
            if (!_isUsedByHandController)
            {
                HandleOnTriggerEnter(collider);
            }
            TriggerEnter?.Invoke(collider);
        }


        /// <summary>
        /// Common method for handleing a collider that entered the haptic object
        /// </summary>
        /// <param name="collider"></param>
        public void HandleOnTriggerEnter(Collider collider)
        {
            if (TryGetTouchableObjectFromCollider(collider, out var touchable))
            {
                if (!_isGrasping)
                {
                    var effect = new WeArtTouchEffect();

                    effect.Set(touchable.Temperature, CalculatePhysicalForce(touchable), touchable.Texture, new WeArtTouchEffect.WeArtImpactInfo()
                    {
                        Position = transform.position,
                        Time = Time.time
                    });

                    AddEffect(effect, touchable);
                    AddTouchedObject(touchable);
                }

                if (!touchable.TouchedHapticsEffects.ContainsKey(this))
                {
                    touchable.TouchedHapticsEffects[this] = null;
                    touchable.CallOnAffectedHapticObjectsUpdate();
                }
            }
        }

        /// <summary>
        /// The OnColliderStay.
        /// </summary>
        /// <param name="collider">The collider<see cref="Collider"/>.</param>
        private void OnTriggerStay(Collider collider)
        {
            if (!_isUsedByHandController)
            {
                HandleOnTriggerStay(collider);
            }
            TriggerStay?.Invoke(collider);
        }

        /// <summary>
        /// Common method for handleing a collider that stays in the haptic object
        /// </summary>
        /// <param name="collider"></param>
        public void HandleOnTriggerStay(Collider collider)
        {
            if (TryGetTouchableObjectFromCollider(collider, out var touchable))
            {
                if (!_isGrasping)
                {
                    if (_touchedObjects.Count > 0)
                    {
                        if (_lastTocuhableObject == touchable)
                        {
                            if (touchable.TouchedHapticsEffects.TryGetValue(this, out var effect))
                            {
                                effect.Set(touchable.Temperature, CalculatePhysicalForce(touchable), touchable.Texture, new WeArtTouchEffect.WeArtImpactInfo()
                                {
                                    Position = transform.position,
                                    Time = Time.time
                                });
                            }
                        }
                    }

                }
                else
                {
                    if (touchable == _graspedObject)
                    {
                        if (touchable.TouchedHapticsEffects.TryGetValue(this, out var effect))
                        {
                            effect.Set(touchable.Temperature, CalculatePhysicalForce(touchable), touchable.Texture, new WeArtTouchEffect.WeArtImpactInfo()
                            {
                                Position = transform.position,
                                Time = Time.time
                            });

                            if (_lastTocuhableObject == null)
                            {
                                AddEffect(effect,_graspedObject);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The OnColliderExit.
        /// </summary>
        /// <param name="collider">The collider<see cref="Collider"/>.</param>
        private void OnTriggerExit(Collider collider)
        {
            if (!_isUsedByHandController)
            {
                HandleOnTriggerExit(collider);
            }
            TriggerExit?.Invoke(collider);

        }

        /// <summary>
        /// Common method for handleing a collider that exits the haptic object
        /// </summary>
        /// <param name="collider"></param>
        public void HandleOnTriggerExit(Collider collider)
        {

            if (TryGetTouchableObjectFromCollider(collider, out var touchable))
            {
                if (!IsGrasping)
                {
                }
                    RemoveTouchedObject(touchable);
                    RemoveEffect();
                touchable.TouchedHapticsEffects.Remove(this);
                touchable.CallOnAffectedHapticObjectsUpdate();
            }
        }

        public void EnablingActuating(bool enable)
        {
            isActuating = enable;
        }
        
        /// <summary>
        /// Checks if there are touchable objects inside a trigger touchable objects and applies them instead of the trigger
        /// </summary>
        public void UpdateIfPositionedInTrigger()
        {
            if (_isGrasping)
                return;

            if (_touchedObjects.Count > 0)
            {

                _nonTriggerTouchedObjects=0;

                foreach(var obj in _touchedObjects)
                {
                    if(obj.GetFirstCollider().isTrigger == false)
                    {
                        _nonTriggerTouchedObjects +=1;
                    }
                }

                if (_nonTriggerTouchedObjects > 0)
                {
                    bool foundNonTriggerTouchable = false;
                    foreach (var obj in _touchedObjects)
                    {
                        if (!obj.GetFirstCollider().isTrigger)
                        {
                            _isAffectedByTrigger = false;
                            foundNonTriggerTouchable = true;
                            break;
                        }
                    }

                    if (!foundNonTriggerTouchable)
                    {
                        if (_touchedObjects.Count > _nonTriggerTouchedObjects)
                        {
                            foreach (var obj in _touchedObjects)
                            {
                                if (obj.GetFirstCollider().isTrigger)
                                {
                                    _isAffectedByTrigger = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    _isAffectedByTrigger = true;
                }
            }
            else
            { 
                _isAffectedByTrigger = false; 
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gameObject.scene.IsValid())
                Init();
        }
#endif

        internal void OnConnectionChanged(bool connected)
        {
            if (connected)
                StartControl();
        }

        internal void StartControl()
        {
            if (Temperature.Active)
                SendSetTemperature();

            if (Force.Active)
                SendSetForce();

            if (Texture.Active)
                SendSetTexture();
        }

        internal void StopControl()
        {
            if (Temperature.Active)
                SendStopTemperature();

            if (Force.Active)
                SendStopForce();

            if (Texture.Active)
                SendStopTexture();
        }

        // Messages
        private void SendSetTemperature() => SendMessage((handSide, actuationPoint) => new SetTemperatureMessage()
        {
            Temperature = _temperature.Value,
            HandSide = handSide,
            ActuationPoint = actuationPoint
        });

        private void SendStopTemperature() => SendMessage((handSide, actuationPoint) => new StopTemperatureMessage()
        {
            HandSide = handSide,
            ActuationPoint = actuationPoint
        });

        private void SendSetForce() => SendMessage((handSide, actuationPoint) => new SetForceMessage()
        {
            Force =new float[] { _force.Value, _force.Value, _force.Value },
            HandSide = handSide,
            ActuationPoint = actuationPoint
        });

        private void SendStopForce() => SendMessage((handSide, actuationPoint) => new StopForceMessage()
        {
            HandSide = handSide,
            ActuationPoint = actuationPoint
        });

        private void SendSetTexture() => SendMessage((handSide, actuationPoint) => new SetTextureMessage()
        {
            TextureIndex = (int)_texture.TextureType,
            TextureVelocity = new float[] { WeArtConstants.defaultTextureVelocity_X, WeArtConstants.defaultTextureVelocity_Y,
               _texture.ForcedVelocity?WeArtConstants.defaultTextureVelocity_X: (_isGrasping? 0: _texture.Velocity) },
            TextureVolume = _texture.Volume,
            HandSide = handSide,
            ActuationPoint = actuationPoint
        });

        private void SendStopTexture() => SendMessage((handSide, actuationPoint) => new StopTextureMessage()
        {
            HandSide = handSide,
            ActuationPoint = actuationPoint
        });

        private void SendMessage(Func<HandSide, ActuationPoint, IWeArtMessage> createMessage)
        {
            if (!isActuating) return;

            if (!WeArtController.Instance) return;

            foreach (var handSide in WeArtConstants.HandSides)
                if (HandSides.HasFlag((HandSideFlags)(1 << (int)handSide)))
                    foreach (var actuationPoint in WeArtConstants.ActuationPoints)
                        if (ActuationPoints.HasFlag((ActuationPointFlags)(1 << (int)actuationPoint)))
                        {
                            if (WeArtController.Instance._deviceGeneration != DeviceGeneration.TD_Pro)
                            {
                                if (actuationPoint == ActuationPoint.Annular || actuationPoint == ActuationPoint.Pinky || actuationPoint == ActuationPoint.Palm) continue;
                            }

                            WeArtController.Instance.Client.SendMessage(createMessage(handSide, actuationPoint));
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
            if(collider.gameObject.GetComponent<WeArtChildCollider>() != null)
            {
                touchableObject = collider.gameObject.GetComponent<WeArtChildCollider>().ParentTouchableObject;
            }

            return touchableObject != null;
        }
    }
}