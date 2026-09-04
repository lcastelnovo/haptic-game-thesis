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
        [Tooltip("Punto della mano attorno a cui cercare la forma. Se vuoto usa il grabPoint di HandGrabController.")]
        [SerializeField] private Transform grabPoint;

        [Tooltip("Raggio (m) entro cui una forma conta come 'in mano'. Come la demo: 0.08.")]
        [SerializeField] private float contactRadius = 0.08f;

        [Header("Anti presa-fantasma (guanto appoggiato e dimenticato)")]
        [Tooltip("Per armare la presa la mano deve prima APRIRSI: dita sotto questa chiusura. Il rumore di un guanto appoggiato oscilla di poco attorno alla soglia di chiusura e non scende mai fin qui, quindi non arma mai la presa. Regolabile per partecipante se qualcuno non riesce ad aprire bene la mano.")]
        [SerializeField, Range(0f, 1f)] private float openThreshold = 0.3f;

        [Tooltip("Log diagnostici in Console (device connesso, dita chiuse, afferra/rilascia).")]
        [SerializeField] private bool debugLog = true;

        private WeArtGraspBridge bridge;
        private RecognizableShape held; // forma attualmente segnata nel bridge da QUESTA mano
        private bool armed;             // la mano si e' aperta davvero: presa pronta (parte NON armata)
        private bool wasConnected;
        private int debugTick;

        void Awake()
        {
            // grabPoint: riusa quello di HandGrabController se non assegnato.
            if (grabPoint == null)
            {
                var grab = GetComponent<HandGrabController>();
                if (grab != null) grabPoint = grab.grabPoint;
            }

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
                ForgetLocalOnly();
                armed = false; // al ritorno del device serve un vero apri -> chiudi
                return;
            }

            int closed = CountClosedFingers();
            bool closedEnough = closed >= minFingersClosed;
            // Mano "davvero aperta" = meno di minFingersClosed dita sopra la soglia BASSA.
            // E' il cancello anti-fantasma: serve scendere fin qui per armare la presa.
            bool openEnough = CountFingersAbove(openThreshold) < minFingersClosed;

            if (held == null)
            {
                // ISTERESI apri -> chiudi: la presa scatta solo se la mano si e' prima
                // aperta davvero (armed) e poi chiusa. Le oscillazioni di un guanto
                // appoggiato attorno alla soglia di chiusura non scendono mai sotto
                // openThreshold, quindi non armano mai: niente prese fantasma.
                if (openEnough) armed = true;

                if (armed && closedEnough)
                {
                    armed = false; // presa consumata: per riprovare bisogna riaprire la mano
                    var shape = FindShapeNearHand();
                    if (shape != null)
                    {
                        held = shape;
                        bridge.SetGrasp(shape.gameObject, isLeftHand);
                        if (debugLog) Debug.Log($"[GloveGrasp {Side}] AFFERRA '{shape.name}' (dita chiuse: {closed})");
                    }
                    else if (debugLog)
                        Debug.Log($"[GloveGrasp {Side}] {closed} dita chiuse ma nessuna forma entro {contactRadius} m dal grabPoint");
                }
                else if (debugLog && (++debugTick % 120 == 0))
                    Debug.Log($"[GloveGrasp {Side}] in ascolto - dita chiuse: {closed}/{minFingersClosed} - {(armed ? "presa pronta" : "apri la mano per armare la presa")}");
            }
            else
            {
                // Rilascio quando le dita si riaprono, o se lo slot del bridge non e' piu' la mia forma
                // (es. il manager ha confermato/azzerato la partita).
                bool stillMine = CurrentSlot() == held.gameObject;
                if (!closedEnough || !stillMine)
                {
                    if (debugLog) Debug.Log($"[GloveGrasp {Side}] rilascia (dita: {closed}, ancora mia: {stillMine})");
                    ReleaseIfHolding();
                }
            }
        }

        private string Side => isLeftHand ? "SX" : "DX";

        // Rilascia nel bridge la presa impostata da questa mano.
        private void ReleaseIfHolding()
        {
            if (held == null) return;
            if (CurrentSlot() == held.gameObject) bridge.ClearGrasp(isLeftHand);
            held = null;
        }

        // Dimentica solo lo stato locale, SENZA toccare il bridge (usato in demo/device assente).
        private void ForgetLocalOnly() => held = null;

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

        private RecognizableShape FindShapeNearHand()
        {
            if (grabPoint == null) return null;
            var hits = Physics.OverlapSphere(grabPoint.position, contactRadius);

            // Sceglie la forma PIÙ VICINA al palmo, non la prima restituita dal physics
            // engine (ordine arbitrario): con più forme nel raggio prenderebbe a caso.
            RecognizableShape best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var rec = hits[i].GetComponentInParent<RecognizableShape>();
                if (rec == null) continue;
                float d = Vector3.Distance(grabPoint.position, hits[i].ClosestPoint(grabPoint.position));
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
