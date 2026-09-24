using UnityEngine;
using WeArt.Core;

namespace HapticResearch.Haptics
{
    // Ponte tra il grasp system del SDK WEART e la logica di gioco.
    //
    // Le mani WEART (guidate dai Vive Tracker) afferrano/rilasciano gli oggetti
    // tramite il loro sistema interno, che NON passa per i nostri
    // HandGrabController / GloveGrabController (quelli sono il fallback desktop).
    // Il SDK pero' espone un hub di eventi statici, WeArtGraspProvider, che
    // WeArtTouchableObject invoca a ogni presa/rilascio.
    //
    // Questo bridge si iscrive a quegli eventi (in sola lettura: la libreria WEART
    // non va toccata) e tiene traccia dell'oggetto attualmente afferrato da ciascuna
    // mano. Cosi' i manager di livello (es. ShapeRecognitionManager) possono sapere
    // "cosa tiene la mano" anche in VR con il guanto reale.
    public class WeArtGraspBridge : MonoBehaviour
    {
        // Istanza comoda da raggiungere (c'e' un solo bridge per scena di livello).
        public static WeArtGraspBridge Instance { get; private set; }

        private GameObject leftGrasped;
        private GameObject rightGrasped;

        // CHI ha scritto lo slot. In scena convivono piu' sorgenti di presa - il grasp
        // nativo del SDK, il rilevatore dei guanti, la demo col mouse - e dal solo nome
        // dell'oggetto afferrato non si capisce quale abbia parlato. Senza questo, una
        // presa sbagliata costringe a indovinare in quale dei tre percorsi cercare.
        private string leftSource;
        private string rightSource;

        // Oggetto fisico attualmente afferrato da ciascuna mano (null se nessuno).
        public GameObject LeftGrasped => leftGrasped;
        public GameObject RightGrasped => rightGrasped;

        // Sorgente della presa di ciascuna mano ("-" se lo slot e' vuoto).
        public string LeftSource => leftGrasped != null ? (leftSource ?? "?") : "-";
        public string RightSource => rightGrasped != null ? (rightSource ?? "?") : "-";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            // Iscrizione agli eventi statici di WEART (presa / rilascio).
            WeArtGraspProvider.OnGrasp += HandleGrasp;
            WeArtGraspProvider.OnRelease += HandleRelease;
        }

        private void OnDisable()
        {
            // Disiscrizione obbligatoria: gli eventi sono statici, altrimenti restano
            // handler appesi tra una scena e l'altra.
            WeArtGraspProvider.OnGrasp -= HandleGrasp;
            WeArtGraspProvider.OnRelease -= HandleRelease;

            if (Instance == this) Instance = null;
        }

        private void HandleGrasp(GameObject grabbed, HandSide hand)
        {
            // "sdk" = grasp nativo WEART, partito da WeArtTouchableObject passando per il
            // WeArtHandController delle mani del SDK: nessuno dei nostri script.
            if (hand == HandSide.Left) { leftGrasped = grabbed; leftSource = "sdk"; }
            else if (hand == HandSide.Right) { rightGrasped = grabbed; rightSource = "sdk"; }
        }

        private void HandleRelease(GameObject grabbed, HandSide hand)
        {
            // Azzera solo se a essere rilasciato e' proprio l'oggetto che risultava in mano.
            if (hand == HandSide.Left && leftGrasped == grabbed) leftGrasped = null;
            else if (hand == HandSide.Right && rightGrasped == grabbed) rightGrasped = null;
        }

        // --- Aggancio ESTERNO (demo mouse + guanti veri) ----------------------------------
        // Permette a una sorgente esterna di segnare/togliere un oggetto come "afferrato" da una
        // mano, ESATTAMENTE come farebbe il grasp nativo WEART, così i manager di livello reagiscono
        // allo stesso modo (path input-agnostico). Lo usano SIA la demo (HandDemoModeController, tasto
        // G) SIA i guanti veri (GloveGraspDetector, chiusura reale delle dita). NON tocca il flusso
        // device: HandleGrasp/HandleRelease restano per gli eventi WEART nativi. Uso un bool (mano
        // sinistra?) per non accoppiare i chiamanti a HandSide.
        public void SetGrasp(GameObject grabbed, bool leftHand, string source)
        {
            if (leftHand) { leftGrasped = grabbed; leftSource = source; }
            else { rightGrasped = grabbed; rightSource = source; }
        }

        public void ClearGrasp(bool leftHand)
        {
            if (leftHand) { leftGrasped = null; leftSource = null; }
            else { rightGrasped = null; rightSource = null; }
        }

        // Alias storici usati da HandDemoModeController (mantenuti per compatibilità).
        public void SetDemoGrasp(GameObject grabbed, bool leftHand) => SetGrasp(grabbed, leftHand, "demo");
        public void ClearDemoGrasp(bool leftHand) => ClearGrasp(leftHand);

        // Rilascia entrambe le mani (usato al riavvio del livello per una partita pulita).
        public void Clear()
        {
            leftGrasped = null;
            rightGrasped = null;
            leftSource = null;
            rightSource = null;
        }
    }
}
