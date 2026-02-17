using UnityEngine;

public class TouchableObject : MonoBehaviour, ITouchable
{
    [Header("Prefab da creare")]
    public GameObject prefabToSpawn;

    [Header("Grid (opzionale)")]
    [SerializeField] public BuildGrid buildGrid;

    [Header("Spawn Parent")]
    public Transform spawnedObjectsRoot; // Dove mettere tutti gli oggetti spawnati

    public bool CanBeInteractedWith => true; // sempre interagibile

    // Metodo chiamato dal controller della mano
    public GrabbableObject OnTouch()
    {
        return SpawnCopy();
    }

    private GrabbableObject SpawnCopy()
    {
        Vector3 spawnPos = transform.position + transform.forward * 0.2f;

        // Creo il prefab come figlio del root della scena/contesto
        GameObject copy = Instantiate(prefabToSpawn);

        copy.transform.SetParent(spawnedObjectsRoot, true);

        // Assicurati che sia Grabbable
        GrabbableObject grabbable = copy.GetComponent<GrabbableObject>();
        if (grabbable == null)
            grabbable = copy.AddComponent<GrabbableObject>();

        // Imposto il parent “di casa” per il rilascio corretto
        grabbable.SetHomeParent(spawnedObjectsRoot);

        // Assegna la BuildGrid solo qui
        grabbable.buildGrid = buildGrid;

        return grabbable; // restituisce l'oggetto appena creato
    }
}


