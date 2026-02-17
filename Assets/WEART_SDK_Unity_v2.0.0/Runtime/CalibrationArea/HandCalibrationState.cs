using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandCalibrationState : MonoBehaviour
{
    [SerializeField] Renderer mesh;
    [SerializeField] Material idleMaterial;
    [SerializeField] Material calibrationMaterial;
    [SerializeField] Material successMaterial;
    [SerializeField] Material failedMaterial;
    [SerializeField] State state;

    public enum State { Idle, Calibrating, Success, Failed }
    public void SetState(State state)
    {
        switch (state)
        {
            case State.Idle:
                mesh.material = idleMaterial;
                break;
            case State.Calibrating:
                mesh.material = calibrationMaterial;
                break;
            case State.Success:
                mesh.material = successMaterial;
                break;
            case State.Failed:
                mesh.material = failedMaterial;
                break;
        }
    }

    public State GetCurrentState()
    {
        return state;
    }
}
