using UnityEngine;

namespace HapticResearch.Levels
{
    // Definizione di un tipo di forma riconoscibile in Level 1.
    // Tutto configurabile da Inspector: niente forme/clip hard-coded.
    [System.Serializable]
    public class ShapeDefinition
    {
        [Tooltip("Identificativo logico della forma (es. cubo, cilindro). Usato nei log e per il match.")]
        [SerializeField] private string id = "cubo";

        [Tooltip("Forma GIÀ piazzata in scena. Se assegnata, il manager usa questa (dov'è) invece di istanziare il Prefab.")]
        [SerializeField] private GameObject sceneInstance;

        [Tooltip("Prefab della forma. Usato SOLO se Scene Instance è vuoto. Deve essere afferrabile: GrabbableObject + WeArtTouchableObject (Graspable ON).")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Clip vocale che annuncia il bersaglio (es. \"trova il cubo\").")]
        [SerializeField] private AudioClip announceClip;

        public string Id => id;
        public GameObject SceneInstance => sceneInstance;
        public GameObject Prefab => prefab;
        public AudioClip AnnounceClip => announceClip;
    }
}
