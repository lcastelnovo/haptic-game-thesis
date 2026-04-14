using UnityEngine;
using WeArt.Components;

/// <summary>
/// Monitora in tempo reale i trigger events sugli WeArtHapticObject.
/// Aggiungere a un GameObject vuoto in scena per il debug.
/// Si auto-configura al Play.
/// </summary>
public class HapticTriggerMonitor : MonoBehaviour
{
    void Start()
    {
        // Sottoscrivi a tutti gli WeArtHapticObject attivi
        var haptics = FindObjectsByType<WeArtHapticObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Debug.Log($"[HapticMonitor] Trovati {haptics.Length} WeArtHapticObject attivi");

        foreach (var h in haptics)
        {
            var hapticRef = h; // cattura per closure
            h.TriggerEnter += (col) =>
            {
                var touchable = col.GetComponent<WeArtTouchableObject>();
                string touchInfo = touchable != null ? $"WeArtTouchableObject(temp={touchable.Temperature.Value:F2})" : "NO WeArtTouchableObject";
                Debug.Log($"[HapticMonitor] TRIGGER ENTER: {hapticRef.gameObject.name} -> {col.gameObject.name} ({touchInfo})");
            };
            h.TriggerExit += (col) =>
            {
                Debug.Log($"[HapticMonitor] TRIGGER EXIT: {hapticRef.gameObject.name} -> {col.gameObject.name}");
            };
        }

        // Info sul cubo
        var touchables = FindObjectsByType<WeArtTouchableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var t in touchables)
        {
            var cols = t.GetComponents<Collider>();
            foreach (var c in cols)
            {
                Debug.Log($"[HapticMonitor] Touchable '{t.gameObject.name}' collider: {c.GetType().Name} isTrigger={c.isTrigger} enabled={c.enabled} bounds={c.bounds.size}");
            }
        }

        // Info posizioni
        InvokeRepeating(nameof(LogPositions), 2f, 3f);
    }

    void LogPositions()
    {
        var haptics = FindObjectsByType<WeArtHapticObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var touchables = FindObjectsByType<WeArtTouchableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (touchables.Length == 0 || haptics.Length == 0) return;

        var cubePos = touchables[0].transform.position;
        float minDist = float.MaxValue;
        string closestFinger = "";
        foreach (var h in haptics)
        {
            float dist = Vector3.Distance(h.transform.position, cubePos);
            if (dist < minDist)
            {
                minDist = dist;
                closestFinger = h.gameObject.name;
            }
        }
        Debug.Log($"[HapticMonitor] Dito piu' vicino al cubo: {closestFinger} dist={minDist:F4}m (cubo a {cubePos})");
    }
}