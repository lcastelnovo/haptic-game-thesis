using System.Collections.Generic;
using UnityEngine;
using HapticResearch.Levels;

using HapticResearch.UI;
namespace HapticResearch.Branding
{
    /// <summary>
    /// Applica il branding UniBS a runtime: watermark a schermo, logo che fluttua sopra ogni forma,
    /// logo sul tavolo e pannello-logo di sfondo. Tutto creato via codice (nessun setup di scena
    /// fragile) e regolabile da Inspector. Non tocca la logica di gioco: è solo estetica, quindi
    /// vale identico in demo e coi guanti.
    /// </summary>
    public class UnibsBranding : MonoBehaviour
    {
        [Header("Logo (assegna Assets/Textures/UnibsLogo)")]
        [SerializeField] private Texture2D logo;

        // Letto dall'HUD operatore per l'intestazione della sidebar.
        public Texture2D Logo => logo;

        [Header("Watermark a schermo")]
        [SerializeField] private bool showWatermark = true;
        [SerializeField] private float watermarkWidth = 210f;
        [SerializeField] private Vector2 watermarkMargin = new Vector2(14f, 14f);

        [Header("Logo sopra le forme")]
        [SerializeField] private bool logoOverShapes = true;
        [SerializeField] private float shapeLogoSize = 0.12f;
        [SerializeField] private float shapeLogoHeight = 0.16f; // quanto sopra il top della forma

        [Header("Logo sul tavolo")]
        [SerializeField] private bool logoOnTable = true;
        [SerializeField] private Vector3 tableLogoPos = new Vector3(0f, 0.861f, -0.28f);
        [SerializeField] private float tableLogoSize = 0.45f;
        [Tooltip("Rotazione del logo sul tavolo. Y=180 lo gira per farlo leggere dal lato giocatore.")]
        [SerializeField] private Vector3 tableLogoEuler = new Vector3(90f, 180f, 0f);

        [Header("Pannello di sfondo")]
        [SerializeField] private bool backdrop = true;
        [SerializeField] private Vector3 backdropPos = new Vector3(0f, 1.25f, 1.3f);
        [SerializeField] private float backdropSize = 1.3f;

        private Material logoMat;
        private Camera cam;
        private readonly Dictionary<RecognizableShape, Transform> shapeLogos =
            new Dictionary<RecognizableShape, Transform>();

        private void Start()
        {
            if (logo == null)
            {
                Debug.LogWarning("[UniBS] Logo non assegnato sullo script UnibsBranding.");
                return;
            }

            logoMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = logo };

            if (logoOnTable) MakeQuad("UnibsTableLogo", tableLogoPos, Quaternion.Euler(tableLogoEuler), tableLogoSize);
            if (backdrop) MakeQuad("UnibsBackdrop", backdropPos, Quaternion.Euler(0f, 180f, 0f), backdropSize);
        }

        private void Update()
        {
            if (logoMat == null || !logoOverShapes)
                return;

            // Crea un logo sopra ogni forma nuova (le forme ricevono RecognizableShape allo start del livello).
            foreach (RecognizableShape s in FindObjectsByType<RecognizableShape>(FindObjectsSortMode.None))
            {
                if (shapeLogos.ContainsKey(s))
                    continue;
                Transform q = MakeQuad($"UnibsLogo_{s.name}", Vector3.zero, Quaternion.identity, shapeLogoSize);
                q.SetParent(s.transform, worldPositionStays: false);
                shapeLogos[s] = q;
            }
        }

        private void LateUpdate()
        {
            if (!logoOverShapes)
                return;

            if (cam == null)
                cam = ResolveCamera();

            // Posiziona i logo sopra le rispettive forme e falli guardare la camera (billboard).
            foreach (var kv in shapeLogos)
            {
                RecognizableShape s = kv.Key;
                Transform q = kv.Value;
                if (s == null || q == null)
                    continue;

                float topY = ShapeTopY(s);
                q.position = new Vector3(s.transform.position.x, topY + shapeLogoHeight, s.transform.position.z);
                if (cam != null)
                    q.rotation = Quaternion.LookRotation(q.position - cam.transform.position, Vector3.up);
            }
        }

        private Transform MakeQuad(string qName, Vector3 pos, Quaternion rot, float size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = qName;
            Destroy(go.GetComponent<Collider>()); // niente fisica di troppo
            go.transform.SetPositionAndRotation(pos, rot);

            // Mantiene le proporzioni del logo.
            float aspect = logo.height > 0 ? (float)logo.width / logo.height : 1f;
            go.transform.localScale = new Vector3(size * aspect, size, 1f);

            go.GetComponent<MeshRenderer>().sharedMaterial = logoMat;
            return go.transform;
        }

        // Y del top della forma (bounds del renderer), per posizionarci il logo sopra.
        private float ShapeTopY(RecognizableShape s)
        {
            Renderer r = s.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.max.y : s.transform.position.y;
        }

        private Camera ResolveCamera()
        {
            Camera best = null;
            float bestDepth = float.NegativeInfinity;
            foreach (Camera c in Camera.allCameras)
            {
                if (c == null || c.targetTexture != null || c.targetDisplay != 0)
                    continue;
                if (c.depth >= bestDepth) { bestDepth = c.depth; best = c; }
            }
            return best;
        }

        private void OnGUI()
        {
            // Con l'HUD operatore il logo sta nell'intestazione della sidebar.
            if (!showWatermark || logo == null || OperatorHud.Active)
                return;

            float aspect = logo.height > 0 ? (float)logo.width / logo.height : 1f;
            float w = watermarkWidth;
            float h = w / aspect;
            var rect = new Rect(watermarkMargin.x, Screen.height - h - watermarkMargin.y, w, h);
            GUI.DrawTexture(rect, logo, ScaleMode.ScaleToFit, true);
        }
    }
}
