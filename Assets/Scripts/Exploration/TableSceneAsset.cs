using System;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Core;

namespace HapticResearch.Exploration
{
    // Il dato dell'esperimento per il Level 3: quali oggetti ci sono sul tavolo, come si
    // sentono, come si chiamano e che richieste facoltative si possono fare.
    //
    // La GEOMETRIA non sta qui: mesh e posizioni vivono nella scena, perche' sono arte
    // (un labirinto e' fatto di cubi e si genera, una fetta di pane no). Qui sta cio' che
    // e' dato sperimentale. Per una variante si DUPLICA l'asset: cosi' nel log resta
    // scritto quale colazione ha esplorato chi.
    [CreateAssetMenu(menuName = "HapticResearch/Table Scene", fileName = "TableScene")]
    public class TableSceneAsset : ScriptableObject
    {
        [Serializable]
        public class ObjectEntry
        {
            [Tooltip("Chiave con cui l'oggetto in scena (SceneObjectBinding) si dichiara.")]
            [SerializeField] private string id;

            [Tooltip("Battuta col nome dell'oggetto, in Resources/Voice.")]
            [SerializeField] private string voiceKey;

            [Tooltip("Testo per sottotitoli, HUD e log.")]
            [SerializeField] private string label;

            [SerializeField] private TextureType texture = TextureType.Laminate;
            [SerializeField, Range(0f, 100f)] private float textureVolume = 100f;
            [SerializeField, Range(0f, 1f)] private float stiffness = 0.5f;

            [Tooltip("Solo il RUOLO: i valori di caldo e freddo vengono da HapticProfile, tarato sul partecipante.")]
            [SerializeField] private ThermalRole role = ThermalRole.Neutral;

            [Tooltip("Su questi pescano i suggerimenti: caldo, freddo, ruvido, liscio, metallo, stoffa...")]
            [SerializeField] private string[] tags = new string[0];

            [Tooltip("Spento per lo sfondo (la tovaglietta non e' una scoperta).")]
            [SerializeField] private bool discoverable = true;

            public string Id => id;
            public string VoiceKey => voiceKey;
            public string Label => string.IsNullOrEmpty(label) ? id : label;
            public TextureType Texture => texture;
            public float TextureVolume => textureVolume;
            public float Stiffness => stiffness;
            public ThermalRole Role => role;
            public IReadOnlyList<string> Tags => tags;
            public bool Discoverable => discoverable;

            public bool HasTag(string tag)
            {
                foreach (var t in tags) if (t == tag) return true;
                return false;
            }
        }

        [Serializable]
        public class SuggestionEntry
        {
            [SerializeField] private string tag;
            [Tooltip("La proposta: 'prova a trovare qualcosa di caldo'.")]
            [SerializeField] private string voiceKey;
            [Tooltip("La conferma quando il dito ci arriva.")]
            [SerializeField] private string metVoiceKey;

            public string Tag => tag;
            public string VoiceKey => voiceKey;
            public string MetVoiceKey => metVoiceKey;
        }

        [Tooltip("Finisce nel log: identifica QUESTA colazione fra le varianti.")]
        [SerializeField] private string sceneId = "colazione_v1";

        [SerializeField] private List<ObjectEntry> objects = new List<ObjectEntry>();
        [SerializeField] private List<SuggestionEntry> suggestions = new List<SuggestionEntry>();

        [Tooltip("Quanti oggetti termici si ammettono. Piu' di due e il Peltier non sta dietro al dito.")]
        [SerializeField, Range(0, 4)] private int maxThermalObjects = 2;

        public string SceneId => sceneId;
        public IReadOnlyList<ObjectEntry> Objects => objects;
        public IReadOnlyList<SuggestionEntry> Suggestions => suggestions;

        public int DiscoverableCount
        {
            get
            {
                int n = 0;
                foreach (var o in objects) if (o.Discoverable) n++;
                return n;
            }
        }

        public bool TryGet(string id, out ObjectEntry entry)
        {
            foreach (var o in objects)
            {
                if (o.Id != id) continue;
                entry = o;
                return true;
            }
            entry = null;
            return false;
        }

        public bool TryGetSuggestion(string tag, out SuggestionEntry entry)
        {
            foreach (var s in suggestions)
            {
                if (s.Tag != tag) continue;
                entry = s;
                return true;
            }
            entry = null;
            return false;
        }

        // Il prossimo tag da proporre, saltando quelli gia' usati. Null se non resta nulla.
        public string PickSuggestionTag(Func<string, bool> alreadyUsed)
        {
            foreach (var s in suggestions)
            {
                if (string.IsNullOrWhiteSpace(s.Tag)) continue;
                if (alreadyUsed != null && alreadyUsed(s.Tag)) continue;
                return s.Tag;
            }
            return null;
        }

        // Ponte verso il validatore puro: qui si traducono i tipi Unity in dati semplici.
        public bool Validate(out string error)
        {
            var infos = new List<SceneObjectInfo>(objects.Count);
            foreach (var o in objects)
            {
                var tags = new string[o.Tags.Count];
                for (int i = 0; i < tags.Length; i++) tags[i] = o.Tags[i];
                infos.Add(new SceneObjectInfo(o.Id, o.VoiceKey, o.Role, tags, o.Discoverable));
            }

            var tagList = new List<string>(suggestions.Count);
            foreach (var s in suggestions) tagList.Add(s.Tag);

            return TableSceneValidator.Validate(infos, tagList, maxThermalObjects, out error);
        }

        // Il dato incoerente si scopre salvando l'asset, non a sessione iniziata.
        private void OnValidate()
        {
            if (objects.Count == 0) return;
            if (!Validate(out string error))
                Debug.LogWarning($"[TableScene '{sceneId}'] {error}", this);
        }
    }
}
