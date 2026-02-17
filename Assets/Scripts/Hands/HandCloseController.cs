using UnityEngine;

public class HandCloseController : MonoBehaviour
{
    public FingerController[] fingers; // array di 4 dita
    public ThumbController thumb;      // pollice

    [Range(0, 1)]
    public float[] closeAmounts; // chiusura di ogni dito (0-1)
    private int selectedFinger = 0; // 0 = tutte le dita, 1-4 = dita specifiche, 5 = pollice

    [HideInInspector]
    public bool isActive = true;

    void Start()
    {
        // inizializza array con numero dita + pollice
        closeAmounts = new float[fingers.Length + 1]; 
    }

    void Update()
    {
        if (!isActive) return;

        // Selezione dito con numeri
        if (Input.GetKeyDown(KeyCode.Alpha0)) selectedFinger = 0; // tutte le dita
        if (Input.GetKeyDown(KeyCode.Alpha1)) selectedFinger = 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) selectedFinger = 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) selectedFinger = 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) selectedFinger = 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) selectedFinger = 5; // pollice

        // Scroll mouse per chiusura/apertura
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            float delta = scroll; // puoi moltiplicare per velocità se vuoi
            if (selectedFinger == 0)
            {
                // tutte le dita
                for (int i = 0; i < closeAmounts.Length; i++)
                    closeAmounts[i] = Mathf.Clamp01(closeAmounts[i] + delta);
            }
            else
            {
                // dito selezionato
                closeAmounts[selectedFinger - 1] = Mathf.Clamp01(closeAmounts[selectedFinger - 1] + delta);
            }
        }

        // Applica i valori
        for (int i = 0; i < fingers.Length; i++)
            fingers[i].closeAmount = closeAmounts[i];

        thumb.closeAmount = closeAmounts[closeAmounts.Length - 1];
    }
}
