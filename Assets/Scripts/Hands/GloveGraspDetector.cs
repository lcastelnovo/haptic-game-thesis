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

        private WeArtGraspBridge bridge;
        private RecognizableShape held; // forma attualmente segnata nel bridge da QUESTA mano

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

            // Attivo solo con device reale connesso: in demo la presa la gestisce HandDemoModeController,
            // e qui NON dobbiamo azzerare lo slot che ha impostato la demo.
            if (!IsDeviceConnected())
            {
                ForgetLocalOnly();
                return;
            }

            int closed = CountClosedFingers();

            if (held == null)
            {
                if (closed >= minFingersClosed)
                {
                    var shape = FindShapeNearHand();
                    if (shape != null)
                    {
                        held = shape;
                        bridge.SetGrasp(shape.gameObject, isLeftHand);
                    }
                }
            }
            else
            {
                // Rilascio quando le dita si riaprono, o se lo slot del bridge non e' piu' la mia forma
                // (es. il manager ha confermato/azzerato la partita).
                bool stillMine = CurrentSlot() == held.gameObject;
                if (closed < minFingersClosed || !stillMine)
                    ReleaseIfHolding();
            }
        }

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

        private bool IsDeviceConnected()
        {
            return WeArtController.Instance != null && WeArtController.Instance.Client != null;
        }

        private int CountClosedFingers()
        {
            int n = 0;
            if (fingerTracking != null)
                for (int i = 0; i < fingerTracking.Length; i++)
                    if (fingerTracking[i] != null && fingerTracking[i].Closure.Value >= closureThreshold) n++;
            if (thumbTracking != null && thumbTracking.Closure.Value >= closureThreshold) n++;
            return n;
        }

        private RecognizableShape FindShapeNearHand()
        {
            if (grabPoint == null) return null;
            var hits = Physics.OverlapSphere(grabPoint.position, contactRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                var rec = hits[i].GetComponentInParent<RecognizableShape>();
                if (rec != null) return rec;
            }
            return null;
        }
    }
}
