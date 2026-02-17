using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Core;
using WeArt.Components;
using Texture = WeArt.Core.Texture;

[RequireComponent(typeof(WeArtTouchableObject))]
public class WeArtAnchoredObject : MonoBehaviour
{

    /// <summary>
    /// Velocity used when grabbing the anchored object
    /// </summary>
    [SerializeField]
    internal float _graspingVelocity = 1f;

    /// <summary>
    /// In case of a screwing motion is needed, this property can allow for a better physical functionality
    /// </summary>
    [SerializeField]
    internal bool _isUsingScrewingOrigin = false;

    public bool IsUsingScrewingOrigin
    {
        get { return _isUsingScrewingOrigin; }
        set { _isUsingScrewingOrigin = value; }
    }

    public float GraspingVelocity
    {
        get { return _graspingVelocity; }
        set { _graspingVelocity = value; }
    }
}
