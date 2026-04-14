using UnityEngine;
using UnityEditor;
using WeArt.Components;
using WeArt.Core;

/// <summary>
/// Diagnostica runtime del pipeline aptico WEART.
/// Eseguire in Play mode da menu: WEART > Diagnostica Pipeline
/// </summary>
public static class WeArtDiagnostics
{
    [MenuItem("WEART/Diagnostica Pipeline")]
    public static void DiagnoseHapticPipeline()
    {
        Debug.Log("=== DIAGNOSTICA PIPELINE APTICO WEART ===");

        // 1. WeArtController
        var controller = Object.FindFirstObjectByType<WeArtController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("[DIAG] WeArtController NON trovato in scena!");
            return;
        }
        var ctrlSo = new SerializedObject(controller);
        Debug.Log($"[DIAG] WeArtController: enabled={controller.enabled}, startAuto={ctrlSo.FindProperty("_startAutomatically").boolValue}, useExternalGrasp={controller.UseExternalGraspSystem}");

        // 2. WeArtHandController
        var handControllers = Object.FindObjectsByType<WeArtHandController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[DIAG] WeArtHandController trovati: {handControllers.Length}");

        foreach (var hc in handControllers)
        {
            Debug.Log($"[DIAG] --- Mano: {hc.gameObject.name} (enabled={hc.enabled}, activeInHierarchy={hc.gameObject.activeInHierarchy}) ---");

            // Verifica ThimbleTrackingObjects
            var thimbles = hc.GetComponentsInChildren<WeArtThimbleTrackingObject>(true);
            foreach (var t in thimbles)
            {
                string closureStr = Application.isPlaying ? $"closure={t.Closure.Value:F3}" : "N/A (non in Play)";
                Debug.Log($"[DIAG]   ThimbleTracking: {t.gameObject.name} enabled={t.enabled} active={t.gameObject.activeInHierarchy} {closureStr}");
            }

            // Verifica WeArtHapticObject
            var haptics = hc.GetComponentsInChildren<WeArtHapticObject>(true);
            foreach (var h in haptics)
            {
                var colliders = h.GetComponents<Collider>();
                string colliderInfo = "";
                foreach (var c in colliders)
                {
                    float worldSize = 0;
                    if (c is SphereCollider sc)
                        worldSize = sc.radius * Mathf.Max(c.transform.lossyScale.x, c.transform.lossyScale.y, c.transform.lossyScale.z);
                    else if (c is CapsuleCollider cc)
                        worldSize = cc.radius * Mathf.Max(c.transform.lossyScale.x, c.transform.lossyScale.y, c.transform.lossyScale.z);
                    else if (c is BoxCollider bc)
                        worldSize = Vector3.Scale(bc.size, c.transform.lossyScale).magnitude;
                    colliderInfo += $"[{c.GetType().Name} enabled={c.enabled} trigger={c.isTrigger} worldRadius~{worldSize:F4}] ";
                }
                if (colliders.Length == 0)
                    colliderInfo = "[NESSUN COLLIDER!]";

                Debug.Log($"[DIAG]   HapticObject: {h.gameObject.name} enabled={h.enabled} active={h.gameObject.activeInHierarchy} {colliderInfo}");
            }

            // Verifica WeArtGraspCheck (proximity colliders)
            var graspChecks = hc.GetComponentsInChildren<WeArtGraspCheck>(true);
            foreach (var gc in graspChecks)
            {
                var colliders = gc.GetComponents<Collider>();
                string colliderInfo = "";
                foreach (var c in colliders)
                {
                    float worldSize = 0;
                    if (c is CapsuleCollider cc)
                        worldSize = cc.radius * Mathf.Max(c.transform.lossyScale.x, c.transform.lossyScale.y, c.transform.lossyScale.z);
                    colliderInfo += $"[{c.GetType().Name} enabled={c.enabled} trigger={c.isTrigger} worldRadius~{worldSize:F4}] ";
                }
                Debug.Log($"[DIAG]   GraspCheck: {gc.gameObject.name} enabled={gc.enabled} active={gc.gameObject.activeInHierarchy} {colliderInfo}");
            }

            // WeArtDeviceTrackingObject
            var dt = hc.GetComponent<WeArtDeviceTrackingObject>();
            if (dt != null)
                Debug.Log($"[DIAG]   DeviceTracking: enabled={dt.enabled} (deve essere false)");

            // WeArtHandGraspingSystem
            var gs = hc.GetComponent<WeArtHandGraspingSystem>();
            if (gs != null)
                Debug.Log($"[DIAG]   GraspingSystem: enabled={gs.enabled}");
            else
                Debug.LogWarning($"[DIAG]   GraspingSystem: NON TROVATO!");

            // Script custom (devono essere disabilitati)
            CheckDisabled<HandCloseController>(hc.gameObject);
            CheckDisabled<FingerController>(hc.gameObject);
            CheckDisabled<ThumbController>(hc.gameObject);
            CheckDisabled<HandGrabController>(hc.gameObject);
            CheckDisabled<HandColliderPart>(hc.gameObject);
            CheckDisabled<HapticFingerSetup>(hc.gameObject);
            CheckDisabled<WeArtHapticBridge>(hc.gameObject);
            CheckDisabled<WeArtTrackingSetup>(hc.gameObject);

            // Rigidbody sulla mano
            var rb = hc.GetComponent<Rigidbody>();
            if (rb != null)
                Debug.Log($"[DIAG]   Rigidbody: isKinematic={rb.isKinematic}");
            else
                Debug.LogWarning($"[DIAG]   Rigidbody: NON TROVATO sulla root mano!");
        }

        // 3. Oggetti con WeArtTouchableObject
        var touchables = Object.FindObjectsByType<WeArtTouchableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[DIAG] WeArtTouchableObject in scena: {touchables.Length}");
        foreach (var t in touchables)
        {
            var col = t.GetComponent<Collider>();
            var rb = t.GetComponent<Rigidbody>();
            string colInfo = col != null ? $"collider={col.GetType().Name} trigger={col.isTrigger}" : "NESSUN COLLIDER!";
            string rbInfo = rb != null ? $"rb=OK kinematic={rb.isKinematic}" : "NESSUN RIGIDBODY!";
            Debug.Log($"[DIAG]   {t.gameObject.name}: graspable={t.Graspable} temp={t.Temperature.Value:F2} stiffness={t.Stiffness.Value:F2} layer={t.gameObject.layer} {colInfo} {rbInfo}");
        }

        // 4. Layer check
        Debug.Log($"[DIAG] Controlla Project Settings > Physics > Layer Collision Matrix per layer 0 vs layer delle mani");

        Debug.Log("=== FINE DIAGNOSTICA ===");
    }

    static void CheckDisabled<T>(GameObject root) where T : Behaviour
    {
        var comps = root.GetComponentsInChildren<T>(true);
        foreach (var c in comps)
        {
            if (c.enabled)
                Debug.LogWarning($"[DIAG]   ATTENZIONE: {typeof(T).Name} su '{c.gameObject.name}' e' ANCORA ABILITATO!");
        }
    }
}