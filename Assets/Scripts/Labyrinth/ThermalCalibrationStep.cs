using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;
using HapticResearch.Audio;
using HapticResearch.Experiment;
using HapticResearch.UI;
using HapticResearch.Voice;

namespace HapticResearch.Labyrinth
{
    // Taratura termica del partecipante, da eseguire PRIMA del labirinto.
    //
    // Perche' serve: la sensibilita' termica varia molto da persona a persona (eta',
    // circolazione, callosita' del polpastrello). Con valori fissi si rischia di chiedere
    // a qualcuno di distinguere due temperature che per lui sono la stessa cosa, e poi di
    // concludere che "il feedback termico non aiuta". La soglia misurata e' anche un dato
    // in piu' per la tesi.
    //
    // Metodo: scala adattiva 2-down / 1-up. Si parte da una differenza ampia rispetto al
    // neutro; due risposte giuste di fila la riducono, una sbagliata la aumenta. Dopo
    // qualche inversione la differenza oscilla intorno alla soglia. I valori di gioco si
    // fissano poi a un multiplo della soglia, non alla soglia stessa: durante il labirinto
    // il partecipante deve riconoscere il caldo con sicurezza, non al limite del possibile.
    //
    // Non serve toccare niente: il pad termico e' sempre a contatto col polpastrello,
    // quindi la temperatura si comanda direttamente al middleware.
    public class ThermalCalibrationStep : MonoBehaviour
    {
        [Header("Riferimenti")]
        [SerializeField] private HapticProfile profile;
        [SerializeField] private SessionLogger sessionLogger;
        [Tooltip("Se presente, il partecipante risponde a voce. Su Mac il riconoscimento e' inattivo: si usano i tasti.")]
        [SerializeField] private VoiceCommandController voiceCommands;

        [Header("Scala adattiva")]
        [Tooltip("Differenza iniziale rispetto al neutro: 0.30 significa 0.80 contro 0.20, cioe' gli estremi comodi.")]
        [SerializeField, Range(0.05f, 0.5f)] private float startDelta = 0.30f;

        [Tooltip("Sotto questa differenza non si scende: l'hardware non la riprodurrebbe in modo affidabile.")]
        [SerializeField, Range(0.01f, 0.2f)] private float minDelta = 0.04f;

        [Tooltip("Quanto si stringe dopo due risposte giuste (e si allarga dopo una sbagliata).")]
        [SerializeField, Range(0.5f, 0.95f)] private float stepFactor = 0.75f;

        [SerializeField, Range(2, 12)] private int maxReversals = 6;
        [SerializeField, Range(4, 40)] private int maxTrials = 16;

        [Tooltip("Su quante ultime inversioni si media la soglia.")]
        [SerializeField, Range(2, 8)] private int reversalsAveraged = 4;

        [Tooltip("I valori di gioco si mettono a questo multiplo della soglia: al limite non si gioca.")]
        [SerializeField, Range(1f, 4f)] private float safetyFactor = 2f;

        [Header("Tempi (s)")]
        [Tooltip("Ritorno al neutro fra due prove. Serve generoso: scendere da 0.8 a 0.5 e' la transizione piu' lenta.")]
        [SerializeField, Range(1f, 8f)] private float settleSeconds = 3f;

        [Tooltip("Permanenza sul valore prima di chiedere: sotto i 2 s l'attuatore non ci e' ancora arrivato.")]
        [SerializeField, Range(1f, 8f)] private float presentSeconds = 3f;

        [Tooltip("Oltre questo tempo senza risposta si ripete la domanda.")]
        [SerializeField, Range(3f, 30f)] private float answerTimeout = 12f;

        [Header("Controlli operatore")]
        [SerializeField] private KeyCode startKey = KeyCode.K;
        [Tooltip("Ripiego quando il riconoscimento vocale non c'e' (Mac, microfono assente).")]
        [SerializeField] private KeyCode warmKey = KeyCode.C;
        [SerializeField] private KeyCode coldKey = KeyCode.F;
        [SerializeField] private KeyCode abortKey = KeyCode.Escape;

        private static readonly string[] WarmPhrases = { "caldo", "e' caldo", "calda", "e' calda" };
        private static readonly string[] ColdPhrases = { "freddo", "e' freddo", "fredda", "e' fredda" };

        // --- stato ---
        private Coroutine routine;
        private bool answered;
        private bool answerWarm;
        private Action warmAction, coldAction;

        public bool Running => routine != null;

        // true = taratura completata, false = interrotta.
        public event Action<bool> OnFinished;

        public float MeasuredThreshold { get; private set; } = -1f;

        void Awake()
        {
            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            if (voiceCommands == null) voiceCommands = FindFirstObjectByType<VoiceCommandController>(FindObjectsInactive.Include);

            // In Awake, cosi' le frasi entrano nel vocabolario prima che il
            // VoiceCommandController lo costruisca in Start.
            if (voiceCommands != null)
            {
                warmAction = () => Answer(true);
                coldAction = () => Answer(false);
                voiceCommands.RegisterCommand(WarmPhrases, warmAction);
                voiceCommands.RegisterCommand(ColdPhrases, coldAction);
            }
        }

        void Update()
        {
            if (!Running)
            {
                if (Input.GetKeyDown(startKey)) Begin();
                return;
            }

            if (Input.GetKeyDown(abortKey)) { Abort(); return; }
            if (Input.GetKeyDown(warmKey)) Answer(true);
            else if (Input.GetKeyDown(coldKey)) Answer(false);
        }

        // --- Flusso ----------------------------------------------------------------

        public void Begin()
        {
            if (Running) return;
            if (profile == null) { Debug.LogError("[Taratura] Manca l'HapticProfile."); return; }
            routine = StartCoroutine(Run());
        }

        public void Abort()
        {
            if (!Running) return;
            StopCoroutine(routine);
            routine = null;
            SendTemperature(profile.NeutralValue);
            StopTemperature();
            Log("calib_abort", "{}");
            OnFinished?.Invoke(false);
        }

        private IEnumerator Run()
        {
            float neutral = profile.NeutralValue;
            float delta = startDelta;
            int correctStreak = 0;
            int trial = 0;
            bool? lastDirectionWasDown = null;
            var reversals = new List<float>();
            bool middleware = HasClient();

            Log("calib_start",
                $"{{\"startDelta\":{F(startDelta)},\"neutral\":{F(neutral)},\"middleware\":{(middleware ? "true" : "false")}}}");

            Speak("level2_calib_intro");
            yield return WaitForSpeech();

            SendTemperature(neutral);
            yield return new WaitForSeconds(settleSeconds);

            while (trial < maxTrials && reversals.Count < maxReversals)
            {
                trial++;
                bool presentWarm = UnityEngine.Random.value < 0.5f;
                float value = Mathf.Clamp01(neutral + (presentWarm ? delta : -delta));

                SendTemperature(value);
                yield return new WaitForSeconds(presentSeconds);

                bool got = false;
                for (int attempt = 0; attempt < 2 && !got; attempt++)
                {
                    if (attempt > 0) Speak("level2_calib_repeat");
                    else Speak("level2_calib_ask");
                    yield return WaitForSpeech();

                    answered = false;
                    float deadline = Time.time + answerTimeout;
                    while (!answered && Time.time < deadline) yield return null;
                    got = answered;
                }

                // Nessuna risposta: si torna al neutro e si riprova con la stessa differenza.
                if (!got)
                {
                    Log("calib_trial",
                        $"{{\"n\":{trial},\"delta\":{F(delta)},\"presented\":\"{(presentWarm ? "caldo" : "freddo")}\"," +
                        $"\"answer\":\"nessuna\",\"correct\":false}}");
                    SendTemperature(neutral);
                    yield return new WaitForSeconds(settleSeconds);
                    continue;
                }

                bool correct = answerWarm == presentWarm;
                VoiceSubtitles.ReportHeard(answerWarm ? "caldo" : "freddo", "-", true,
                                           correct ? "giusto" : "sbagliato");

                // 2-down / 1-up: due giuste di fila stringono, una sbagliata allarga.
                bool goingDown;
                if (correct)
                {
                    correctStreak++;
                    if (correctStreak < 2) goingDown = false;
                    else { correctStreak = 0; goingDown = true; }
                }
                else { correctStreak = 0; goingDown = false; }

                bool changed = correct ? goingDown : true;
                if (changed)
                {
                    bool down = correct;
                    if (lastDirectionWasDown.HasValue && lastDirectionWasDown.Value != down)
                        reversals.Add(delta);
                    lastDirectionWasDown = down;
                    delta = down ? Mathf.Max(minDelta, delta * stepFactor)
                                 : Mathf.Min(startDelta, delta / stepFactor);
                }

                Log("calib_trial",
                    $"{{\"n\":{trial},\"delta\":{F(delta)},\"presented\":\"{(presentWarm ? "caldo" : "freddo")}\"," +
                    $"\"answer\":\"{(answerWarm ? "caldo" : "freddo")}\",\"correct\":{(correct ? "true" : "false")}," +
                    $"\"reversals\":{reversals.Count}}}");

                SendTemperature(neutral);
                yield return new WaitForSeconds(settleSeconds);
            }

            float threshold = Threshold(reversals, delta);
            MeasuredThreshold = threshold;

            float margin = Mathf.Clamp(threshold * safetyFactor, minDelta, 0.5f);
            float warm = Mathf.Clamp01(neutral + margin);
            float cold = Mathf.Clamp01(neutral - margin);
            profile.ApplyMeasuredThresholds(warm, cold);

            StopTemperature();
            Log("calib_done",
                $"{{\"warm\":{F(warm)},\"cold\":{F(cold)},\"threshold\":{F(threshold)}," +
                $"\"trials\":{trial},\"reversals\":{reversals.Count}}}");
            Debug.Log($"[Taratura] Soglia {threshold:0.000} su {trial} prove. Valori di gioco: caldo {warm:0.00}, freddo {cold:0.00}.");

            Speak("level2_calib_done");
            routine = null;
            OnFinished?.Invoke(true);
        }

        // Media delle ultime inversioni. Senza inversioni (partecipante che sbaglia
        // subito, o prove finite) si usa la differenza a cui si e' arrivati: e' una stima
        // grossolana ma onesta, e nel log si vede che le inversioni erano zero.
        private float Threshold(List<float> reversals, float currentDelta)
        {
            if (reversals.Count == 0) return currentDelta;
            int take = Mathf.Min(reversalsAveraged, reversals.Count);
            float sum = 0f;
            for (int i = reversals.Count - take; i < reversals.Count; i++) sum += reversals[i];
            return sum / take;
        }

        private void Answer(bool warm)
        {
            if (!Running || answered) return;
            answered = true;
            answerWarm = warm;
        }

        // --- Voce e middleware ---------------------------------------------------------

        private void Speak(string key)
        {
            var nm = NarrationManager.Instance;
            if (nm != null && nm.Has(key)) nm.Speak(key);
            else Debug.LogWarning($"[Taratura] Traccia vocale '{key}' mancante.");
        }

        private IEnumerator WaitForSpeech()
        {
            var nm = NarrationManager.Instance;
            if (nm == null) yield break;
            yield return null; // un frame perche' IsSpeaking diventi vero
            while (nm.IsSpeaking) yield return null;
        }

        private bool HasClient() =>
            WeArtController.Instance != null && WeArtController.Instance.Client != null;

        private void SendTemperature(float value)
        {
            if (!HasClient()) return;
            var client = WeArtController.Instance.Client;
            if (profile.ActuatesLeft) client.SendMessage(new SetTemperatureMessage
            { Temperature = value, HandSide = HandSide.Left, ActuationPoint = ActuationPoint.Index });
            if (profile.ActuatesRight) client.SendMessage(new SetTemperatureMessage
            { Temperature = value, HandSide = HandSide.Right, ActuationPoint = ActuationPoint.Index });
        }

        private void StopTemperature()
        {
            if (!HasClient()) return;
            var client = WeArtController.Instance.Client;
            if (profile.ActuatesLeft) client.SendMessage(new StopTemperatureMessage
            { HandSide = HandSide.Left, ActuationPoint = ActuationPoint.Index });
            if (profile.ActuatesRight) client.SendMessage(new StopTemperatureMessage
            { HandSide = HandSide.Right, ActuationPoint = ActuationPoint.Index });
        }

        void OnDestroy()
        {
            if (voiceCommands != null)
            {
                voiceCommands.UnregisterCommand(warmAction);
                voiceCommands.UnregisterCommand(coldAction);
            }
            if (Running) StopTemperature();
        }

        private static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        private void Log(string eventType, string json)
        {
            if (sessionLogger == null) sessionLogger = SessionLogger.Instance;
            sessionLogger?.Log("level2_calibration", eventType, json);
        }
    }
}
