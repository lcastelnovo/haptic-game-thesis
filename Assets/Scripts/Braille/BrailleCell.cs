using UnityEngine;

public class BrailleCell : MonoBehaviour
{
    [Header("Dot prefab")]
    public GameObject dotPrefab;  // prefab cilindro
    public float cellWidth = 0.2f;   // asse X
    public float cellHeight = 0.3f;  // asse Z

    private BrailleDot[] dots = new BrailleDot[6];

    // crea i 6 puntini
    public void InitializeDots()
    {
        ClearDots();

        // Calcola gli offset in base alla dimensione della cella
        float xOffset = cellWidth / 4f;   // metà distanza tra le colonne
        float zOffset = cellHeight / 3f;  // distanza tra le righe (3 righe → 2 spazi)

        for (int i = 0; i < 6; i++)
        {
            int col = i / 3;   // 0 o 1
            int row = i % 3;   // 0,1,2

            Vector3 localPos = new Vector3(
                (col == 0 ? -xOffset : xOffset),
                0f,
                zOffset - row * zOffset
            );

            GameObject dotObj = Instantiate(dotPrefab);

            dotObj.transform.SetParent(transform, false);
            dotObj.transform.localPosition = localPos;

            BrailleDot dot = dotObj.GetComponent<BrailleDot>();
            if (dot == null)
                dot = dotObj.AddComponent<BrailleDot>();
            
            dot.transform.localPosition = localPos;
            dot.SetBasePosition(localPos);

            dot.dotIndex = i;

            dot.SetState(false); // inizialmente non visibile

            dots[i] = dot;  
        }
    }

    public void SetBrailleDots(bool[] pattern)
    {
        if (pattern.Length != dots.Length)
        {
            return;
        }

        for (int i = 0; i < dots.Length; i++)
        {
            dots[i].SetState(pattern[i]);
        }
    }


    public void SetLetter(char c)
    {
        bool[] pattern = BrailleDatabase.GetLetterPattern(c);

        for (int i = 0; i < dots.Length; i++)
            dots[i].SetState(pattern[i]);
    }

    private void ClearDots()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}
