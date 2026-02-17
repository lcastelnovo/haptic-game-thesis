using UnityEngine;
public class ThumbController : MonoBehaviour
{
    public Transform proximal;
    public Transform distal;
    public float closeAmount = 0f;

    // Angoli di chiusura specifici per il pollice
    public Vector3 closedProximalEuler = new Vector3(50f, 0.345f, -4.911f);
    public Vector3 closedDistalEuler = new Vector3(50f, 0, -3.526f);

    private Quaternion initialProximalRot;
    private Quaternion initialDistalRot;

    void Start()
    {
        initialProximalRot = proximal.localRotation;
        initialDistalRot = distal.localRotation;
    }

    void Update()
    {
        // Calcola le rotazioni basate su closeAmount
        var targetProximal = Quaternion.Euler(closedProximalEuler);
        var targetDistal = Quaternion.Euler(closedDistalEuler);

        // Interpola tra la posizione iniziale e quella chiusa
        proximal.localRotation = Quaternion.Slerp(initialProximalRot, targetProximal, closeAmount);
        distal.localRotation = Quaternion.Slerp(initialDistalRot, targetDistal, closeAmount);
    }
}
