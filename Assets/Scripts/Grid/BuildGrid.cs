using UnityEngine;

public class BuildGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize = 0.075f;   // lato cella
    public int gridWidth = 13;
    public int gridDepth = 8;
    public float tableHeight = 0.05f; // Y fisso

    private GameObject[,] grid;

    void Awake()
    {
        grid = new GameObject[gridWidth, gridDepth];
    }

    public void PlaceObject(GrabbableObject obj, Vector3 releasePosition)
    {
        // snap alla cella più vicina
        Vector3 snappedPos = GetSnappedPosition(releasePosition);

        // rotazione
        IGridOrientable orientable = obj.GetComponent<IGridOrientable>();
        Quaternion finalRot;
        Vector3 cellOffset = Vector3.zero;

        if (orientable != null)
        {
            finalRot = orientable.GetGridRotation();
            cellOffset = orientable.GetCellOffset();
        }
        else
        {
            finalRot = obj.GetInitialRotation();
        }

        snappedPos += cellOffset;


        // registra
        if (RegisterObject(obj.gameObject, snappedPos))
        {
            obj.transform.SetPositionAndRotation(snappedPos, finalRot);
        }
        else
        {
            Debug.Log("Cella occupata!");
            obj.transform.position = releasePosition;
        }
    }

    /// <summary>
    /// Restituisce la posizione snap della cella più vicina
    /// </summary>
    public Vector3 GetSnappedPosition(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);

        int x = Mathf.RoundToInt(local.x / cellSize);
        int z = Mathf.RoundToInt(local.z / cellSize);

        x = Mathf.Clamp(x, 0, gridWidth - 1);
        z = Mathf.Clamp(z, 0, gridDepth - 1);

        Vector3 snappedLocal = new Vector3(
            x * cellSize,
            tableHeight,
            z * cellSize
        );

        return transform.TransformPoint(snappedLocal);
    }

    /// <summary>
    /// Registra l'oggetto nella cella
    /// </summary>
    public bool RegisterObject(GameObject obj, Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        int x = Mathf.RoundToInt(local.x / cellSize);
        int z = Mathf.RoundToInt(local.z / cellSize);

        x = Mathf.Clamp(x, 0, gridWidth - 1);
        z = Mathf.Clamp(z, 0, gridDepth - 1);

        if (grid[x, z] != null)
        {
            Debug.Log("Cella già occupata!");
            return false;
        }

        grid[x, z] = obj;
        return true;
    }

    /// <summary>
    /// Rimuove un oggetto dalla griglia
    /// </summary>
    public void RemoveObject(GameObject obj)
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                if (grid[x, z] == obj)
                {
                    grid[x, z] = null;
                    return;
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                Vector3 localPos = new Vector3(x * cellSize, tableHeight, z * cellSize);
                Vector3 worldPos = transform.TransformPoint(localPos);
                Gizmos.DrawWireCube(worldPos, new Vector3(cellSize, 0.001f, cellSize));
            }
        }
    }
#endif
}
