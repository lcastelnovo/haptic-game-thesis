using UnityEngine;
using WeArt.Components;

public class HandCloseController : MonoBehaviour
{
    public FingerController[] fingers; // array di 4 dita
    public ThumbController thumb;      // pollice

    [Range(0, 1)]
    public float[] closeAmounts; // chiusura di ogni dito (0-1)
    private int selectedFinger = 0; // 0 = tutte le dita, 1-4 = dita specifiche, 5 = pollice

    [HideInInspector]
    public bool isActive = true;

    [Header("Tracking guanto WEART (opzionale)")]
    [Tooltip("WeArtThimbleTrackingObject per ogni dito, stesso ordine di fingers[]")]
    public WeArtThimbleTrackingObject[] fingerTracking;
    public WeArtThimbleTrackingObject thumbTracking;

    void Start()
    {
        // inizializza array con numero dita + pollice
        closeAmounts = new float[fingers.Length + 1];
    }

    void Update()
    {
        if (!isActive) return;

        if (IsGloveTrackingActive())
        {
            // Usa dati reali dal guanto
            for (int i = 0; i < fingers.Length; i++)
            {
                if (i < fingerTracking.Length && fingerTracking[i] != null)
                    closeAmounts[i] = fingerTracking[i].Closure.Value;
            }
            if (thumbTracking != null)
                closeAmounts[closeAmounts.Length - 1] = thumbTracking.Closure.Value;
        }
        else
        {
            // Fallback: tastiera + scroll
            if (Input.GetKeyDown(KeyCode.Alpha0)) selectedFinger = 0; // tutte le dita
            if (Input.GetKeyDown(KeyCode.Alpha1)) selectedFinger = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) selectedFinger = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) selectedFinger = 3;
            if (Input.GetKeyDown(KeyCode.Alpha4)) selectedFinger = 4;
            if (Input.GetKeyDown(KeyCode.Alpha5)) selectedFinger = 5; // pollice

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                if (selectedFinger == 0)
                {
                    for (int i = 0; i < closeAmounts.Length; i++)
                        closeAmounts[i] = Mathf.Clamp01(closeAmounts[i] + scroll);
                }
                else
                {
                    closeAmounts[selectedFinger - 1] = Mathf.Clamp01(closeAmounts[selectedFinger - 1] + scroll);
                }
            }
        }

        // Applica i valori
        for (int i = 0; i < fingers.Length; i++)
            fingers[i].closeAmount = closeAmounts[i];

        thumb.closeAmount = closeAmounts[closeAmounts.Length - 1];
    }

    // Il tracking è attivo se il middleware è connesso e almeno un tracking object è assegnato
    private bool IsGloveTrackingActive()
    {
        if (WeArtController.Instance == null || WeArtController.Instance.Client == null)
            return false;
        bool hasFingerTracking = fingerTracking != null && fingerTracking.Length > 0;
        return hasFingerTracking || thumbTracking != null;
    }
}
