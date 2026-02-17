using UnityEngine;

public class HandGrabController : MonoBehaviour
{
    [Header("Riferimenti")]
    public Transform grabPoint;   // Palmo / punto di presa
    public float grabRadius = 0.1f; // Raggio di rilevamento
    public float rotationStep = 90f; // Step di rotazione

    private IGrabbable currentGrabbable;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && currentGrabbable == null)
            TryInteract();

        if (Input.GetMouseButtonUp(0) && currentGrabbable != null)
            Release();

        if (Input.GetMouseButtonDown(1) && currentGrabbable != null)
            TryDestroy();

        // Rotazione in mano
        if (currentGrabbable != null)
            RotateInHand();
    }

    void TryInteract()
    {
        Collider[] hits = Physics.OverlapSphere(grabPoint.position, grabRadius);
        if (hits.Length == 0)
            Debug.Log("Nessun collider rilevato nel raggio");

        foreach (Collider c in hits)
        {
            // Controlla IPressable (bottone)
            IPressable pressable = c.GetComponentInParent<IPressable>();
            if (pressable != null && pressable.CanBePressed)
            {
                pressable.Press();
                return;
            }

            // Controlla IGrabbable
            IGrabbable grabbable = c.GetComponentInParent<IGrabbable>();
            if (grabbable != null && grabbable.CanBeInteractedWith)
            {
                currentGrabbable = grabbable;
                grabbable.OnGrab(grabPoint);
                return;
            }

            // Controlla ITouchable
            ITouchable touchable = c.GetComponentInParent<ITouchable>();
            if (touchable != null && touchable.CanBeInteractedWith)
            {
                GrabbableObject created = touchable.OnTouch();
                if (created != null)
                {
                    currentGrabbable = created;
                    created.OnGrab(grabPoint);
                }
                return;
            }
        }
    }

    void Release()
    {
        if (currentGrabbable == null) return;

        currentGrabbable.OnRelease(grabPoint.position);
        currentGrabbable = null;
    }

    private void TryDestroy()
    {
        if (currentGrabbable is GrabbableObject grabbable && grabbable.canBeDestroyed)
        {
            Destroy(grabbable.gameObject);
            currentGrabbable = null;
        }
    }

    private void RotateInHand()
    {
        // Controlla se l'oggetto implementa IGridOrientable
        GrabbableObject obj = currentGrabbable as GrabbableObject;
        if (obj == null) return;

        IGridOrientable orientable = obj.GetComponent<IGridOrientable>();
        if (orientable == null) return;

        float zRotation = 0f;
        if (Input.GetKeyDown(KeyCode.S)) zRotation = rotationStep;
        if (Input.GetKeyDown(KeyCode.D)) zRotation = -rotationStep;

        if (!Mathf.Approximately(zRotation, 0f))
        {
            obj.transform.Rotate(0f, 0f, zRotation, Space.Self);
            
            (obj.GetComponent<TriangleGridOrientation>())?.AddRotation(zRotation);
        }
    }
}