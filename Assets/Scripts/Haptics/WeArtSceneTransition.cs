using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;

namespace HapticResearch.Haptics
{
    // Chiude il WeArtController della scena che sta per andarsene, PRIMA che la scena nuova
    // venga attivata. Lo chiama SceneFader subito prima di allowSceneActivation.
    //
    // Perche' serve: il WeArtController del SDK non e' pensato per piu' scene con un
    // controller ciascuna, e al cambio livello (livello -> livello, il menu non ne ha) fa tre
    // danni, tutti silenziosi:
    //  1. Unity fa l'Awake della scena nuova PRIMA di distruggere la vecchia. All'evento
    //     activeSceneChanged il controller nuovo trova in _instance quello vecchio ancora
    //     vivo e si distrugge da solo (Destroy(this) in AssignVariablesFromScene). Nel
    //     livello nuovo WeArtController.Instance resta null e il guanto sembra "bloccato".
    //  2. OnDestroy e' vuoto: socket TCP e thread di ricezione del client vecchio restano
    //     aperti. A ogni cambio livello si accumula una connessione orfana al middleware, e
    //     quando il socket cade il thread gira a vuoto al 100% di CPU.
    //  3. Le iscrizioni a eventi statici (activeSceneChanged, ConnectedDevicesReady) non
    //     vengono mai tolte: il controller distrutto riceve ancora eventi, lancia eccezioni
    //     e interrompe gli altri iscritti (CalibrationManager e controller della scena nuova).
    //
    // Qui si chiude la connessione della scena vecchia e si fa pulizia. La sessione del
    // middleware invece resta in RUNNING: il controller della scena nuova si riconnette e la
    // ritrova avviata. Con Client.Stop al cambio scena, nel livello dopo dita e aptica
    // restavano spente (test del 24 set 2026, livello -> menu -> livello): la sessione
    // fermata non ripartiva con lo start automatico del SDK.
    // Il codice del SDK non si tocca: quello che e' privato si raggiunge per reflection.
    public static class WeArtSceneTransition
    {
        private static readonly FieldInfo InstanceField =
            typeof(WeArtController).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly FieldInfo DevicesReadyField =
            typeof(WeArtStatusTracker).GetField("ConnectedDevicesReady", BindingFlags.NonPublic | BindingFlags.Static);

        // stopSession: true = ferma anche la sessione del middleware (vedi sopra: da non
        // usare al cambio scena), false = chiude solo la connessione di questa scena.
        public static void ReleaseCurrentController(bool stopSession = false)
        {
            var controllers = UnityEngine.Object.FindObjectsByType<WeArtController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var controller in controllers)
            {
                if (controller == null) continue;
                SceneManager.activeSceneChanged -= controller.OnSceneChanged;

                try
                {
                    // Stop() = messaggio di stop al middleware + chiusura socket e thread.
                    if (stopSession) controller.Client.Stop();
                    else controller.Client.StopConnection();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WeArtSceneTransition] Chiusura client fallita: {e.Message}");
                }

                // Spento, FindFirstObjectByType non lo trova piu': dalla scena nuova
                // WeArtController.Instance risolve sul controller giusto anche durante l'Awake.
                // Si spegne tutto il root WEART, non solo il controller: mani e grasping del
                // SDK leggono Instance in Update senza controllare null, e per i pochi frame
                // prima dell'unload riempirebbero la console di eccezioni.
                controller.transform.root.gameObject.SetActive(false);
            }

            RemoveUnityTargets(DevicesReadyField);
            InstanceField?.SetValue(null, null);

            if (InstanceField == null || DevicesReadyField == null)
                Debug.LogWarning("[WeArtSceneTransition] Campi privati del SDK non trovati: versione WEART cambiata? Il cambio scena puo' lasciare il guanto bloccato.");
        }

        // Toglie da un evento statico gli iscritti che sono oggetti Unity: appartengono tutti
        // alla scena che se ne va, quelli della scena nuova si iscrivono dopo, nel loro Awake/Start.
        private static void RemoveUnityTargets(FieldInfo eventField)
        {
            if (eventField == null) return;
            if (eventField.GetValue(null) is not Delegate current) return;

            Delegate kept = null;
            foreach (var handler in current.GetInvocationList())
            {
                if (handler.Target is UnityEngine.Object) continue;
                kept = Delegate.Combine(kept, handler);
            }
            eventField.SetValue(null, kept);
        }
    }
}
