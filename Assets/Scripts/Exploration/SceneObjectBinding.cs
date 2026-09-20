using UnityEngine;
using WeArt.Components;

namespace HapticResearch.Exploration
{
    // Sta su ogni oggetto della colazione e porta SOLO un id. Tutto il resto (nome,
    // texture, durezza, ruolo termico) arriva dall'asset: e' l'unico punto in cui scena e
    // dato possono divergere, e quando succede deve farsi sentire, non fallire in
    // silenzio a sessione iniziata.
    [RequireComponent(typeof(Collider))]
    public class SceneObjectBinding : MonoBehaviour
    {
        [Tooltip("Deve combaciare con un id della TableSceneAsset del livello.")]
        [SerializeField] private string id;

        [Tooltip("Se vuoto lo cerca su questo GameObject: e' lui a ricevere texture e stiffness.")]
        [SerializeField] private WeArtTouchableObject touchable;

        private Collider cachedCollider;
        private TableSceneAsset.ObjectEntry entry;

        public string Id => id;
        public TableSceneAsset.ObjectEntry Entry => entry;
        public Collider Collider => cachedCollider;
        public string Label => entry != null ? entry.Label : id;
        public ThermalRole Role => entry != null ? entry.Role : ThermalRole.Neutral;

        void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            if (touchable == null) touchable = GetComponent<WeArtTouchableObject>();
        }

        // Chiamato dal manager all'avvio: lega l'oggetto al dato e ne applica i parametri.
        public bool Bind(TableSceneAsset scene)
        {
            if (cachedCollider == null) cachedCollider = GetComponent<Collider>();

            if (scene == null || !scene.TryGet(id, out entry))
            {
                Debug.LogError($"[Level3] L'oggetto '{name}' dichiara l'id '{id}', che non esiste " +
                               "nella TableSceneAsset del livello: resterebbe muto e senza texture.", this);
                entry = null;
                return false;
            }

            if (touchable == null)
            {
                Debug.LogWarning($"[Level3] '{name}' non ha un WeArtTouchableObject: " +
                                 "si sentira' solo come collisione, senza texture ne' durezza.", this);
                return true;
            }

            // Nell'SDK v2.3.0 non esiste un tipo "Stiffness": la durezza e' un WeArt.Core.Force
            // (proprieta' WeArtTouchableObject.Stiffness di tipo Force). Le due proprieta' sono
            // setter, non campi: assegnarle chiama gia' UpdateTouchedHaptics() internamente, quindi
            // il valore raggiunge davvero il dito che sta gia' toccando l'oggetto.
            touchable.Texture = new WeArt.Core.Texture
            {
                TextureType = entry.Texture,
                Volume = entry.TextureVolume,
                Active = true,
            };
            touchable.Stiffness = new WeArt.Core.Force
            {
                Value = entry.Stiffness,
                Active = true,
            };

            // La temperatura NON passa di qui. Tutto il canale termico del livello (un
            // oggetto armato per volta, minimo di tenuta, grazia, soppressione) si regge
            // sul messaggio diretto di ThermalObjectCue: il campo Temperature del
            // WeArtTouchableObject scavalcherebbe quelle regole in silenzio, e la
            // checklist di CLAUDE.md dice di spuntarlo. Qui il DATO vince sempre: lo si
            // spegne, e il tool di validazione lo segnala come errore a chi lo riaccende.
            var temperature = touchable.Temperature;
            temperature.Active = false;
            touchable.Temperature = temperature;
            return true;
        }
    }
}
