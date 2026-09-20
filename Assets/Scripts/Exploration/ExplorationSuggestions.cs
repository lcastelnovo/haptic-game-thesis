using System.Collections.Generic;
using UnityEngine;
using HapticResearch.Audio;

namespace HapticResearch.Exploration
{
    // Involucro Unity dello SuggestionScheduler: la logica dei tempi sta nello scheduler
    // (provata fuori da Unity), qui ci sono solo voce, suono e tag dell'asset.
    public class ExplorationSuggestions : MonoBehaviour
    {
        [Tooltip("Secondi senza scoperte nuove prima di proporre qualcosa.")]
        [SerializeField, Range(5f, 120f)] private float idleSeconds = 25f;

        [Tooltip("Ogni quante scoperte proporre, senza aspettare lo stallo. 0 = mai.")]
        [SerializeField, Range(0, 10)] private int everyNDiscoveries = 3;

        [Tooltip("Quanto resta viva una richiesta prima di cadere in silenzio.")]
        [SerializeField, Range(10f, 180f)] private float timeoutSeconds = 60f;

        [Tooltip("Tetto per livello: e' un invito, non un compito.")]
        [SerializeField, Range(0, 8)] private int maxSuggestions = 4;

        private ExplorationManager manager;
        private TableSceneAsset scene;
        private AudioClip tone;
        private SuggestionScheduler scheduler;
        private readonly HashSet<string> usedTags = new HashSet<string>();

        public bool HasActive => scheduler != null && scheduler.HasActive;
        public string ActiveTag => scheduler != null ? scheduler.ActiveTag : null;

        public void Configure(ExplorationManager explorationManager, TableSceneAsset tableScene, AudioClip suggestionTone)
        {
            manager = explorationManager;
            scene = tableScene;
            tone = suggestionTone;

            scheduler = new SuggestionScheduler(maxSuggestions, idleSeconds, everyNDiscoveries, timeoutSeconds)
            {
                TagPicker = () => scene != null ? scene.PickSuggestionTag(t => usedTags.Contains(t)) : null,
            };
            scheduler.OnSuggest += HandleSuggest;
            scheduler.OnMet += HandleMet;
            scheduler.OnDropped += HandleDropped;
        }

        public void ResetAll()
        {
            usedTags.Clear();
            scheduler?.Reset();
        }

        public void Tick(float dt)
        {
            if (scheduler == null) return;
            var nm = NarrationManager.Instance;
            scheduler.NotifyNarration(nm != null && nm.IsSpeaking);
            scheduler.Tick(dt);
        }

        public void NotifyDiscovery() => scheduler?.NotifyDiscovery();

        // Il dito si e' fermato su un oggetto: se e' quello chiesto, la richiesta si chiude.
        public void NotifyTouched(SceneObjectBinding binding)
        {
            if (scheduler == null || binding == null || binding.Entry == null) return;
            if (!scheduler.HasActive) return;
            if (binding.Entry.HasTag(scheduler.ActiveTag))
                scheduler.NotifyTouched(scheduler.ActiveTag);
        }

        private void HandleSuggest(string tag)
        {
            usedTags.Add(tag);
            if (scene != null && scene.TryGetSuggestion(tag, out var entry))
            {
                if (tone != null) manager.PlaySuggestionTone(tone);
                manager.Voice(entry.VoiceKey);
            }
            manager.Log("suggestion_given", $"{{\"tag\":\"{tag}\"}}");
        }

        private void HandleMet(string tag)
        {
            if (scene != null && scene.TryGetSuggestion(tag, out var entry) &&
                !string.IsNullOrEmpty(entry.MetVoiceKey))
                manager.Voice(entry.MetVoiceKey);
            manager.Log("suggestion_met", $"{{\"tag\":\"{tag}\"}}");
        }

        // Cade in SILENZIO: nessuna voce, nessun suono. Ignorare un invito non e' sbagliare.
        private void HandleDropped(string tag) => manager.Log("suggestion_dropped", $"{{\"tag\":\"{tag}\"}}");
    }
}
