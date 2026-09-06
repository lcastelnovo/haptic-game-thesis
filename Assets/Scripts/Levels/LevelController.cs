using UnityEngine;

namespace HapticResearch.Levels
{
    // Base comune dei livelli: quello che operatore (HUD), comandi vocali, menu in-level e
    // flusso di fine livello devono sapere di un livello SENZA conoscerne la logica.
    // Level 1 = ShapeRecognitionManager, Level 2 = LabyrinthManager.
    //
    // Chi ha bisogno "del livello della scena" usa LevelController.Find(): cosi' gli stessi
    // script (VoiceCommandController, OperatorHud, LevelFlowController...) girano identici
    // in tutti i livelli.
    public abstract class LevelController : MonoBehaviour
    {
        // Id per i log (es. "level1_shape_recognition").
        public abstract string LevelId { get; }

        // Numero e titolo mostrati all'operatore ("LIVELLO 1" / "Riconoscimento forme").
        public abstract int LevelNumber { get; }
        public abstract string LevelTitle { get; }

        public abstract bool IsRunning { get; }
        public abstract bool IsComplete { get; }

        // Riga di stato breve per l'HUD ("in attesa di avvio", "round 2/4 · trova: cubo"...).
        public abstract string StatusLine { get; }

        // Secondi dal via (0 se il livello non e' mai partito; fermo a fine livello).
        public abstract float ElapsedSeconds { get; }

        // Avvia / riavvia il livello (nuovo partecipante) da qualunque stato.
        public abstract void StartLevel();

        // Ri-annuncia a voce l'obiettivo corrente.
        public abstract void RepeatAnnouncement();

        // Il livello della scena corrente (uno per scena). Null nel menu principale.
        public static LevelController Current { get; private set; }

        protected virtual void OnEnable() { Current = this; }
        protected virtual void OnDisable() { if (Current == this) Current = null; }

        public static LevelController Find()
        {
            if (Current != null) return Current;
            return FindFirstObjectByType<LevelController>(FindObjectsInactive.Include);
        }

        // Al cambio scena l'OnEnable del livello nuovo arriva PRIMA dell'OnDisable del
        // vecchio: il vecchio non deve azzerare Current (gia' gestito da 'Current == this').
    }
}
