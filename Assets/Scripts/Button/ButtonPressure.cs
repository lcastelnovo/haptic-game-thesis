/*
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class ButtonPressure : MonoBehaviour
{
    [Header("Mano")]
    public GameObject handRoot;

    [Header("Pulsante")]
    public Transform visualModel;
    public float pressSpeed = 5f;
    public float releaseSpeed = 5f;
    public float maxPush = 0.02f;

    [Header("Evento")]
    public UnityEvent onPressed;

    [HideInInspector]
    public bool touching = false;

    private Vector3 startPos;
    private bool activated;
    private bool interactionLocked;

    void Start()
    {
        if (visualModel != null)
            startPos = visualModel.localPosition;
    }

    void FixedUpdate()
    {
        if (visualModel == null)
            return;

        // 🔹 Se non sto toccando → torna su
        if (!touching || interactionLocked)
        {
            visualModel.localPosition = Vector3.Lerp(
                visualModel.localPosition,
                startPos,
                releaseSpeed * Time.fixedDeltaTime);
            return;
        }

        float deepest = 0f;

        foreach (Collider c in handRoot.GetComponentsInChildren<Collider>())
        {
            Vector3 closest = c.ClosestPoint(visualModel.position);
            float depth = Vector3.Dot(visualModel.position - closest, transform.up);
            if (depth > 0f)
                deepest = Mathf.Max(deepest, depth);
        }

        float push = Mathf.Clamp(deepest, 0f, maxPush);
        Vector3 targetPos = startPos - transform.up * push;

        visualModel.localPosition = Vector3.Lerp(
            visualModel.localPosition,
            targetPos,
            pressSpeed * Time.fixedDeltaTime);

        // 🔥 attivazione
        if (push >= maxPush && !activated)
        {
            activated = true;
            interactionLocked = true;
            onPressed?.Invoke();
        }
        else if (push < maxPush)
        {
            activated = false;
        }
    }

    public void UnlockInteraction()
    {
        interactionLocked = false;
        activated = false;
    }
}
*/

using UnityEngine;
using UnityEngine.Events;

public class ButtonPressure : MonoBehaviour, IPressable
{
    [Header("Evento bottone")]
    public UnityEvent onPressed;
    
    // Stato del bottone
    public bool CanBePressed => true;

    public void Press()
    {
        if (!CanBePressed) return;

        onPressed?.Invoke();
    }

}
