using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetRigidbodies : MonoBehaviour
{
    [SerializeField]
    private List<Transform> _bodies = new List<Transform>();

    private List<Vector3> _positions = new List<Vector3>();
    private List<Vector3> _rotations = new List<Vector3>();

    void Start()
    {
        foreach (var b in _bodies)
        {
            _positions.Add(b.position);
            _rotations.Add(b.rotation.eulerAngles);
        }
    }

    public void ResetRigidBodies()
    {

        for (int i = 0; i < _bodies.Count; i++)
        {
            if (_bodies[i]!= null)
            {
                Rigidbody rB = _bodies[i].GetComponent<Rigidbody>();
                if (rB != null)
                {
                    rB.angularVelocity = Vector3.zero;
                    rB.velocity = Vector3.zero;
                }

                _bodies[i].position = _positions[i];
                _bodies[i].rotation = Quaternion.Euler(_rotations[i]);
            }
        }
    }
   
    void Update()
    {
        
    }
}
