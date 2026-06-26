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

        // Oggetto fisico attualmente afferrato da ciascuna mano (null se nessuno).
        public GameObject LeftGrasped => leftGrasped;
        public GameObject RightGrasped => rightGrasped;

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
            if (hand == HandSide.Left) leftGrasped = grabbed;
            else if (hand == HandSide.Right) rightGrasped = grabbed;
        }

        private void HandleRelease(GameObject grabbed, HandSide hand)
        {
            // Azzera solo se a essere rilasciato e' proprio l'oggetto che risultava in mano.
            if (hand == HandSide.Left && leftGrasped == grabbed) leftGrasped = null;
            else if (hand == HandSide.Right && rightGrasped == grabbed) rightGrasped = null;
        }
    }
}
