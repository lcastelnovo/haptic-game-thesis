using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;

namespace HapticResearch.Haptics
{
    // UNA sola connessione al middleware WEART per tutta la vita dell'app.
    //
    // Ogni livello ha il suo prefab WEART con il suo WeArtController, ma il SDK avvia UN solo
    // client per tutta l'app: in StartClients() il flag _clientsStarted e' STATICO, quindi
    // dal secondo controller in poi il client non parte mai. Nel livello caricato dopo il
    // primo (livello -> livello, o livello -> menu -> livello) il client resta non connesso:
    // HUD con middleware assente, niente dita, niente aptica. Col codice originale "a volte"
    // funzionava, perche' la scena nuova finiva per caso sul client del controller vecchio
    // ancora vivo; chiudendo quel client (primi tentativi di fix) non funzionava mai.
    //
    // Quindi il primo controller che lascia la sua scena diventa persistente
    // (DontDestroyOnLoad) e resta l'unico, con la sua connessione. E' il modello per cui il
    // SDK e' scritto: WeArtController e' un singleton, a ogni activeSceneChanged ricollega le
    // mani della scena nuova (GetAndAssignHandControllers) e i duplicati si distruggono da
    // soli (Destroy(this) in AssignVariablesFromScene). Qui si aggiunge quello che manca:
    //  - prima del cambio scena: il controller va alla radice e in DontDestroyOnLoad; dal suo
    //    client si staccano gli iscritti della scena che se ne va (se uno di loro lancia
    //    un'eccezione nel thread di ricezione, gli iscritti dopo non ricevono piu' niente);
    //  - dopo il caricamento: il controller della scena nuova viene spento prima del suo
    //    Start, cosi' non apre la seconda connessione; CalibrationManager, che nell'Awake
    //    potrebbe aver preso quello, viene ripuntato sul persistente.
    // Gli iscritti della scena nuova (dita, aptica, status tracker, HUD) passano da
    // WeArtController.Instance e trovano da soli il persistente.
    //
    // Il codice del SDK non si tocca: quello che e' privato si raggiunge per reflection.
    public static class WeArtSceneTransition
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly FieldInfo DevicesReadyField =
            typeof(WeArtStatusTracker).GetField("ConnectedDevicesReady", PrivateStatic);

        // Statico nel SDK: vale per tutti i controller.
        private static readonly FieldInfo VariablesAssignedField =
            typeof(WeArtController).GetField("_variablesAssigned", PrivateStatic);

        private static readonly string[] ClientEvents =
            { "OnConnectionStatusChanged", "OnMessage", "OnTextMessage", "OnError", "OnMessageResetHandClosure" };

        private static WeArtController persistent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => persistent = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        // Da chiamare subito prima di attivare la scena nuova (lo fa SceneFader).
        public static void PrepareForSceneChange()
        {
            var controller = persistent != null ? persistent : FindControllerInActiveScene();
            if (controller == null) return; // menu all'avvio: ancora nessun guanto

            if (persistent == null)
            {
                persistent = controller;
                controller.transform.SetParent(null, true);
                UnityEngine.Object.DontDestroyOnLoad(controller.gameObject);
            }

            // Cosi' all'activeSceneChanged il controller ricerca CalibrationManager della
            // scena nuova invece di tenersi quello distrutto.
            VariablesAssignedField?.SetValue(null, false);

            var keep = persistent.gameObject;
            foreach (var name in ClientEvents)
                RemoveSceneTargets(typeof(WeArtClient).GetField(name, PrivateInstance), persistent.Client, keep);
            RemoveSceneTargets(DevicesReadyField, null, keep);

            if (VariablesAssignedField == null || DevicesReadyField == null)
                Debug.LogWarning("[WeArtSceneTransition] Campi privati del SDK non trovati: versione WEART cambiata? Il guanto potrebbe non funzionare dopo il cambio scena.");
        }

        // Scena nuova caricata (Awake fatti, Start non ancora): i suoi controller si spengono.
        private static void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (persistent == null) return;

            foreach (var controller in UnityEngine.Object.FindObjectsByType<WeArtController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (controller == persistent) continue;
                // Spento non riceve Start: niente seconda connessione. Il SDK poi lo
                // distrugge da se' (AssignVariablesFromScene trova un'altra istanza).
                controller.enabled = false;
                // L'evento in corso lo chiama comunque (e lui si distrugge); da qui in poi
                // un controller distrutto non deve piu' ricevere cambi scena.
                SceneManager.activeSceneChanged -= controller.OnSceneChanged;
            }

            RepointField<CalibrationManager>("weArtController");
            RepointField<CalibrationAreaManager>("weArtController");

            // Lo status tracker e l'HUD della scena nuova partono da "disconnesso" finche' il
            // middleware non manda uno stato: lo si chiede subito.
            var client = persistent.Client;
            if (client != null && client.IsConnected)
            {
                client.SendMessage(new GetMiddlewareStatusMessage());
                client.SendMessage(new GetDevicesStatusMessage());
            }
        }

        private static void RepointField<T>(string fieldName) where T : MonoBehaviour
        {
            var field = typeof(T).GetField(fieldName, PrivateInstance);
            if (field == null) return;
            foreach (var component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                field.SetValue(component, persistent);
        }

        private static WeArtController FindControllerInActiveScene()
        {
            var active = SceneManager.GetActiveScene();
            foreach (var controller in UnityEngine.Object.FindObjectsByType<WeArtController>(FindObjectsSortMode.None))
                if (controller.gameObject.scene == active) return controller;
            return null;
        }

        // Toglie da un evento gli iscritti che sono oggetti Unity della scena che se ne va.
        // Restano quelli non Unity e quelli del controller persistente (keep).
        private static void RemoveSceneTargets(FieldInfo eventField, object owner, GameObject keep)
        {
            if (eventField == null) return;
            if (eventField.GetValue(owner) is not Delegate current) return;

            Delegate kept = null;
            foreach (var handler in current.GetInvocationList())
            {
                if (handler.Target is Component component && component != null && component.gameObject == keep)
                {
                    kept = Delegate.Combine(kept, handler);
                    continue;
                }
                if (handler.Target is UnityEngine.Object) continue;
                kept = Delegate.Combine(kept, handler);
            }
            eventField.SetValue(owner, kept);
        }
    }
}
