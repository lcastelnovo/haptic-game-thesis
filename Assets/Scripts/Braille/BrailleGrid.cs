using UnityEngine;
using System.Collections.Generic;

public class BrailleGrid : MonoBehaviour
{
    [Header("Prefabs")]
    public BrailleCell cellPrefab;
    public GameObject dotPrefab;
    public BrailleWordProvider wordProvider;

    [Header("Grid size")]
    public int rows = 3;
    public int columns = 4;

    [Header("Grid height")]
    public float tableHeight = 0.85f;

    private const float CELL_WIDTH = 0.2f;   // X
    private const float CELL_HEIGHT = 0.3f;  // Z

    private List<BrailleCell> cells = new List<BrailleCell>();

    public int Columns => columns;
    public int Rows => rows;

    public void ShowAllDots()
    {
        Clear();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                BrailleCell cell = SpawnCell(c, r);

                cell.InitializeDots();
            }
        }
    }

    public void ShowWord(string input)
    {
        Clear();

        Debug.Log($"ShowWord chiamato con: {input}");

        if (string.IsNullOrEmpty(input))
            return;

        // Encode con il database
        List<bool[]> brailleCells = BrailleDatabase.Encode(input);

        /*for (int i = 0; i < brailleCells.Count; i++)
        {
            string patternStr = string.Join(",", brailleCells[i]);
            Debug.Log($"Cella {i}: {patternStr}");
        }*/

        // Limita al numero di colonne disponibili
        int maxCells = Mathf.Min(brailleCells.Count, columns * rows);

        for (int i = 0; i < maxCells; i++)
        {
            // Calcola la posizione nella griglia (colonna e riga)
            int col = i % columns;
            int row = i / columns;

            // Spawn di una cella
            BrailleCell cell = SpawnCell(col, row);

            // Imposta i puntini della cella
            cell.SetBrailleDots(brailleCells[i]);
        }
    }


    private BrailleCell SpawnCell(int col, int row)
    {
        BrailleCell cell = Instantiate(cellPrefab, transform);
        cell.dotPrefab = dotPrefab;
        cell.cellWidth = CELL_WIDTH;
        cell.cellHeight = CELL_HEIGHT;
        cell.InitializeDots();

        cell.transform.localPosition = new Vector3(
            col * CELL_WIDTH,
            tableHeight,
            -row * CELL_HEIGHT
        );

        cells.Add(cell);
        return cell;
    }

    public void RefreshGrid()
    {
        Clear();
    }

    private void Clear()
    {
        foreach (var c in cells)
            DestroyImmediate(c.gameObject);
        cells.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int c = 0; c < columns; c++)
            for (int r = 0; r < rows; r++)
            {
                Vector3 pos = new Vector3(c * CELL_WIDTH, tableHeight, -r * CELL_HEIGHT);
                Gizmos.DrawWireCube(transform.TransformPoint(pos), new Vector3(CELL_WIDTH, 0.001f, CELL_HEIGHT));
            }
    }
#endif
}
