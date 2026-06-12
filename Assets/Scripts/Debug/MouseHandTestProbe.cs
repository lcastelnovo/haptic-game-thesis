using UnityEngine;
using System.Collections.Generic;

// "Ditina virtuale" per test mode senza hardware.
// Si attacca a un GameObject con SphereCollider trigger + Rigidbody kinematic + mesh visiva.
//
// Comportamento:
//   - Update: proietta la posizione del mouse sul piano del tavolo, sposta il transform li'.
//   - Scroll wheel: alza/abbassa la sonda rispetto al piano (per "uscire" dal piano).
//   - Click sinistro su un oggetto = "grab/trovato": l'oggetto entra nella set dei trovati,
//     contatore stampato in Console + HUD top-left.
//
// Usa OnTrigger* per registrare i contatti. Le forme del tavolo (Cube, Cylinder, ...) hanno
// Rigidbody kinematic ma collider NON trigger - basta che uno dei due lo sia, qui lo e' la sonda.
public class MouseHandTestProbe : MonoBehaviour
{
    [Header("Piano del tavolo")]
    [Tooltip("Altezza Y del piano del tavolo su cui proiettare il mouse.")]
    [SerializeField] private float tableY = 0.86f;

    [Header("Offset Y (scroll wheel)")]
    [SerializeField] private float scrollSensitivity = 0.02f;
    [SerializeField] private float minYOffset = -0.05f;
    [SerializeField] private float maxYOffset = 0.30f;

    [Header("Camera sorgente")]
    [Tooltip("Camera da cui proiettare il raggio del mouse. Se null usa Camera.main.")]
    [SerializeField] private Camera sourceCamera;

    [Header("Tracking trovati")]
    [Tooltip("Numero totale di oggetti del livello (per il contatore HUD).")]
    [SerializeField] private int expectedTotal = 5;

    [Tooltip("Colore applicato all'oggetto quando viene trovato per la prima volta.")]
    [SerializeField] private Color foundColor = Color.green;

    private float currentYOffset = 0f;
    private readonly HashSet<Collider> touched = new HashSet<Collider>();
    private readonly HashSet<GameObject> found = new HashSet<GameObject>();

    void Awake()
    {
        if (sourceCamera == null) sourceCamera = Camera.main;
    }

    void Update()
    {
        if (sourceCamera == null) return;

        // Offset Y con scroll wheel
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            currentYOffset = Mathf.Clamp(currentYOffset + scroll * scrollSensitivity, minYOffset, maxYOffset);
        }

        // Proiezione mouse sul piano y=tableY
        Ray ray = sourceCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, tableY, 0f));
        if (plane.Raycast(ray, out float dist))
        {
            Vector3 hit = ray.GetPoint(dist);
            transform.position = new Vector3(hit.x, tableY + currentYOffset, hit.z);
        }

        // Click sinistro: "grab/trovato"
        if (Input.GetMouseButtonDown(0))
        {
            HandleGrab();
        }
    }

    private void HandleGrab()
    {
        if (touched.Count == 0)
        {
            Debug.Log("[Probe] Click a vuoto (nessun oggetto sotto)");
            return;
        }

        foreach (var c in touched)
        {
            if (c == null) continue;
            // Per oggetti compound (es. Star = parent + 5 figli) risali al root cosi' contiamo
            // l'oggetto logico, non il singolo pezzo
            GameObject go = c.transform.root.gameObject;

            if (found.Add(go))
            {
                Debug.Log($"[Probe] TROVATO: {go.name} ({found.Count}/{expectedTotal})");
                ColorAsFound(go);
                if (found.Count >= expectedTotal)
                {
                    Debug.Log("[Probe] Tutti gli oggetti trovati!");
                }
            }
            else
            {
                Debug.Log($"[Probe] Gia' trovato: {go.name}");
            }
        }
    }

    // Colora tutti i MeshRenderer dell'oggetto (e dei figli, per compound tipo Star).
    // .material crea un'istanza per oggetto: non sporca il prefab condiviso.
    private void ColorAsFound(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
        {
            r.material.color = foundColor;
        }
    }

    void OnTriggerEnter(Collider other) { touched.Add(other); }
    void OnTriggerExit(Collider other) { touched.Remove(other); }

    void LateUpdate()
    {
        touched.RemoveWhere(c => c == null);
    }

    void OnGUI()
    {
        string text = $"Trovati: {found.Count}/{expectedTotal}";
        GUI.Box(new Rect(10, 10, 200, 30), text);
    }
}
