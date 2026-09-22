using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using WeArt.Core;
using HapticResearch.Exploration;
using HapticResearch.Labyrinth;

namespace HapticResearch.EditorTools
{
    // Genera il dato e la scena del Level 3 (colazione) che finora si dovevano comporre a
    // mano nell'editor: sette oggetti con quote al millimetro e undici righe di Inspector
    // ciascuno. Stessa filosofia di MazeGeometryBuilder: il layout/asset e' l'unica fonte,
    // la scena si rigenera da li'.
    //
    // Qui pero' la scena stessa non esiste ancora: a differenza di Labyrinth.unity, che e'
    // gia' in repo e si aggiorna sul posto, Level3_Breakfast.unity va CREATA duplicando il
    // template ViveTrackerScene.unity. Sovrascrivere quella copia col tool distruggerebbe
    // qualunque rifinitura fatta a mano, quindi il comando "normale" si ferma se la scena
    // c'e' gia': la creazione da zero e l'aggiornamento del contenuto sono due comandi
    // separati (vedi i menu qui sotto).
    public static class Level3SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Level3_Breakfast.unity";
        private const string TemplateScenePath = "Assets/Scenes/ViveTrackerScene.unity";

        private const string AssetDir = "Assets/Settings/Exploration";
        private const string AssetPath = AssetDir + "/TableScene_Colazione_v1.asset";

        private const string ProfilePath = "Assets/Settings/Labyrinth/HapticProfile_Default.asset";
        private const string ContactClipPath = "Assets/Audio/Level2/wall_bump.mp3";
        private const string DiscoveryClipPath = "Assets/Audio/Level2/checkpoint_chime.mp3";
        private const string SuggestionClipPath = "Assets/Audio/Level3/suggestion_tone.mp3";

        private const string TableRootName = "Table";
        private const string ColazioneRootName = "Colazione";
        private const string ManagerName = "Level3Manager";

        // --- Menu: dato -----------------------------------------------------------------

        [MenuItem("HapticResearch/Level 3/Genera dati colazione (asset)")]
        public static void GenerateAssetFromMenu()
        {
            var asset = EnsureTableSceneAsset(out bool created);
            if (!asset.Validate(out string error))
            {
                Debug.LogError($"[Level3Builder] L'asset '{AssetPath}' non e' valido, controlla i dati: {error}", asset);
                return;
            }
            Debug.Log($"[Level3Builder] {(created ? "Creato" : "Aggiornato")} {AssetPath}: " +
                      $"{asset.Objects.Count} oggetti, {asset.Suggestions.Count} suggerimenti, dato valido.", asset);
        }

        // --- Menu: scena (crea da zero, si ferma se esiste gia') ------------------------

        [MenuItem("HapticResearch/Level 3/Genera scena colazione")]
        public static void GenerateSceneFromMenu()
        {
            if (TryGenerateScene(forceRecreate: false, out string report)) Debug.Log("[Level3Builder] " + report);
            else Debug.LogError("[Level3Builder] " + report);
        }

        // --- Menu: rigenerazione distruttiva, con conferma -------------------------------

        [MenuItem("HapticResearch/Level 3/Rigenera scena da zero (ATTENZIONE: cancella)")]
        public static void RegenerateSceneFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rigenera Level3_Breakfast da zero",
                $"Questo CANCELLA '{ScenePath}' se esiste gia' e la ricrea da zero copiando il template. " +
                "Qualunque rifinitura fatta a mano nella scena andra' persa. Procedere?",
                "Cancella e rigenera", "Annulla");
            if (!confirmed)
            {
                Debug.Log("[Level3Builder] Rigenerazione annullata, nessuna modifica.");
                return;
            }

            if (TryGenerateScene(forceRecreate: true, out string report)) Debug.Log("[Level3Builder] " + report);
            else Debug.LogError("[Level3Builder] " + report);
        }

        // --- Menu: aggiorna solo gli oggetti di una scena gia' aperta --------------------

        // Idempotente: da rilanciare con la scena aperta dopo aver ritoccato l'asset, senza
        // passare dalla copia del template (e senza rischiare di duplicare niente).
        [MenuItem("HapticResearch/Level 3/Aggiorna oggetti scena")]
        public static void UpdateObjectsFromMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError($"[Level3Builder] Apri {ScenePath} prima di lanciare il tool (scena attiva: {scene.path}).");
                return;
            }
            if (!UpdateSceneContent(scene, out string report))
            {
                Debug.LogError("[Level3Builder] " + report);
                return;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Level3Builder] " + report);
        }

        // --- Orchestrazione ---------------------------------------------------------------

        private static bool TryGenerateScene(bool forceRecreate, out string report)
        {
            var tableScene = EnsureTableSceneAsset(out _);
            if (!tableScene.Validate(out string assetError))
            {
                report = $"L'asset '{AssetPath}' non e' valido, niente e' stato generato:\n{assetError}";
                return false;
            }

            bool exists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath, AssetPathToGUIDOptions.OnlyExistingAssets));

            if (exists && !forceRecreate)
            {
                report = $"'{ScenePath}' esiste gia': NON la tocco (si perderebbe una scena rifinita a mano). " +
                          "Apri la scena e lancia 'HapticResearch/Level 3/Aggiorna oggetti scena' per rigenerare " +
                          "solo gli oggetti della colazione, oppure usa 'Rigenera scena da zero' per ricominciare " +
                          "da capo (cancella tutto).";
                return false;
            }

            if (exists && forceRecreate && !DeleteExistingScene(out report)) return false;

            if (!AssetDatabase.CopyAsset(TemplateScenePath, ScenePath))
            {
                report = $"Copia da '{TemplateScenePath}' a '{ScenePath}' fallita.";
                return false;
            }
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!UpdateSceneContent(scene, out report)) return false;

            EditorSceneManager.SaveScene(scene);
            return true;
        }

        // Chiude la scena se e' aperta (aprendone una vuota temporanea se era l'unica) e la
        // cancella dall'AssetDatabase. Non si puo' cancellare un asset con la sua scena
        // ancora caricata.
        private static bool DeleteExistingScene(out string report)
        {
            var open = SceneManager.GetSceneByPath(ScenePath);
            if (open.IsValid() && open.isLoaded)
            {
                if (SceneManager.sceneCount <= 1)
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                else
                    EditorSceneManager.CloseScene(open, true);
            }
            if (!AssetDatabase.DeleteAsset(ScenePath))
            {
                report = $"Non sono riuscito a cancellare '{ScenePath}'.";
                return false;
            }
            report = null;
            return true;
        }

        // Genera/aggiorna il root 'Colazione' coi sette oggetti e il Level3Manager, poi
        // richiama Level3SetupTool.Configure() per il resto (mani, HUD, voce, flusso di
        // fine livello, logger...). La scena passata deve essere gia' quella attiva.
        private static bool UpdateSceneContent(Scene scene, out string report)
        {
            var tableRoot = FindRoot(scene, TableRootName);
            if (tableRoot == null)
            {
                report = $"Nella scena non c'e' un root '{TableRootName}': impossibile agganciare la colazione. " +
                          "La scena non e' partita da ViveTrackerScene.unity?";
                return false;
            }

            var tableScene = AssetDatabase.LoadAssetAtPath<TableSceneAsset>(AssetPath);
            if (tableScene == null)
            {
                report = $"Manca l'asset '{AssetPath}': generalo prima con 'HapticResearch/Level 3/Genera dati colazione'.";
                return false;
            }
            if (!tableScene.Validate(out string assetError))
            {
                report = $"Asset '{tableScene.SceneId}' non valido: {assetError}";
                return false;
            }

            var colazioneRoot = ResetChildRoot(tableRoot.transform, ColazioneRootName);
            var created = new List<string>(Specs.Length);
            foreach (var spec in Specs)
            {
                BuildObject(colazioneRoot.transform, spec);
                created.Add(spec.Name);
            }

            var profile = AssetDatabase.LoadAssetAtPath<HapticProfile>(ProfilePath);
            if (profile == null) Debug.LogWarning($"[Level3Builder] Profilo aptico non trovato in '{ProfilePath}'.");

            var contactClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ContactClipPath);
            var discoveryClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DiscoveryClipPath);
            var suggestionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SuggestionClipPath);

            EnsureManager(scene, tableScene, profile, contactClip, discoveryClip, suggestionClip);
            EditorSceneManager.MarkSceneDirty(scene);

            SceneManager.SetActiveScene(scene);
            if (!Level3SetupTool.Configure())
            {
                report = "Level3SetupTool.Configure() ha segnalato un cablaggio fallito: controlla la Console.";
                return false;
            }

            bool objectsOk = Level3SetupTool.ValidateObjects(asWarnings: false);

            report = $"Oggetti generati sotto '{TableRootName}/{ColazioneRootName}': {string.Join(", ", created)}. " +
                      $"Asset dati: '{AssetPath}' ({tableScene.Objects.Count} oggetti, {tableScene.Suggestions.Count} suggerimenti). " +
                      $"Manager '{ManagerName}': TableSceneAsset collegata" +
                      (profile != null ? ", profilo collegato" : ", PROFILO MANCANTE") +
                      $". Validazione oggetti: {(objectsOk ? "OK" : "PROBLEMI, vedi Console")}.";
            return true;
        }

        // --- Asset dei dati ---------------------------------------------------------------

        private static TableSceneAsset EnsureTableSceneAsset(out bool created)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TableSceneAsset>(AssetPath);
            created = asset == null;
            if (asset == null)
            {
                EnsureFolder(AssetDir);
                asset = ScriptableObject.CreateInstance<TableSceneAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            WriteAssetData(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // I campi di TableSceneAsset sono privati senza setter: si scrivono con
        // SerializedObject, esattamente come MazeGeometryBuilder fa coi campi del SDK.
        private static void WriteAssetData(TableSceneAsset asset)
        {
            var so = new SerializedObject(asset);

            so.FindProperty("sceneId").stringValue = "colazione_v1";
            so.FindProperty("maxThermalObjects").intValue = 2;

            var objects = so.FindProperty("objects");
            objects.arraySize = ObjectEntries.Length;
            for (int i = 0; i < ObjectEntries.Length; i++)
            {
                var e = ObjectEntries[i];
                var elem = objects.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("id").stringValue = e.Id;
                elem.FindPropertyRelative("voiceKey").stringValue = e.VoiceKey;
                elem.FindPropertyRelative("label").stringValue = e.Label;
                elem.FindPropertyRelative("texture").enumValueIndex = (int)e.Texture;
                elem.FindPropertyRelative("textureVolume").floatValue = e.TextureVolume;
                elem.FindPropertyRelative("stiffness").floatValue = e.Stiffness;
                elem.FindPropertyRelative("role").enumValueIndex = (int)e.Role;
                elem.FindPropertyRelative("discoverable").boolValue = e.Discoverable;

                var tags = elem.FindPropertyRelative("tags");
                tags.arraySize = e.Tags.Length;
                for (int t = 0; t < e.Tags.Length; t++)
                    tags.GetArrayElementAtIndex(t).stringValue = e.Tags[t];
            }

            var suggestions = so.FindProperty("suggestions");
            suggestions.arraySize = SuggestionEntries.Length;
            for (int i = 0; i < SuggestionEntries.Length; i++)
            {
                var s = SuggestionEntries[i];
                var elem = suggestions.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("tag").stringValue = s.Tag;
                elem.FindPropertyRelative("voiceKey").stringValue = s.VoiceKey;
                elem.FindPropertyRelative("metVoiceKey").stringValue = s.MetVoiceKey;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Oggetti in scena --------------------------------------------------------------

        private readonly struct ObjectSpec
        {
            public readonly string Id, Name;
            public readonly PrimitiveType Primitive;
            public readonly Vector3 Scale;
            public readonly float CenterX, CenterZ, Top;

            public ObjectSpec(string id, string name, PrimitiveType primitive, Vector3 scale,
                              float centerX, float centerZ, float top)
            {
                Id = id; Name = name; Primitive = primitive; Scale = scale;
                CenterX = centerX; CenterZ = centerZ; Top = top;
            }
        }

        // Piano del tavolo a y=0.85 (TableTop: localPosition.y=0.8, scala y=0.1, cubo
        // unitario -> top = 0.8 + 0.05). Centro (x,z) e top da CLAUDE.md/spec del Level 3.
        // La TOVAGLIETTA ha il top 5 mm piu' basso di PROPOSITO (0.855 invece di 0.860):
        // fa da sfondo sotto piattino/tazza/pane/tovagliolo/bicchiere. Se stesse alla loro
        // stessa quota, un polpastrello sul bordo della tazza disterebbe zero anche dalla
        // tovaglietta sottostante (ResolveTouched in ExplorationManager prende l'oggetto
        // piu' vicino): il contatto resterebbe incollato li' per sempre e non si potrebbe
        // mai nominare nessun altro oggetto. Non "correggere" questo valore per farlo
        // combaciare con gli altri: e' la scoperta della revisione finale, non un refuso.
        private static readonly ObjectSpec[] Specs =
        {
            new ObjectSpec("tovaglietta", "Tovaglietta", PrimitiveType.Cube,
                           new Vector3(0.30f, 0.004f, 0.22f), 0.00f, 0.22f, 0.855f),
            new ObjectSpec("piattino", "Piattino", PrimitiveType.Cylinder,
                           new Vector3(0.13f, 0.004f, 0.13f), 0.00f, 0.22f, 0.860f),
            new ObjectSpec("tazza", "Tazza", PrimitiveType.Cylinder,
                           new Vector3(0.08f, 0.010f, 0.08f), -0.12f, 0.28f, 0.860f),
            new ObjectSpec("cucchiaino", "Cucchiaino", PrimitiveType.Cube,
                           new Vector3(0.012f, 0.005f, 0.09f), -0.10f, 0.16f, 0.860f),
            new ObjectSpec("pane", "Pane", PrimitiveType.Cube,
                           new Vector3(0.09f, 0.012f, 0.09f), 0.12f, 0.28f, 0.860f),
            new ObjectSpec("tovagliolo", "Tovagliolo", PrimitiveType.Cube,
                           new Vector3(0.10f, 0.006f, 0.10f), 0.13f, 0.15f, 0.860f),
            new ObjectSpec("bicchiere", "Bicchiere", PrimitiveType.Cylinder,
                           new Vector3(0.06f, 0.010f, 0.06f), -0.06f, 0.08f, 0.860f),
        };

        private static void BuildObject(Transform parent, ObjectSpec spec)
        {
            var go = GameObject.CreatePrimitive(spec.Primitive);
            go.name = spec.Name;
            go.transform.SetParent(parent, false);

            // Cubo unitario: la faccia superiore e' a scale.y/2 dal centro.
            // Cilindro unitario (altezza 2, raggio 0.5): la calotta e' a scale.y dal centro,
            // cioe' la scala Y del Cylinder E' gia' la mezza altezza (scala y=1 -> alto 2).
            float halfHeight = spec.Primitive == PrimitiveType.Cylinder ? spec.Scale.y : spec.Scale.y * 0.5f;
            float centerY = spec.Top - halfHeight;
            go.transform.localPosition = new Vector3(spec.CenterX, centerY, spec.CenterZ);
            go.transform.localScale = spec.Scale;

            // Collider: le primitive ne hanno gia' uno (Box per il cubo, Capsule per il
            // cilindro). Il dito lo attraversa: la sensazione la danno i pad aptici.
            var collider = go.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;

            // Rigidbody obbligatorio (checklist CLAUDE.md "Aggiungere oggetti touchable"):
            // floating, non cinetico verso la fisica, controllato a mano.
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.mass = 1f;

            var touchable = go.AddComponent<WeArtTouchableObject>();
            ConfigureTouchable(touchable);

            var binding = go.AddComponent<SceneObjectBinding>();
            var bindingSo = new SerializedObject(binding);
            bindingSo.FindProperty("id").stringValue = spec.Id;
            bindingSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(go, "Genera oggetto colazione");
        }

        // Solo i flag richiesti dalla checklist. NIENTE Texture/Stiffness/Temperature coi
        // valori dell'esperimento: quelli li scrive SceneObjectBinding.Bind() a runtime,
        // leggendoli dalla TableSceneAsset. Scriverli qui creerebbe una seconda fonte dello
        // stesso dato, destinata a disallinearsi dall'asset alla prima variante.
        private static void ConfigureTouchable(WeArtTouchableObject touchable)
        {
            var so = new SerializedObject(touchable);
            SetBool(so, "_disableDynamicForce", true); // senza, la forza arriva sbagliata sulle dita
            SetBool(so, "_graspable", false);           // il Level 3 e' esplorazione, non presa

            // La temperatura del Level 3 passa SOLO da ThermalObjectCue (un oggetto armato
            // per volta, minimo di tenuta, soppressione): se questo campo del SDK restasse
            // attivo scavalcherebbe quelle regole in silenzio. SceneObjectBinding.Bind() lo
            // rispegne comunque a ogni avvio, ma partire gia' spento evita un frame in cui
            // un oggetto termico avrebbe temperatura attiva senza che nessuno l'abbia armato.
            SetBool(so, "_temperature._active", false);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(SerializedObject so, string path, bool v)
        {
            var p = so.FindProperty(path);
            if (p != null) p.boolValue = v;
        }

        // --- Manager --------------------------------------------------------------------

        private static void EnsureManager(Scene scene, TableSceneAsset tableScene, HapticProfile profile,
                                          AudioClip contactClip, AudioClip discoveryClip, AudioClip suggestionClip)
        {
            var existing = FindInScene<ExplorationManager>(scene);
            ExplorationManager manager;
            if (existing == null)
            {
                var go = new GameObject(ManagerName);
                SceneManager.MoveGameObjectToScene(go, scene);
                manager = go.AddComponent<ExplorationManager>();
                Undo.RegisterCreatedObjectUndo(go, $"Crea {ManagerName}");
            }
            else
            {
                manager = existing;
            }

            var so = new SerializedObject(manager);
            so.FindProperty("scene").objectReferenceValue = tableScene;
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("contactClip").objectReferenceValue = contactClip;
            so.FindProperty("discoveryClip").objectReferenceValue = discoveryClip;
            so.FindProperty("suggestionToneClip").objectReferenceValue = suggestionClip;
            // 'bindings' resta vuota: l'ExplorationManager la riempie da sola in Awake con
            // tutti gli SceneObjectBinding della scena.
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Dati sperimentali (Objects/Suggestions) --------------------------------------

        private readonly struct ObjectData
        {
            public readonly string Id, VoiceKey, Label;
            public readonly TextureType Texture;
            public readonly float TextureVolume, Stiffness;
            public readonly ThermalRole Role;
            public readonly string[] Tags;
            public readonly bool Discoverable;

            public ObjectData(string id, string voiceKey, string label, TextureType texture, float volume,
                              float stiffness, ThermalRole role, string[] tags, bool discoverable)
            {
                Id = id; VoiceKey = voiceKey; Label = label; Texture = texture; TextureVolume = volume;
                Stiffness = stiffness; Role = role; Tags = tags; Discoverable = discoverable;
            }
        }

        private readonly struct SuggestionData
        {
            public readonly string Tag, VoiceKey, MetVoiceKey;
            public SuggestionData(string tag, string voiceKey, string metVoiceKey)
            { Tag = tag; VoiceKey = voiceKey; MetVoiceKey = metVoiceKey; }
        }

        private static readonly ObjectData[] ObjectEntries =
        {
            new ObjectData("tovaglietta", "level3_obj_tovaglietta", "Tovaglietta",
                           TextureType.TextileMedium, 100f, 0.15f, ThermalRole.Neutral,
                           new[] { "stoffa" }, discoverable: false),
            new ObjectData("tovagliolo", "level3_obj_tovagliolo", "Tovagliolo",
                           TextureType.Cotton, 100f, 0.08f, ThermalRole.Neutral,
                           new[] { "stoffa", "morbido" }, discoverable: true),
            new ObjectData("piattino", "level3_obj_piattino", "Piattino",
                           TextureType.Laminate, 100f, 0.70f, ThermalRole.Neutral,
                           new[] { "liscio" }, discoverable: true),
            new ObjectData("tazza", "level3_obj_tazza", "Tazza",
                           TextureType.Laminate, 100f, 0.80f, ThermalRole.Warm,
                           new[] { "caldo", "liscio" }, discoverable: true),
            new ObjectData("cucchiaino", "level3_obj_cucchiaino", "Cucchiaino",
                           TextureType.Aluminium, 100f, 0.90f, ThermalRole.Cool,
                           new[] { "freddo", "metallo" }, discoverable: true),
            new ObjectData("pane", "level3_obj_pane", "Fetta di pane",
                           TextureType.CrushedRock, 100f, 0.40f, ThermalRole.Neutral,
                           new[] { "ruvido" }, discoverable: true),
            new ObjectData("bicchiere", "level3_obj_bicchiere", "Bicchiere",
                           TextureType.PlasticFoil, 100f, 0.95f, ThermalRole.Neutral,
                           new[] { "liscio", "duro" }, discoverable: true),
        };

        private static readonly SuggestionData[] SuggestionEntries =
        {
            new SuggestionData("caldo", "level3_hint_caldo", "level3_hint_caldo_ok"),
            new SuggestionData("ruvido", "level3_hint_ruvido", "level3_hint_ruvido_ok"),
            new SuggestionData("metallo", "level3_hint_metallo", "level3_hint_metallo_ok"),
            new SuggestionData("morbido", "level3_hint_morbido", "level3_hint_morbido_ok"),
        };

        // --- Util scena e asset -------------------------------------------------------------

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash), leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == name) return r;
            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var r in scene.GetRootGameObjects())
            {
                var c = r.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        // Distrugge e ricrea il figlio diretto 'name' di 'parent': stesso trucco di
        // MazeGeometryBuilder.ResetChildRoot per restare idempotente senza inseguire quali
        // oggetti aggiornare uno per uno.
        private static GameObject ResetChildRoot(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, $"Crea {name}");
            return go;
        }
    }
}
