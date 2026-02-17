using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;

public class WeArtChildCollider : MonoBehaviour
{
    /// <summary>
    /// Touchable object that holds the effect data
    /// </summary>
    private WeArtTouchableObject _parentTouchableObject;

    /// <summary>
    /// Defines the touchable object that holds the effect data
    /// </summary>
    public WeArtTouchableObject ParentTouchableObject
    {
        get {return _parentTouchableObject;}
        set {_parentTouchableObject = value;}
    }

    /// <summary>
    /// Method fired when the gameobject has a child added or removed from it
    /// </summary>
    private void OnTransformChildrenChanged()
    {
        StartCoroutine(DelayedChildrenCheck());
    }

    /// <summary>
    /// Next frame check for colliders and check the _touchableColliders list
    /// </summary>
    /// <returns></returns>
    private IEnumerator DelayedChildrenCheck()
    {
        yield return null;
        _parentTouchableObject.SetChildrenColliders(transform);
        _parentTouchableObject.CheckChildColliderList();
    }
}
