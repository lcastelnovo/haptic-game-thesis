using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : MonoBehaviour, IGrabbable
{
    public TableScenarioManager scenarioManager;
    private Rigidbody rb;
    private Collider coll;
    private Transform currentParent;
    private Quaternion initialRotation;
    private Transform homeParent; // scenario o root in cui è nato

    [Header("Opzioni")]
    public bool canBeHeld = true;        // Decide se può essere afferrato
    public bool canBeDestroyed = true;   // Decide se può essere distrutto
    public Vector3 handOffset = Vector3.zero; // Offset quando viene preso

    [Header("Grid (opzionale)")]
    public BuildGrid buildGrid; // Se assegnato, l'oggetto si snapperà alla griglia

    // Implementazione della proprietà dell'interfaccia
    public bool CanBeInteractedWith => canBeHeld;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        coll = GetComponent<Collider>();

        initialRotation = transform.rotation;

        rb.mass = 1f;
        rb.drag = 0.5f;
        rb.angularDrag = 0.05f;
        rb.useGravity = true;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public Quaternion GetInitialRotation()
    {
        return initialRotation;
    }   

    public void SetHomeParent(Transform parent)
    {
        homeParent = parent;
    }

    /// <summary>Chiama la mano per afferrare l'oggetto</summary>
    public void OnGrab(Transform grabPoint)
    {
        if (!canBeHeld) return;

        // Se era nella griglia, rimuovilo
        if (buildGrid != null)
            buildGrid.RemoveObject(gameObject);

        currentParent = grabPoint;
        transform.SetParent(grabPoint);
        transform.localPosition = handOffset;
        rb.isKinematic = true;

        coll.isTrigger = true;
    }

    /// <summary>Rilascia l'oggetto dalla mano</summary>
    public void OnRelease(Vector3 releasePosition)
    {
        transform.SetParent(homeParent);

        // Se c'è una griglia, lascia che la griglia gestisca posizione e rotazione
        if (buildGrid != null)
        {
            buildGrid.PlaceObject(this, releasePosition);
        }
        else
        {
            transform.position = releasePosition;
        }

        rb.isKinematic = false;
        coll.isTrigger = false;
    }

}
