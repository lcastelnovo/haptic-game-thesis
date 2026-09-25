using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using HapticResearch.Haptics;
using HapticResearch.Levels;

namespace HapticResearch.Hands
{
    // Rileva la presa con i GUANTI VERI e la instrada nel WeArtGraspBridge, ESATTAMENTE come la
    // modalita' demo fa col tasto G. Cosi' la logica di gioco (ShapeRecognitionManager) reagisce
    // identica a demo e guanti: percorso input-agnostico, ZERO modifiche all'SDK.
    //
    // Perche' serve: nel nostro setup il WeArtHandController ufficiale e' disabilitato (il movimento
    // mano lo fanno i nostri script), quindi il grasp NATIVO WEART non parte e il bridge non riceve
    // nulla dai guanti. Questo componente colma il buco: legge la chiusura reale dei thimble e,
    // quando abbastanza dita si chiudono su una forma vicina, la segna come "afferrata" nel bridge;
    // al riaprirsi delle dita la rilascia.
    //
    // Da mettere su OGNI mano (una copia per lato: sinistra isLeftHand=ON, destra=OFF). Attivo SOLO
    // col middleware WEART connesso: in demo (nessun device) resta dormiente e non tocca la presa
    // iniettata da HandDemoModeController.
    public class GloveGraspDetector : MonoBehaviour
    {
        [Header("Lato mano")]
        [Tooltip("Questa e' la mano SINISTRA? Una copia per mano: sinistra ON, destra OFF.")]
        [SerializeField] private bool isLeftHand = true;

        [Header("Chiusura dita (thimble WEART reali)")]
        [Tooltip("I thimble delle dita (indice..mignolo). Se vuoto, prova a cercarli tra i figli della mano.")]
        [SerializeField] private WeArtThimbleTrackingObject[] fingerTracking;

        [Tooltip("Thimble del pollice (opzionale, si somma al conteggio).")]
        [SerializeField] private WeArtThimbleTrackingObject thumbTracking;

        [Tooltip("Chiusura (0-1) oltre la quale un dito conta come 'chiuso'.")]
        [SerializeField, Range(0f, 1f)] private float closureThreshold = 0.5f;

        [Tooltip("Numero minimo di dita chiuse per afferrare.")]
        [SerializeField, Range(1, 5)] private int minFingersClosed = 2;

        [Header("Rilevamento forma")]
        [Tooltip("Punto attorno a cui cercare la forma. Lasciare VUOTO: si usa la punta dell'indice della mano tracciata. Riempirlo solo per forzare un punto diverso.")]
        [SerializeField] private Transform grabPoint;

        [Tooltip("Raggio (m) entro cui una forma conta come 'in mano'. Come la demo: 0.08.")]
        [SerializeField] private float contactRadius = 0.08f;

        [Header("Anti presa-fantasma (guanto appoggiato e dimenticato)")]
        [Tooltip("Per armare la presa la mano deve prima APRIRSI: dita sotto questa chiusura. Il rumore di un guanto appoggiato oscilla di poco attorno alla soglia di chiusura e non scende mai fin qui, quindi non arma mai la presa. Regolabile per partecipante se qualcuno non riesce ad aprire bene la mano.")]
        [SerializeField, Range(0f, 1f)] private float openThreshold = 0.3f;

        [Tooltip("Log diagnostici in Console (device connesso, dita chiuse, afferra/rilascia).")]
        [SerializeField] private bool debugLog = true;

        private WeArtGraspBridge bridge;
        private GraspLatch<RecognizableShape> latch; // apri->chiudi e forma in mano (provata in Tools/GraspTest)
        private FingerProbeSource trackedTip;        // punta dell'indice della mano dei tracker
        private Transform fallbackPoint;             // ripiego se la punta tracciata non c'e'
        private string probeOrigin = "-";            // da dove si sta misurando (diagnostica F1/Console)
        private bool wasConnected;
        private int debugTick;

        void Awake()
        {
            latch = new GraspLatch<RecognizableShape>(minFingersClosed);

            // Punta dell'indice della mano TRACCIATA di questo lato. E' il punto giusto da
            // cui misurare: questo componente lavora solo col device connesso, quindi la
            // mano che il partecipante muove davvero e' quella dei tracker. Misurare dal
            // transform di QUESTO GameObject significherebbe misurare dalla mano del mouse,
            // che resta ferma dove il cursore l'ha lasciata - tipicamente nella sua posizione
            // di partenza, a pochi centimetri da una forma, che risulterebbe cosi' l'unica
            // afferrabile per tutto il livello.
            trackedTip = new FingerProbeSource
            {
                RigFilter = FingerProbeSource.Rig.Tracked,
                SideFilter = isLeftHand ? HandSideFlags.Left : HandSideFlags.Right,
            };

            // Ripiego storico, usato solo se la punta tracciata non c'e' (scene senza rig WEART).
            var grab = GetComponent<HandGrabController>();
            if (grab != null) fallbackPoint = grab.grabPoint;

            // Fallback thimble: se non assegnati, prende quelli sotto la mano (sono figli del rig
            // WEART, lato corretto in automatico). Esclude il palmo: conto solo le dita.
            if (fingerTracking == null || fingerTracking.Length == 0)
            {
                var found = GetComponentsInChildren<WeArtThimbleTrackingObject>(true);
                var fingers = new List<WeArtThimbleTrackingObject>();
                foreach (var t in found)
                    if (t != null && t.ActuationPoint != ActuationPoint.Palm)
                        fingers.Add(t);
                fingerTracking = fingers.ToArray();
            }
        }

        void Start()
        {
            bridge = WeArtGraspBridge.Instance;
        }

        void Update()
        {
            if (bridge == null) bridge = WeArtGraspBridge.Instance;
            if (bridge == null) return;

            bool connected = IsDeviceConnected();
            if (debugLog && connected != wasConnected)
                Debug.Log($"[GloveGrasp {Side}] device {(connected ? "CONNESSO" : "disconnesso")}");
            wasConnected = connected;

            // Attivo solo con device reale connesso: in demo la presa la gestisce HandDemoModeController,
            // e qui NON dobbiamo azzerare lo slot che ha impostato la demo.
            if (!connected)
            {
                latch.Reset(); // al ritorno del device serve un vero apri -> chiudi
                return;
            }

            int closed = CountClosedFingers();

            // La forma sotto la mano si rilegge A OGNI FRAME, anche a stretta gia' in corso:
            // e' cio' che tiene "quello che il gioco crede di avere in mano" agganciato a
            // dove sta davvero la mano. Decidendola solo all'istante della chiusura, una
            // mano che si chiude una volta sulla forma su cui riposa se la porta dietro per
            // tutto il livello e ogni altra risposta risulta sbagliata.
            var nearest = FindShapeNearHand();

            // Lo slot e' ancora nostro? Dopo ogni conferma il manager lo azzera apposta,
            // per pretendere un nuovo gesto prima di riprendere una forma.
            var before = latch.Held;
            bool slotIntact = before == null || CurrentSlot() == before.gameObject;

            var after = latch.Step(closed, CountFingersAbove(openThreshold), nearest, slotIntact);
            if (after != before) ApplyToBridge(before, after, closed);

            if (!debugLog || (++debugTick % 120 != 0)) return;
            if (!latch.SessionActive)
                Debug.Log($"[GloveGrasp {Side}] in ascolto - dita chiuse: {closed}/{minFingersClosed} - {(latch.Armed ? "presa pronta" : "apri la mano per armare la presa")}");
            else if (latch.Held == null)
                Debug.Log($"[GloveGrasp {Side}] mano chiusa ma nessuna forma entro {contactRadius} m da {ProbeDescription()}");
        }

        // Riporta nel bridge il cambio di forma deciso dalla macchina a stati.
        private void ApplyToBridge(RecognizableShape before, RecognizableShape after, int closed)
        {
            // Tolgo dallo slot solo cio' che ci avevo messo io: se nel frattempo se n'e'
            // impossessato qualcun altro non lo tocco.
            if (before != null && CurrentSlot() == before.gameObject) bridge.ClearGrasp(isLeftHand);
            if (after != null) bridge.SetGrasp(after.gameObject, isLeftHand, "guanto");

            if (!debugLog) return;
            if (after != null)
                Debug.Log($"[GloveGrasp {Side}] AFFERRA '{after.name}' (dita chiuse: {closed})");
            else
                Debug.Log($"[GloveGrasp {Side}] rilascia '{(before != null ? before.name : "-")}' (dita chiuse: {closed})");
        }

        private string Side => isLeftHand ? "SX" : "DX";

        // Espone lato e posizione del punto di presa per la diagnostica (GraspDebugPanel).
        public bool IsLeftHand => isLeftHand;
        public Vector3? GrabPointPosition => TryProbePosition(out Vector3 p) ? p : (Vector3?)null;

        // Da dove sta misurando questa mano, in chiaro: se qui compare "grabPoint" invece
        // di "indice tracciato", il rig dei tracker non sta dando nessuna punta e la presa
        // sta misurando dalla mano del mouse.
        public string ProbeOrigin => probeOrigin;

        private string ProbeDescription()
            => TryProbePosition(out Vector3 p) ? $"{probeOrigin} {p.ToString("0.00")}" : "nessun punto di misura";

        private GameObject CurrentSlot() => isLeftHand ? bridge.LeftGrasped : bridge.RightGrasped;

        // "Device presente" = middleware WEART davvero connesso (socket aperto). ATTENZIONE: il getter
        // WeArtController.Client CREA il client la prima volta, quindi "Client != null" e' sempre vero
        // anche senza device: il segnale affidabile e' Client.IsConnected.
        private bool IsDeviceConnected()
        {
            var c = WeArtController.Instance;
            return c != null && c.Client != null && c.Client.IsConnected;
        }

        private int CountClosedFingers() => CountFingersAbove(closureThreshold);

        private int CountFingersAbove(float threshold)
        {
            int n = 0;
            if (fingerTracking != null)
                for (int i = 0; i < fingerTracking.Length; i++)
                    if (fingerTracking[i] != null && fingerTracking[i].Closure.Value >= threshold) n++;
            if (thumbTracking != null && thumbTracking.Closure.Value >= threshold) n++;
            return n;
        }

        // Da dove si misura, in ordine: punto forzato dall'Inspector, punta dell'indice
        // tracciata, ripiego storico. Aggiorna probeOrigin per la diagnostica.
        private bool TryProbePosition(out Vector3 point)
        {
            if (grabPoint != null)
            {
                point = grabPoint.position;
                probeOrigin = "forzato";
                return true;
            }

            // trackedTip e' null finche' Awake non e' girato: il pannello diagnostico legge
            // anche i rilevatori su GameObject disattivati, dove Awake non viene chiamato.
            if (trackedTip != null)
            {
                trackedTip.Refresh();
                if (trackedTip.TryLowestTip(out point))
                {
                    probeOrigin = "indice tracciato";
                    return true;
                }
            }

            if (fallbackPoint != null)
            {
                point = fallbackPoint.position;
                probeOrigin = "grabPoint (nessuna punta tracciata)";
                return true;
            }

            point = default;
            probeOrigin = "nessuno";
            return false;
        }

        private RecognizableShape FindShapeNearHand()
        {
            if (!TryProbePosition(out Vector3 probe)) return null;
            var hits = Physics.OverlapSphere(probe, contactRadius);

            // Sceglie la forma PIÙ VICINA al dito, non la prima restituita dal physics
            // engine (ordine arbitrario): con più forme nel raggio prenderebbe a caso.
            RecognizableShape best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var rec = hits[i].GetComponentInParent<RecognizableShape>();
                if (rec == null) continue;
                float d = Vector3.Distance(probe, hits[i].ClosestPoint(probe));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = rec;
                }
            }
            return best;
        }
    }
}
