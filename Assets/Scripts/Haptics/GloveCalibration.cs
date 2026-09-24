using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;
using HapticResearch.Audio;
using HapticResearch.Experiment;
using HapticResearch.Levels;

namespace HapticResearch.Haptics
{
    // Calibrazione unica del partecipante: dita del TouchDIVER (middleware WEART) + posizione
    // della mano rispetto al tavolo (Vive Tracker). E' la stessa operazione sia da Space sia
    // da "Avvia livello": la posa e' la stessa (palmo appoggiato al tavolo, dita distese e
    // perpendicolari al bordo), quindi si chiede una volta sola e si fanno tutte e due.
    //
    // Ordine:
    //  1. voce "appoggia la mano e tienila ferma", si aspetta che finisca + una breve pausa
    //     per dare tempo alla mano di arrivare in posa (la calibrazione delle dita fallisce
    //     se la mano si muove);
    //  2. calibrazione delle dita, fino all'esito del middleware o al timeout;
    //  3. allineamento del Vive Tracker, a mano ancora ferma nella posa;
    //  4. voce "fatto", poi la callback (per "Avvia livello": StartLevel).
    //
    // Senza middleware o senza guanto il passo 2 si salta e il livello parte lo stesso:
    // l'esito finisce nel log, cosi' in analisi si sa con che calibrazione ha giocato chi.
    //
    // Si auto-installa nelle scene con un LevelController (come OperatorHud).
    public class GloveCalibration : MonoBehaviour
    {
        public static GloveCalibration Instance { get; private set; }

        [Tooltip("Pausa dopo la voce, prima di calibrare: il tempo di appoggiare la mano.")]
        [SerializeField] private float settleSeconds = 1f;

        [Tooltip("Attesa massima dell'esito della calibrazione delle dita dal middleware.")]
        [SerializeField] private float gloveTimeoutSeconds = 12f;

        [Tooltip("Battuta vocale (voice_lines.json) che chiede di appoggiare la mano.")]
        [SerializeField] private string holdVoiceKey = "calibration_hold";

        [Tooltip("Battuta vocale a calibrazione finita.")]
        [SerializeField] private string doneVoiceKey = "calibration_done";

        public bool IsCalibrating { get; private set; }

        // Scritti dal thread di ricezione del client WEART, letti dalla coroutine.
        private volatile bool resultReceived;
        private volatile bool resultSuccess;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (_, _) => EnsureForActiveLevel();
            EnsureForActiveLevel();
        }

        private static void EnsureForActiveLevel()
        {
            var level = LevelController.Find();
            if (level == null || level.gameObject.scene != SceneManager.GetActiveScene()) return;
            Ensure();
        }

        // L'istanza della scena attiva, creata se manca.
        public static GloveCalibration Ensure()
        {
            var active = SceneManager.GetActiveScene();
            if (Instance != null && Instance.gameObject.scene == active) return Instance;
            foreach (var existing in FindObjectsByType<GloveCalibration>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing.gameObject.scene != active) continue;
                Instance = existing;
                return existing;
            }
            return new GameObject("GloveCalibration").AddComponent<GloveCalibration>();
        }

        void Awake()
        {
            // Al cambio scena la nuova arriva prima che la vecchia sia distrutta.
            if (Instance != null && Instance != this && Instance.gameObject.scene == gameObject.scene)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Avvia la calibrazione; onDone arriva a calibrazione finita (anche se fallita).
        // Ritorna false se una calibrazione e' gia' in corso: la richiesta viene ignorata.
        public bool Calibrate(string reason, Action onDone = null)
        {
            if (IsCalibrating) return false;
            StartCoroutine(Run(reason, onDone));
            return true;
        }

        private IEnumerator Run(string reason, Action onDone)
        {
            IsCalibrating = true;
            float startedAt = Time.unscaledTime;
            Log("calibration_start", $"{{\"reason\":\"{reason}\"}}");

            var narration = NarrationManager.Instance;
            if (narration != null)
            {
                narration.Speak(holdVoiceKey);
                while (narration.IsSpeaking) yield return null;
            }
            yield return new WaitForSecondsRealtime(settleSeconds);

            string glove = "no_middleware";
            var controller = WeArtController.Instance;
            var client = controller != null ? controller.Client : null;
            if (client != null && client.IsConnected)
            {
                // Dopo un cambio scena la sessione e' stata fermata e riparte da sola; se non
                // e' ancora RUNNING la si riavvia qui, altrimenti la calibrazione cadrebbe nel vuoto.
                var status = FindFirstObjectByType<WeArtStatusTracker>();
                if (status != null && status.CurrentStatus != MiddlewareStatus.RUNNING)
                {
                    controller.StartMiddleware(TrackingType.WEART_HAND);
                    float waitedStart = 0f;
                    while (status != null && status.CurrentStatus != MiddlewareStatus.RUNNING && waitedStart < gloveTimeoutSeconds)
                    {
                        waitedStart += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                resultReceived = false;
                client.OnMessage += OnClientMessage;
                controller.StartCalibration();

                float waited = 0f;
                while (!resultReceived && waited < gloveTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                client.OnMessage -= OnClientMessage;
                glove = !resultReceived ? "timeout" : resultSuccess ? "ok" : "failed";
            }

            // La mano e' ancora nella posa di calibrazione: si allinea anche il tracker.
            var tracker = FindFirstObjectByType<ViveTrackerCalibrationManager>();
            bool trackerDone = tracker != null && tracker.isActiveAndEnabled;
            if (trackerDone) tracker.CalibrateSpatial();

            if (narration != null)
            {
                narration.Speak(doneVoiceKey);
                while (narration.IsSpeaking) yield return null;
            }

            float seconds = Time.unscaledTime - startedAt;
            Log("calibration_end",
                $"{{\"reason\":\"{reason}\",\"glove\":\"{glove}\",\"tracker\":{(trackerDone ? "true" : "false")},\"seconds\":{seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}}}");
            if (glove != "ok")
                Debug.LogWarning($"[GloveCalibration] Calibrazione guanto: {glove}. Il livello parte lo stesso; ripetere con Space se serve.");

            IsCalibrating = false;
            onDone?.Invoke();
        }

        // Thread di ricezione del client: solo assegnazioni, niente API Unity.
        private void OnClientMessage(WeArtClient.MessageType type, IWeArtMessage message)
        {
            if (type != WeArtClient.MessageType.MessageReceived) return;
            if (message is TrackingCalibrationResult result)
            {
                resultSuccess = result.Success;
                resultReceived = true;
            }
        }

        private static void Log(string eventType, string json)
        {
            var logger = SessionLogger.Instance;
            if (logger == null) return;
            var level = LevelController.Find();
            logger.Log(level != null ? level.LevelId : SceneManager.GetActiveScene().name, eventType, json);
        }
    }
}
