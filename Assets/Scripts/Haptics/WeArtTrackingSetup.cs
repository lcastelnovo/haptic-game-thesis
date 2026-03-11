using UnityEngine;
using WeArt.Components;
using WeArt.Core;

/// <summary>
/// Collega i WeArtThimbleTrackingObject già presenti nel prefab WEART alla mano virtuale.
/// Cerca i tracker per ActuationPoint tra i figli e popola HandCloseController.
/// Da attaccare allo stesso GameObject di HandCloseController.
/// </summary>
[DefaultExecutionOrder(-90)]
[RequireComponent(typeof(HandCloseController))]
public class WeArtTrackingSetup : MonoBehaviour
{
    [Tooltip("ActuationPoint per ogni dito, stesso ordine di HandCloseController.fingers[]")]
    public ActuationPoint[] fingerActuationPoints = new ActuationPoint[]
    {
        ActuationPoint.Index,
        ActuationPoint.Middle,
        ActuationPoint.Annular,
        ActuationPoint.Pinky
    };

    void Awake()
    {
        var closeCtrl = GetComponent<HandCloseController>();

        // Trova tutti i ThimbleTrackingObject già presenti nel prefab
        var allTrackers = GetComponentsInChildren<WeArtThimbleTrackingObject>(includeInactive: true);

        // Associa ogni dito al tracker con ActuationPoint corrispondente
        var fingerTrackers = new WeArtThimbleTrackingObject[closeCtrl.fingers.Length];
        for (int i = 0; i < closeCtrl.fingers.Length && i < fingerActuationPoints.Length; i++)
        {
            fingerTrackers[i] = FindTracker(allTrackers, fingerActuationPoints[i]);
            if (fingerTrackers[i] == null)
                Debug.LogWarning($"[WeArtTrackingSetup] Tracker non trovato per {fingerActuationPoints[i]}");
        }
        closeCtrl.fingerTracking = fingerTrackers;

        closeCtrl.thumbTracking = FindTracker(allTrackers, ActuationPoint.Thumb);
        if (closeCtrl.thumbTracking == null)
            Debug.LogWarning("[WeArtTrackingSetup] Tracker Thumb non trovato");

        Debug.Log($"[WeArtTrackingSetup] Tracking collegato per mano {gameObject.name}");
    }

    private WeArtThimbleTrackingObject FindTracker(WeArtThimbleTrackingObject[] trackers, ActuationPoint point)
    {
        foreach (var t in trackers)
            if (t.ActuationPoint == point) return t;
        return null;
    }
}
