using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WeArt.Components;
using HapticResearch.Levels;

namespace HapticResearch.EditorTools
{
    // Sonda diagnostica OFFLINE per il rilevamento forme di Level1.
    //
    // Replica esattamente la query di GloveGraspDetector.FindShapeNearHand
    // (Physics.OverlapSphere raggio 0.08 dal "palmo") su punti campione attorno a OGNI
    // forma registrata in ShapeRecognitionManager.shapes[], e stampa cosa viene visto:
    // quali collider entrano nell'overlap e a che distanza risulta ClosestPoint.
    // Serve a capire, SENZA hardware, se una forma non viene rilevata (es. problemi di
    // MeshCollider convex trigger) o se il problema sta altrove (prese fantasma, soglie).
    public static class Level1DetectionProbe
    {
        private const float ContactRadius = 0.08f; // stesso valore del GloveGraspDetector

        public static void RunHeadless()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Level1_ShapeRecognition.unity");
            Run();
        }

        [MenuItem("HapticResearch/Level 1/Diagnostica rilevamento forme")]
        public static void Run()
        {
            var manager = Object.FindFirstObjectByType<ShapeRecognitionManager>();
            if (manager == null)
            {
                Debug.LogError("[Probe] Nessuno ShapeRecognitionManager: apri Level1.");
                return;
            }

            var so = new SerializedObject(manager);
            var shapes = so.FindProperty("shapes");
            Debug.Log($"[Probe] Forme registrate: {shapes.arraySize}. Raggio contatto: {ContactRadius} m.");

            for (int i = 0; i < shapes.arraySize; i++)
            {
                var el = shapes.GetArrayElementAtIndex(i);
                string id = el.FindPropertyRelative("id").stringValue;
                var go = el.FindPropertyRelative("sceneInstance").objectReferenceValue as GameObject;
                if (go == null)
                {
                    Debug.LogWarning($"[Probe] '{id}': nessuna sceneInstance, salto.");
                    continue;
                }

                var col = go.GetComponent<Collider>();
                var rend = go.GetComponent<Renderer>();
                var touchable = go.GetComponent<WeArtTouchableObject>();
                var bounds = rend != null ? rend.bounds : (col != null ? col.bounds : new Bounds(go.transform.position, Vector3.one));

                Debug.Log($"[Probe] --- '{id}' ({go.name}) pos {go.transform.position} " +
                          $"collider {(col != null ? col.GetType().Name : "NESSUNO")} " +
                          $"trigger {(col != null && col.isTrigger)} enabled {(col != null && col.enabled)} " +
                          $"touchable {(touchable != null)} activeInHierarchy {go.activeInHierarchy} " +
                          $"bounds top y {bounds.max.y:0.000}");

                // Punti campione: come il palmo di una mano appoggiata sulla forma.
                Probe(id, go, "sopra il top (+3cm)", new Vector3(bounds.center.x, bounds.max.y + 0.03f, bounds.center.z));
                Probe(id, go, "al centro", bounds.center);
                Probe(id, go, "di lato (+5cm x)", new Vector3(bounds.max.x + 0.05f, bounds.center.y, bounds.center.z));
            }
        }

        private static void Probe(string id, GameObject shape, string label, Vector3 point)
        {
            var hits = Physics.OverlapSphere(point, ContactRadius);
            bool found = false;
            string nearest = "-";
            float nearestDist = float.MaxValue;

            foreach (var h in hits)
            {
                // Conta solo i collider delle forme (root con WeArtTouchableObject),
                // come farebbe GetComponentInParent<RecognizableShape> a runtime.
                var t = h.GetComponentInParent<WeArtTouchableObject>();
                if (t == null) continue;

                float d = Vector3.Distance(point, h.ClosestPoint(point));
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = t.gameObject.name;
                }
                if (t.gameObject == shape) found = true;
            }

            Debug.Log($"[Probe] '{id}' {label}: {(found ? "TROVATA" : "NON TROVATA")} " +
                      $"(overlap tot {hits.Length}, forma piu' vicina: {nearest} a {(nearestDist == float.MaxValue ? -1 : nearestDist):0.000} m)");
        }
    }
}
