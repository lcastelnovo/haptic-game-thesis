using UnityEngine;
using WeArt.Core;

namespace HapticResearch.Labyrinth
{
    // Taratura aptica del singolo partecipante. La sensibilita' termica varia molto fra
    // persone: questi valori NON vanno scritti nei MonoBehaviour, si regolano prima della
    // sessione (a mano, o dalla procedura di taratura) e finiscono nel log.
    //
    // Le misure del labirinto NON stanno qui ma nel MazeLayoutAsset: sono l'identita' del
    // labirinto, non una preferenza del partecipante. Per un'area piu' piccola si duplica
    // il layout, cosi' resta chiaro quale labirinto ha giocato chi.
    [CreateAssetMenu(menuName = "HapticResearch/Haptic Profile", fileName = "HapticProfile")]
    public class HapticProfile : ScriptableObject
    {
        [Header("Temperatura (0 = freddo, 0.5 = attuatore spento, 1 = caldo)")]
        [Tooltip("Valore del ramo corretto.")]
        [SerializeField, Range(0f, 1f)] private float warmValue = 0.80f;

        [Tooltip("Valore del ramo sbagliato.")]
        [SerializeField, Range(0f, 1f)] private float coldValue = 0.20f;

        [Tooltip("Valore a riposo: 0.5 lascia l'attuatore spento e gli permette di tornare neutro fra due letture.")]
        [SerializeField, Range(0f, 1f)] private float neutralValue = 0.50f;

        [Header("Sosta di lettura")]
        [Tooltip("Quanto il dito deve restare fermo sulla piastrella prima che la lettura sia valida. Sotto i 2 s l'attuatore Peltier non ha ancora raggiunto il valore: la lettura sarebbe un mezzo tepore, non un caldo.")]
        [SerializeField, Range(0.5f, 6f)] private float thermalDwellSeconds = 2.5f;

        [Tooltip("Margine dopo l'uscita dalla piastrella prima di considerare la sosta annullata: evita che un tremolio della mano azzeri la lettura.")]
        [SerializeField, Range(0f, 1f)] private float dwellGraceSeconds = 0.25f;

        [Header("Muri")]
        [Tooltip("Piu' alto degli 0.577 del vecchio labirinto: il muro deve essere inconfondibile, perche' e' l'unico riferimento di chi non vede.")]
        [SerializeField, Range(0f, 1f)] private float wallStiffness = 0.85f;

        [Tooltip("Ruvida e diversa da quella delle piastrelle.")]
        [SerializeField] private TextureType wallTexture = TextureType.CrushedRock;

        [SerializeField, Range(0f, 100f)] private float wallTextureVolume = 100f;

        [Header("Piastrelle di scelta")]
        [Tooltip("Liscia: serve a far capire al tatto di essere su una piastrella PRIMA di leggerla.")]
        [SerializeField] private TextureType tileTexture = TextureType.Leather;

        [SerializeField, Range(0f, 100f)] private float tileTextureVolume = 100f;

        [Tooltip("Piu' bassa di quella dei muri: la piastrella e' un pavimento, non un ostacolo.")]
        [SerializeField, Range(0f, 1f)] private float tileStiffness = 0.35f;

        [Header("Mano")]
        [Tooltip("Su quale mano attuare. Il labirinto e' pensato per una mano sola: con Both si attuano entrambe.")]
        [SerializeField] private HandSideSelection actuatedHand = HandSideSelection.Right;

        public enum HandSideSelection { Left, Right, Both }

        public float WarmValue => warmValue;
        public float ColdValue => coldValue;
        public float NeutralValue => neutralValue;
        public float ThermalDwellSeconds => thermalDwellSeconds;
        public float DwellGraceSeconds => dwellGraceSeconds;
        public float WallStiffness => wallStiffness;
        public TextureType WallTexture => wallTexture;
        public float WallTextureVolume => wallTextureVolume;
        public TextureType TileTexture => tileTexture;
        public float TileTextureVolume => tileTextureVolume;
        public float TileStiffness => tileStiffness;
        public HandSideSelection ActuatedHand => actuatedHand;

        // Scritti dalla procedura di taratura: si sovrascrivono i valori nominali con
        // quelli davvero discriminabili da questo partecipante.
        public void ApplyMeasuredThresholds(float warm, float cold)
        {
            warmValue = Mathf.Clamp01(warm);
            coldValue = Mathf.Clamp01(cold);
        }

        public bool ActuatesLeft => actuatedHand != HandSideSelection.Right;
        public bool ActuatesRight => actuatedHand != HandSideSelection.Left;
    }
}
