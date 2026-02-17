using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ColliderHandle : MonoBehaviour
{
    public System.Action<Collider> TriggerEnter = delegate { };
    public System.Action<Collider> TriggerExit = delegate { };
    public System.Action<Collider> TriggerStay = delegate { };

    public System.Action<Collision> CollisionEnter = delegate { };
    public System.Action<Collision> CollisionExit = delegate { };

    private void OnCollisionEnter(Collision collision)
    {
        CollisionEnter?.Invoke(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        CollisionExit?.Invoke(collision);   
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TriggerStay(other);
    }

    private void OnTriggerExit(Collider other)
    {
        TriggerExit(other);
    }
}
