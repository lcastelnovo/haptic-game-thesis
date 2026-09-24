using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using HapticResearch.Memory;

namespace HapticResearch.EditorTools
{
    // Cabla Level3_Memory.unity con il cablaggio comune (LevelSceneWiring) e controlla le
    // tessere con la checklist "Aggiungere oggetti touchable" di CLAUDE.md.
    //   Unity -batchmode -quit -projectPath . -executeMethod HapticResearch.EditorTools.MemorySetupTool.ConfigureHeadless
    public static class MemorySetupTool
    {
        public const string ScenePath = "Assets/Scenes/Level3_Memory.unity";

        private static readonly LevelWiringOptions Wiring = new LevelWiringOptions
        {
            LogTag = "[MemorySetup]",
            VoiceLevelId = "level3_memory",
            WelcomeKey = "level3m_welcome",
        };

        public static void ConfigureHeadless()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!Configure())
            {
                Debug.LogError("[MemorySetup] Configurazione fallita: scena NON salvata.");
                EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MemorySetup] Scena salvata.");
        }

        [MenuItem("HapticResearch/Level 3 Memory/Configura scena")]
        public static void ConfigureFromMenu() => Configure();

        [MenuItem("HapticResearch/Level 3 Memory/Ricostruisci da Level 1 (mani, demo, branding, voce)")]
        public static void RebuildFromLevel1()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError($"[MemorySetup] Apri {ScenePath} prima."); return; }
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
                Debug.LogError($"[MemorySetup] Apri {ScenePath} prima di lanciare il tool (scena attiva: {scene.path}).");
                return false;
            }

            bool ok = true;
            int changes = LevelSceneWiring.Wire(scene, Wiring, ref ok);
            ValidateTiles(asWarnings: true);

            if (changes > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MemorySetup] Fatto: {changes} blocchi aggiunti/aggiornati.");
            return ok;
        }

        [MenuItem("HapticResearch/Level 3 Memory/Valida tessere")]
        public static void ValidateTilesFromMenu() => ValidateTiles();

        public static bool ValidateTiles(bool asWarnings = false)
        {
            void Report(string message, Object context)
            {
                if (asWarnings) Debug.LogWarning(message, context);
                else Debug.LogError(message, context);
            }

            var manager = Object.FindFirstObjectByType<MemoryManager>(FindObjectsInactive.Include);
            if (manager == null) { Report("[MemorySetup] Nessun MemoryManager in scena.", null); return false; }
            var layout = manager.Layout;
            if (layout == null) { Report("[MemorySetup] Il MemoryManager non ha un MemoryLayoutAsset.", manager); return false; }
            if (!layout.Validate(out string layoutError))
            {
                Report($"[MemorySetup] Layout '{layout.LayoutId}' non valido: {layoutError}", layout);
                return false;
            }

            var tiles = Object.FindObjectsByType<MemoryTile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int expected = layout.Columns * layout.Rows;
            int problems = 0;
            if (tiles.Length != expected)
            {
                Report($"[MemorySetup] In scena ci sono {tiles.Length} tessere, il layout ne vuole {expected}: " +
                       "'HapticResearch/Level 3 Memory/Rigenera griglia'.", null);
                problems++;
            }

            var seen = new HashSet<int>();
            foreach (var t in tiles)
            {
                string who = t.gameObject.name;
                if (t.Index < 0 || t.Index >= expected || !seen.Add(t.Index))
                {
                    Report($"[MemorySetup] '{who}': indice {t.Index} fuori range o duplicato.", t);
                    problems++;
                }

                var col = t.GetComponent<Collider>();
                if (col == null || !col.isTrigger)
                {
                    Report($"[MemorySetup] '{who}': serve un Collider trigger.", t);
                    problems++;
                }

                var rb = t.GetComponent<Rigidbody>();
                if (rb == null || !rb.isKinematic || rb.useGravity)
                {
                    Report($"[MemorySetup] '{who}': serve un Rigidbody kinematic senza gravita' (senza, il sistema aptico ignora la tessera).", t);
                    problems++;
                }

                var touchable = t.GetComponent<WeArtTouchableObject>();
                if (touchable == null)
                {
                    Report($"[MemorySetup] '{who}': manca il WeArtTouchableObject.", t);
                    problems++;
                    continue;
                }
                if (!touchable.DisableDynamicForce)
                {
                    Report($"[MemorySetup] '{who}': 'Disable Dynamic Force' spento: la forza arriverebbe sbagliata sulle dita.", t);
                    problems++;
                }
                // Come nella colazione: la temperatura passa solo da ThermalObjectCue.
                if (touchable.Temperature.Active)
                {
                    Report($"[MemorySetup] '{who}': 'Temperature' attivo sul WeArtTouchableObject: " +
                           "scavalcherebbe minimo di tenuta e soppressione di ThermalObjectCue.", t);
                    problems++;
                }
            }

            if (problems == 0) Debug.Log($"[MemorySetup] {tiles.Length} tessere validate, nessun problema.");
            return problems == 0;
        }
    }
}
