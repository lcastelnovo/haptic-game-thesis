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

        // Da quanto e' viva la richiesta e chi l'ha soddisfatta: senza, in analisi resta
        // un tag senza sapere quale oggetto ha chiuso l'invito ne' quanto ci e' voluto.
        private float activeElapsed;
        private string metById;

        public bool HasActive => scheduler != null && scheduler.HasActive;
        public string ActiveTag => scheduler != null ? scheduler.ActiveTag : null;

        public void Configure(ExplorationManager explorationManager, TableSceneAsset tableScene, AudioClip suggestionTone)
        {
            manager = explorationManager;
            scene = tableScene;
            tone = suggestionTone;

            // Idempotente come ThermalObjectCue.Configure: Configure e' pubblico e un tool
            // di cablaggio potrebbe richiamarlo. Senza disiscrivere, lo scheduler vecchio
            // resterebbe agganciato ai nostri handler e ogni evento verrebbe loggato due volte.
            if (scheduler != null)
            {
                scheduler.OnSuggest -= HandleSuggest;
                scheduler.OnMet -= HandleMet;
                scheduler.OnDropped -= HandleDropped;
            }

            scheduler = new SuggestionScheduler(maxSuggestions, idleSeconds, everyNDiscoveries, timeoutSeconds)
            {
                TagPicker = () => scene != null ? scene.PickSuggestionTag(t => usedTags.Contains(t)) : null,
            };
            scheduler.OnSuggest += HandleSuggest;
            scheduler.OnMet += HandleMet;
            scheduler.OnDropped += HandleDropped;
            activeElapsed = 0f;
            metById = null;
        }

        public void ResetAll()
        {
            usedTags.Clear();
            scheduler?.Reset();
            activeElapsed = 0f;
            metById = null;
        }

        public void Tick(float dt)
        {
            if (scheduler == null) return;
            if (scheduler.HasActive) activeElapsed += dt;   // prima del Tick: la caduta lo legge
            var nm = NarrationManager.Instance;
            scheduler.NotifyNarration(nm != null && nm.IsSpeaking);
            scheduler.Tick(dt);
        }

        // Il livello si chiude con una richiesta ancora viva. Non e' un fallimento del
        // partecipante (era un invito), ma senza questa riga in analisi resterebbe un
        // suggestion_given senza esito, indistinguibile da un buco nel log.
        public void DropActiveOnFinish()
        {
            if (scheduler == null || !scheduler.HasActive) return;
            LogOutcome("suggestion_dropped", scheduler.ActiveTag, null, "fine_livello");
        }

        public void NotifyDiscovery() => scheduler?.NotifyDiscovery();

        // Il dito si e' fermato su un oggetto: se e' quello chiesto, la richiesta si chiude.
        public void NotifyTouched(SceneObjectBinding binding)
        {
            if (scheduler == null || binding == null || binding.Entry == null) return;
            if (!scheduler.HasActive) return;
            if (!binding.Entry.HasTag(scheduler.ActiveTag)) return;

            metById = binding.Id;   // letto da HandleMet, che dallo scheduler riceve solo il tag
            scheduler.NotifyTouched(scheduler.ActiveTag);
            metById = null;
        }

        private void HandleSuggest(string tag)
        {
            usedTags.Add(tag);
            if (scene != null && scene.TryGetSuggestion(tag, out var entry))
            {
                if (tone != null) manager.PlaySuggestionTone(tone);
                manager.Voice(entry.VoiceKey);
            }
            activeElapsed = 0f;
            LogOutcome("suggestion_given", tag, null, null);
        }

        private void HandleMet(string tag)
        {
            // Si ACCODA: la richiesta si soddisfa nello stesso frame in cui la voce
            // comincia a dire il nome dell'oggetto, e con Voice() la conferma lo
            // troncherebbe (Speak svuota la coda e ferma la sorgente). Se quella era
            // anche l'ultima scoperta, cancellerebbe pure "hai trovato tutta la colazione".
            if (scene != null && scene.TryGetSuggestion(tag, out var entry) &&
                !string.IsNullOrEmpty(entry.MetVoiceKey))
                manager.VoiceQueued(entry.MetVoiceKey);
            LogOutcome("suggestion_met", tag, metById, null);
        }

        // Cade in SILENZIO: nessuna voce, nessun suono. Ignorare un invito non e' sbagliare.
        private void HandleDropped(string tag) => LogOutcome("suggestion_dropped", tag, null, "timeout");

        // Tag, oggetto che ha soddisfatto la richiesta e millisecondi da quando e' partita:
        // e' quello che la spec chiede di poter leggere in analisi.
        private void LogOutcome(string eventType, string tag, string id, string reason)
        {
            int ms = Mathf.RoundToInt(activeElapsed * 1000f);
            string json = $"{{\"tag\":\"{tag}\",\"id\":\"{id ?? string.Empty}\",\"ms\":{ms}";
            if (!string.IsNullOrEmpty(reason)) json += $",\"reason\":\"{reason}\"";
            manager.Log(eventType, json + "}");
        }
    }
}
