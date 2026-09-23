using UnityEngine;
using WeArt.Components;

namespace HapticResearch.Memory
{
    // Come si sente una tessera in questo momento.
    public enum TileFeel
    {
        Covered,     // neutra: nessuna texture, durezza media, uguale per tutte
        Signature,   // la sua firma (texture + durezza)
        Off,         // fuori gioco: niente texture, niente durezza, al tatto e' tavolo
    }

    // Sta su ogni tessera generata dal builder e porta SOLO il suo indice. Cosa deve
    // sentire il dito lo decide MemoryManager a partire dalla partita (MemoryBoard).
    [RequireComponent(typeof(Collider))]
    public class MemoryTile : MonoBehaviour
    {
        [Tooltip("Riga * colonne + colonna, in coordinate del partecipante. Lo scrive il builder.")]
        [SerializeField] private int index;

        [Tooltip("Se vuoto lo cerca su questo GameObject.")]
        [SerializeField] private WeArtTouchableObject touchable;

        [Tooltip("Se vuoto lo cerca su questo GameObject. Si spegne solo quando la tessera e' fuori gioco.")]
        [SerializeField] private Renderer tileRenderer;

        public int Index => index;

        void Awake()
        {
            if (touchable == null) touchable = GetComponent<WeArtTouchableObject>();
            if (tileRenderer == null) tileRenderer = GetComponent<Renderer>();
        }

        // Texture e Stiffness sono setter del SDK: assegnarli chiama gia'
        // UpdateTouchedHaptics(), quindi il cambio arriva anche al dito che e' gia' sopra
        // (e' il caso normale: la tessera si gira mentre il dito ci sta fermo).
        public void Apply(TileFeel feel, MemoryLayoutAsset.Signature signature, float coveredStiffness)
        {
            // Igiene sperimentale: in Game view coperta e girata sono identiche. Se
            // l'operatore vedesse la firma potrebbe suggerire la coppia senza volerlo.
            if (tileRenderer != null) tileRenderer.enabled = feel != TileFeel.Off;
            if (touchable == null) return;

            bool showSignature = feel == TileFeel.Signature && signature != null;
            touchable.Texture = new WeArt.Core.Texture
            {
                TextureType = showSignature ? signature.Texture : WeArt.Core.TextureType.Click,
                Volume = showSignature ? signature.TextureVolume : 0f,
                Active = showSignature,
            };
            touchable.Stiffness = new WeArt.Core.Force
            {
                Value = showSignature ? signature.Stiffness : coveredStiffness,
                Active = feel != TileFeel.Off,
            };

            // La temperatura non passa mai di qui (vedi ThermalObjectCue): se qualcuno
            // riaccende il campo nell'Inspector, lo si rispegne.
            var temperature = touchable.Temperature;
            if (temperature.Active)
            {
                temperature.Active = false;
                touchable.Temperature = temperature;
            }
        }
    }
}
