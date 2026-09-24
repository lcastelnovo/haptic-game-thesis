using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using HapticResearch.Branding;
using HapticResearch.Debugging;
using HapticResearch.Experiment;
using HapticResearch.Exploration;
using HapticResearch.Hands;
using HapticResearch.Levels;
using HapticResearch.Voice;

namespace HapticResearch.EditorTools
{
    // Cabla la scena della colazione (Level 3) con tutto quello che Level 1 ha e lei no:
    // mani (rig mouse/demo + componenti nostri sulle mani WEART), modalita' demo,
    // branding UniBS, comandi vocali, menu in-level, flusso di fine livello, logger di
    // sessione, pannello F1 e l'ExplorationManager.
    //
    // Level 1 e' la SOURCE OF TRUTH: i componenti che esistono gia' li' vengono COPIATI
    // (valori Inspector compresi) aprendo Level1_ShapeRecognition.unity in additiva e
    // rimappando i riferimenti per percorso (es. Table/TableTop, TopCamera). Cosi' se
    // qualcuno ritocca una mano in Level 1, basta rilanciare questo tool.
    //
    // Idempotente: ogni Ensure* aggiunge solo cio' che manca e NON tocca cio' che c'e' gia'
    // (oggetti spostati a mano, clip cambiati...). Per riallineare mani e singleton a una
    // Level 1 modificata c'e' la voce di menu "Ricostruisci da Level 1", che li cancella e
    // li ricrea. Da menu (con Level3 aperta) o headless:
    //   Unity -batchmode -quit -projectPath . -executeMethod HapticResearch.EditorTools.Level3SetupTool.ConfigureHeadless
    public static class Level3SetupTool
    {
        private const string ScenePath = "Assets/Scenes/Level3_Breakfast.unity";
        private const string TableScenePath = "Assets/Settings/Exploration/TableScene_Colazione_v1.asset";

        private static readonly LevelWiringOptions Wiring = new LevelWiringOptions
        {
            LogTag = "[Level3Setup]",
            VoiceLevelId = "level3_breakfast",
            WelcomeKey = "level3_welcome",
        };

        public static void ConfigureHeadless()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!Configure())
            {
                Debug.LogError("[Level3Setup] Configurazione fallita: scena NON salvata.");
                EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Level3Setup] Scena salvata.");
        }

        [MenuItem("HapticResearch/Level 3/Configura scena")]
        public static void ConfigureFromMenu() => Configure();

        // Cancella i blocchi copiati da Level 1 (mani, demo, branding, voce) e li ricrea:
        // da usare quando Level 1 cambia e si vuole riallineare la colazione.
        [MenuItem("HapticResearch/Level 3/Ricostruisci da Level 1 (mani, demo, branding, voce)")]
        public static void RebuildFromLevel1()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError($"[Level3Setup] Apri {ScenePath} prima."); return; }
            foreach (var name in LevelSceneWiring.RebuildRoots)
            {
                var go = LevelSceneWiring.FindRoot(scene, name);
                if (go != null) Undo.DestroyObjectImmediate(go);
            }
            Configure();
        }

        public static bool Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError($"[Level3Setup] Apri {ScenePath} prima di lanciare il tool (scena attiva: {scene.path}).");
                return false;
            }

            bool ok = true;
            int changes = LevelSceneWiring.Wire(scene, Wiring, ref ok);
            // Level 3 non ha la geometria da generare (quella del labirinto): solo il manager
            if (EnsureLevelManager(scene)) changes++;

            // La validazione del CONTENUTO gira, ma non fa fallire il cablaggio: alla
            // prima esecuzione - l'unica che crea tutto - l'ExplorationManager non ha
            // ancora una TableSceneAsset (la lascia vuota EnsureLevelManager di
            // proposito), quindi la validazione non puo' che essere negativa. Farla
            // pesare sull'esito significava, in headless, buttare via il cablaggio
            // appena prodotto con un Exit(1). Qui parla per warning; per gli errori
            // veri c'e' la voce di menu "Valida oggetti", da rilanciare dopo aver
            // riempito l'asset.
            ValidateObjects(asWarnings: true);

            if (changes > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Level3Setup] Fatto: {changes} blocchi aggiunti/aggiornati. " +
                      "Assegna la TableSceneAsset all'ExplorationManager, poi lancia " +
                      "'HapticResearch/Level 3/Valida oggetti'.");
            return ok;   // false SOLO per un cablaggio fallito (prefab mancanti, copia non riuscita)
        }

        // --- Validazione degli oggetti: checklist di CLAUDE.md ----

        // La checklist "Aggiungere oggetti touchable" di CLAUDE.md, eseguita da una
        // macchina invece che a memoria. Il classico "non sento niente" e' quasi sempre
        // uno di questi flag, e a occhio in Inspector non si nota. Ogni segnalazione
        // nomina l'oggetto e dice cosa manca, e passa il GameObject come contesto: cosi'
        // cliccando la riga in Console si seleziona il colpevole.
        [MenuItem("HapticResearch/Level 3/Valida oggetti")]
        public static void ValidateObjectsFromMenu() => ValidateObjects();

        // asWarnings: durante il cablaggio la scena e' ancora a meta' e un errore rosso
        // sarebbe rumore; da menu invece e' un controllo vero e parla in errori.
        public static bool ValidateObjects(bool asWarnings = false)
        {
            void Report(string message, UnityEngine.Object context)
            {
                if (asWarnings) Debug.LogWarning(message, context);
                else Debug.LogError(message, context);
            }

            var manager = Object.FindFirstObjectByType<ExplorationManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Report("[Level3Setup] Nessun ExplorationManager in scena.", null);
                return false;
            }

            var scene = manager.Scene;
            if (scene == null)
            {
                Report("[Level3Setup] L'ExplorationManager non ha una TableSceneAsset: " +
                       "assegnala nell'Inspector, altrimenti il livello non parte.", null);
                return false;
            }
            if (!scene.Validate(out string assetError))
            {
                Report($"[Level3Setup] Asset '{scene.SceneId}' non valido: {assetError}", null);
                return false;
            }

            var bindings = Object.FindObjectsByType<SceneObjectBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int problems = 0;
            var seen = new HashSet<string>();

            foreach (var b in bindings)
            {
                string who = b.gameObject.name;

                // Verificare che l'id esista nell'asset e non sia duplicato
                if (!scene.TryGet(b.Id, out _))
                {
                    Report($"[Level3Setup] '{who}': id '{b.Id}' assente dall'asset.", b);
                    problems++;
                }
                else if (!seen.Add(b.Id))
                {
                    Report($"[Level3Setup] '{who}': id '{b.Id}' usato da due oggetti in scena.", b);
                    problems++;
                }

                // Collider
                var col = b.GetComponent<Collider>();
                if (col == null)
                {
                    Report($"[Level3Setup] '{who}': manca il Collider.", b);
                    problems++;
                }
                else
                {
                    if (!col.isTrigger)
                    {
                        Report($"[Level3Setup] '{who}': il Collider non è trigger.", b);
                        problems++;
                    }
                    if (col is MeshCollider mesh && !mesh.convex)
                    {
                        Report($"[Level3Setup] '{who}': MeshCollider non convex.", b);
                        problems++;
                    }
                }

                // Rigidbody
                var rb = b.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    Report($"[Level3Setup] '{who}': manca il Rigidbody (senza, il sistema aptico ignora l'oggetto).", b);
                    problems++;
                }
                else if (!rb.isKinematic || rb.useGravity)
                {
                    Report($"[Level3Setup] '{who}': il Rigidbody deve essere kinematic e senza gravita'.", b);
                    problems++;
                }

                // WeArtTouchableObject e Disable Dynamic Force
                var touchable = b.GetComponent<WeArtTouchableObject>();
                if (touchable == null)
                {
                    Report($"[Level3Setup] '{who}': manca il WeArtTouchableObject.", b);
                    problems++;
                }
                else
                {
                    if (!touchable.DisableDynamicForce)
                    {
                        Report($"[Level3Setup] '{who}': 'Disable Dynamic Force' spento: la forza arriverebbe sbagliata sulle dita.", b);
                        problems++;
                    }

                    // La casella Temperature della checklist di CLAUDE.md, qui e SOLO qui,
                    // non va spuntata: nel Level 3 la temperatura la comanda
                    // ThermalObjectCue con un messaggio diretto, e quel percorso e' l'unico
                    // che rispetta un oggetto armato per volta, il minimo di tenuta e la
                    // soppressione. Col campo del SDK attivo quelle regole verrebbero
                    // scavalcate in silenzio, senza che niente in scena lo faccia notare.
                    if (touchable.Temperature.Active)
                    {
                        Report($"[Level3Setup] '{who}': 'Temperature' attivo sul WeArtTouchableObject. " +
                               "Nel Level 3 la temperatura passa solo da ThermalObjectCue: cosi' " +
                               "isteresi, minimo di tenuta e soppressione verrebbero scavalcati.", b);
                        problems++;
                    }
                }
            }

            // Oggetti dell'asset senza nessuno che li rappresenti in scena.
            foreach (var entry in scene.Objects)
            {
                if (seen.Contains(entry.Id)) continue;
                Report($"[Level3Setup] L'asset prevede '{entry.Id}' ma in scena non c'e' nessun oggetto con quell'id.", null);
                problems++;
            }

            if (problems == 0) Debug.Log($"[Level3Setup] {bindings.Length} oggetti validati, nessun problema.");
            return problems == 0;
        }

        private static bool EnsureLevelManager(Scene scene)
        {
            var existing = LevelSceneWiring.FindInScene<ExplorationManager>(scene);
            bool created = existing == null;
            var go = created ? LevelSceneWiring.NewInScene("Level3Manager", scene) : existing.gameObject;
            var manager = created ? go.AddComponent<ExplorationManager>() : existing;
            var so = new SerializedObject(manager);

            // Level 3 non ha lista di `bindings` pre-riempita nel tool: l'ExplorationManager
            // la carica autonomamente in Awake da tutti gli SceneObjectBinding della scena.
            // Qui si collega solo l'asset TableSceneAsset e il profilo, che rimangono vuoti
            // fino a che non li riempie l'operatore (il tool e' idempotente).
            // La colazione di default, se qualcuno l'ha gia' creata: e' l'unico asset che
            // il tool puo' indovinare senza sbagliare. Finche' non esiste il campo resta
            // vuoto e lo riempie l'operatore. Il profilo non si tocca: se resta vuoto
            // l'ExplorationManager ne crea uno di default, ma in sessione va assegnato
            // quello tarato nel Level 2.
            LevelSceneWiring.SetIfEmpty(so, "scene", AssetDatabase.LoadAssetAtPath<TableSceneAsset>(TableScenePath));

            so.ApplyModifiedPropertiesWithoutUndo();
            bool hasScene = so.FindProperty("scene").objectReferenceValue != null;
            Debug.Log($"[Level3Setup] ExplorationManager {(created ? "creato" : "aggiornato")}: " +
                      (hasScene ? "TableSceneAsset collegata" : "TableSceneAsset da assegnare") +
                      ", profilo aptico da assegnare (quello tarato nel Level 2).");
            return created;
        }
    }
}
