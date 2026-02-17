using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using Texture = WeArt.Core.Texture;

[RequireComponent(typeof(WeArtTouchableObject))]
public class WeArtVolumeEffects: MonoBehaviour
{
    /// <summary>
    /// List of touchable objects to ignore when applying effects on the touchable onjects that enter this volume
    /// </summary>
    [SerializeField] 
    internal List<WeArtTouchableObject> _ignoreTouchableObjects = new List<WeArtTouchableObject>();

    [SerializeField]
    internal bool _overrideTemperature = true;

    [SerializeField]
    internal bool _overrideForce = false;

    [SerializeField]
    internal bool _overrideTexture = true;

    /// <summary>
    /// Touchable object component that is present on the same game object
    /// </summary>
    private WeArtTouchableObject _touchableObject;

    /// <summary>
    /// List of touchable objects affected by this volume
    /// </summary>
    private List<TouchableInVolume> _touchablesInsideTheVolume = new List<TouchableInVolume>();

    private struct TouchableInVolume
    {
        public WeArtTouchableObject touchableObject;
        public Temperature initialTemperature; 
        public Force initialStiffness;
        public Texture initialTexture;
    }

    private void Awake()
    {
        _touchableObject = GetComponent<WeArtTouchableObject>();
    }

    private void OnDisable()
    {
        ReverseTouchableObjectsToOriginal();
    }

    private void OnDestroy()
    {
        ReverseTouchableObjectsToOriginal();
    }

    /// <summary>
    /// Reverse all touchable objects to their original effect
    /// </summary>
    private void ReverseTouchableObjectsToOriginal()
    {
        foreach (var touch in _touchablesInsideTheVolume)
        {
            if (touch.touchableObject == null) continue;
            
            touch.touchableObject.Temperature = touch.initialTemperature;
            touch.touchableObject.Stiffness = touch.initialStiffness;
            touch.touchableObject.Texture = touch.initialTexture;
        }
        _touchablesInsideTheVolume.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetTouchableObjectFromCollider(other, out WeArtTouchableObject touchable)) return;

        foreach (var item in _ignoreTouchableObjects)
        {
            if (item == touchable)
                return;
        }

        foreach (var touch in _touchablesInsideTheVolume)
        {
            if (touch.touchableObject == touchable) return;
        }
        
        _touchablesInsideTheVolume.Add(new TouchableInVolume
        {
            touchableObject = touchable,
            initialTemperature = (Temperature) touchable.Temperature.Clone(),
            initialStiffness = (Force)touchable.Stiffness.Clone(),
            initialTexture = (Texture)touchable.Texture.Clone()
        });

        if(_overrideTemperature)
        touchable.Temperature = _touchableObject.Temperature;

        if(_overrideForce)
        touchable.Stiffness = _touchableObject.Stiffness;

        if(_overrideTexture)
        touchable.Texture = _touchableObject.Texture;
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!TryGetTouchableObjectFromCollider(other, out WeArtTouchableObject touchable)) return;

        foreach (var item in _ignoreTouchableObjects)
        {
            if (item == touchable)
                return;
        }

        foreach (var touch in _touchablesInsideTheVolume)
        {
            if (touch.touchableObject != touchable) continue;
            
            touchable.Temperature = touch.initialTemperature;
            touchable.Texture = touch.initialTexture;
            touchable.Stiffness = touch.initialStiffness;
            
            _touchablesInsideTheVolume.Remove(touch);
            return;
        }
    }
    
    private static bool TryGetTouchableObjectFromCollider(Collider collider, out WeArtTouchableObject touchableObject)
    {
        touchableObject = collider.gameObject.GetComponent<WeArtTouchableObject>();

        if (collider.gameObject.GetComponent<WeArtChildCollider>() != null)
        {
            touchableObject = collider.gameObject.GetComponent<WeArtChildCollider>().ParentTouchableObject;
        }

        return touchableObject != null;
    }
    
}
