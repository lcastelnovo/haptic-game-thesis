using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class HandColliderPart : MonoBehaviour
{
    // Definisce le parti della mano
    public enum HandPart { BackHand, Thumb, Index, Middle, Annular, Pinky }
    public HandPart part;

    [Tooltip("Distanza massima per considerare il tocco")]
    public float touchDistance = 0.03f;

    private HandCollisionController controller;
    private Collider col;

    private readonly HashSet<Collider> currentlyTouching = new HashSet<Collider>();

    void Awake()
    {
        col = GetComponent<Collider>();
        controller = GetComponentInParent<HandCollisionController>();
        touchDistance = 0.03f; // corretto!
    }

    void FixedUpdate()
    {
        if (controller == null) return;

        // trova tutti i collider vicini
        Collider[] nearby = Physics.OverlapSphere(col.bounds.center, touchDistance * 2f, ~0, QueryTriggerInteraction.Collide);
        
        HashSet<Collider> detected = new HashSet<Collider>();

        // controlla quali oggetti sono effettivamente toccati
        foreach (Collider other in nearby)
        {
            // Ignora collider figli della mano SOLO se NON sono grabbable
            IGrabbable grabbable = other.GetComponentInParent<IGrabbable>();
            if (other.transform.IsChildOf(transform.root) && grabbable == null)
                continue;

            Vector3 closest = other.ClosestPoint(col.bounds.center);
            float dist = Vector3.Distance(col.bounds.center, closest);

            if (dist <= touchDistance)
            {
                detected.Add(other);

                if (!currentlyTouching.Contains(other))
                    controller.SetPartTouching(part, other, true);
            }
        }

        // oggetti che non sono più toccati
        foreach (Collider old in currentlyTouching)
        {
            if (!detected.Contains(old))
                controller.SetPartTouching(part, old, false);
        }

        currentlyTouching.Clear();
        foreach (var c in detected)
            currentlyTouching.Add(c);
    }
}
