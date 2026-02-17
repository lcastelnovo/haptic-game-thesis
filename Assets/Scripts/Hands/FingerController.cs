using UnityEngine;

public class FingerController : MonoBehaviour
{
    public Transform proximal;     // falange prossimale
    public Transform intermediate; // falangina
    public Transform distal;       // falangetta

    // angoli massimi di flessione per ogni articolazione
    public float maxProximalAngle = 70f;
    public float maxIntermediateAngle = 90f;
    public float maxDistalAngle = 90f;

    private Quaternion initialProximalRot;
    private Quaternion initialIntermediateRot;
    private Quaternion initialDistalRot;

    public float closeAmount = 0f; // 0 aperto, 1 chiuso

    void Start()
    {
        initialProximalRot = proximal.localRotation;
        initialIntermediateRot = intermediate.localRotation;
        initialDistalRot = distal.localRotation;
    }

    void Update()
    {
        // calcola le rotazioni basate su closeAmount
        Quaternion closedProximal = Quaternion.Euler(maxProximalAngle, 0, 0);
        Quaternion closedIntermediate = Quaternion.Euler(maxIntermediateAngle, 0, 0);
        Quaternion closedDistal = Quaternion.Euler(maxDistalAngle, 0, 0);

        // interpola tra la posizione iniziale e quella chiusa
        proximal.localRotation = Quaternion.Slerp(initialProximalRot, closedProximal, closeAmount);
        intermediate.localRotation = Quaternion.Slerp(initialIntermediateRot, closedIntermediate, closeAmount);
        distal.localRotation = Quaternion.Slerp(initialDistalRot, closedDistal, closeAmount);
    }
}
