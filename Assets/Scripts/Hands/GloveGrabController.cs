using UnityEngine;
using System.Collections.Generic;
using WeArt.Components;

/// <summary>
/// Afferrare/rilasciare oggetti chiudendo/aprendo le dita.
/// Usa dati di closure da HandCloseController e contatti da HandCollisionController.
/// Da attaccare allo stesso GameObject della mano (con HandGrabController).
/// ATTENZIONE: è il grasp del guanto SIMULATO (scroll/tastiera). Coi guanti veri
/// connessi resta dormiente: la presa reale la gestisce GloveGraspDetector, che non
/// sposta gli oggetti. Senza questo stop, le chiusure reali dei thimble (che alimentano
/// closeAmounts) facevano afferrare e trascinare forme a insaputa del partecipante.
/// </summary>
public class GloveGrabController : MonoBehaviour
{
    [Header("Riferimenti")]
    [SerializeField] private HandCloseController closeController;
    [SerializeField] private HandCollisionController collisionController;
    [SerializeField] private HandGrabController handGrabController;

    [Header("Soglie di grasping")]
    [Tooltip("Closure minima (0-1) perché un dito conti come 'chiuso'")]
    [SerializeField, Range(0f, 1f)] private float closureThreshold = 0.6f;

    [Tooltip("Closure massima (0-1) sotto la quale si rilascia")]
    [SerializeField, Range(0f, 1f)] private float releaseThreshold = 0.3f;

    [Tooltip("Numero minimo di dita chiuse E in contatto per afferrare")]
    [SerializeField, Range(1, 5)] private int minFingersToGrab = 2;

    private IGrabbable currentGrabbable;
    public IGrabbable CurrentGrabbable => currentGrabbable;

    // Mapping indice closeAmounts → HandPart (fingers[0..3] = Index..Pinky, [4] = Thumb)
    private static readonly HandColliderPart.HandPart[] FingerParts = {
        HandColliderPart.HandPart.Index,
        HandColliderPart.HandPart.Middle,
        HandColliderPart.HandPart.Annular,
        HandColliderPart.HandPart.Pinky
    };

    void Start()
    {
        if (closeController == null) closeController = GetComponent<HandCloseController>();
        if (collisionController == null) collisionController = GetComponent<HandCollisionController>();
        if (handGrabController == null) handGrabController = GetComponent<HandGrabController>();
    }

    void Update()
    {
        // Coi guanti VERI connessi questo controller non deve afferrare: closeAmounts
        // riflette le dita reali e qui si aggancerebbero forme in silenzio durante
        // l'esplorazione (il partecipante non vedente non può accorgersene). Se al momento
        // della connessione qualcosa era in mano, va rilasciato subito.
        if (IsRealDeviceConnected())
        {
            if (currentGrabbable != null)
            {
                currentGrabbable.OnRelease(handGrabController.grabPoint.position);
                Debug.Log("[GloveGrabController] Device WEART connesso: rilascio e resto dormiente");
                currentGrabbable = null;
            }
            return;
        }

        if (closeController.closeAmounts == null) return;

        if (currentGrabbable == null)
            TryGrab();
        else
            TryRelease();
    }

    // Stesso segnale usato da GloveGraspDetector: "device presente" = socket middleware
    // davvero aperto. Il getter Client crea il client alla prima lettura, quindi
    // "Client != null" sarebbe sempre vero: l'unico segnale affidabile è IsConnected.
    private bool IsRealDeviceConnected()
    {
        var c = WeArtController.Instance;
        return c != null && c.Client != null && c.Client.IsConnected;
    }

    void TryGrab()
    {
        // Se HandGrabController ha già afferrato qualcosa (via click), non interferire
        if (handGrabController != null && handGrabController.CurrentGrabbable != null) return;

        // Trova l'oggetto IGrabbable toccato dal maggior numero di dita chiuse
        var candidates = new Dictionary<IGrabbable, int>();

        // Controlla le 4 dita
        for (int i = 0; i < FingerParts.Length && i < closeController.closeAmounts.Length - 1; i++)
        {
            if (closeController.closeAmounts[i] < closureThreshold) continue;

            if (!collisionController.touchedObjects.TryGetValue(FingerParts[i], out var colliders))
                continue;

            foreach (var col in colliders)
            {
                if (col == null) continue;
                var grabbable = col.GetComponentInParent<IGrabbable>();
                if (grabbable != null && grabbable.CanBeInteractedWith)
                {
                    if (!candidates.ContainsKey(grabbable)) candidates[grabbable] = 0;
                    candidates[grabbable]++;
                }
            }
        }

        // Controlla il pollice
        int thumbIdx = closeController.closeAmounts.Length - 1;
        if (closeController.closeAmounts[thumbIdx] >= closureThreshold)
        {
            if (collisionController.touchedObjects.TryGetValue(HandColliderPart.HandPart.Thumb, out var thumbColliders))
            {
                foreach (var col in thumbColliders)
                {
                    if (col == null) continue;
                    var grabbable = col.GetComponentInParent<IGrabbable>();
                    if (grabbable != null && grabbable.CanBeInteractedWith)
                    {
                        if (!candidates.ContainsKey(grabbable)) candidates[grabbable] = 0;
                        candidates[grabbable]++;
                    }
                }
            }
        }

        // Afferrare il candidato con più dita (se >= soglia)
        IGrabbable best = null;
        int bestCount = 0;
        foreach (var kvp in candidates)
        {
            if (kvp.Value >= minFingersToGrab && kvp.Value > bestCount)
            {
                best = kvp.Key;
                bestCount = kvp.Value;
            }
        }

        if (best != null)
        {
            currentGrabbable = best;
            best.OnGrab(handGrabController.grabPoint);
            Debug.Log($"[GloveGrabController] Afferrato con {bestCount} dita");
        }
    }

    void TryRelease()
    {
        // Conta quante dita sono ancora chiuse
        int closedFingers = 0;
        for (int i = 0; i < closeController.closeAmounts.Length; i++)
        {
            if (closeController.closeAmounts[i] > releaseThreshold)
                closedFingers++;
        }

        if (closedFingers < minFingersToGrab)
        {
            currentGrabbable.OnRelease(handGrabController.grabPoint.position);
            Debug.Log("[GloveGrabController] Rilasciato");
            currentGrabbable = null;
        }
    }
}