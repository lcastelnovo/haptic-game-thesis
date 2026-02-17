using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;

public class WeArtGraspCheck : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    //Delegates for the hand controller to use
    public Action<Collider> TriggerEnter = delegate { };

    public Action<Collider> TriggerStay = delegate { };

    public Action<Collider> TriggerExit = delegate { };

    private void OnTriggerEnter(Collider other)
    {
        TriggerEnter?.Invoke(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TriggerStay?.Invoke(other);
    }

    private void OnTriggerExit(Collider other)
    {
        TriggerExit?.Invoke(other);
    }

}
