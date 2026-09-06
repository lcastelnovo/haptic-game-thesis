# Haptic Research – Unity VR Experiment

Progetto di ricerca UniBS (tesi) sull'uso di feedback aptico (Weart TouchDIVER Pro) per
supportare la percezione tattile in VR per utenti non vedenti e ipovedenti.

Si parte da un prototipo desktop (top-down su tavolo, mani simulate) e si sta migrando a
VR nativo con OpenXR + SteamVR e tracking opzionale via Vive Tracker.

## Stack

- Unity **6000.3.15f1**
- URP 17.3.0 — renderer separati PC/Mobile in `Assets/Settings/`
- New Input System 1.19.0 — action map in `Assets/InputSystem_Actions.inputactions`
- OpenXR 1.16.1 + SteamVR/OpenVR (pacchetto locale in `Assets/SteamVR/OpenVRUnityXRPackage/`)
- Vive Tracker 3.0 (opzionale, per oggetti reali)
- WEART Unity SDK **v2.3.0** in `Packages/WEART-UNITY-SDK/` (referenziato come pacchetto
  locale in `Packages/manifest.json`). La cartella
  `Assets/WEART-UNITY-SDK_v2.1.5_preview/` è residuo della versione vecchia, da rimuovere
- Target: PC VR (Quest via Link, Valve Index, Varjo) + fallback desktop standalone

## Build & Run

Niente CI, niente Makefile. Aprire da Unity Hub.

- Scene template: `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/ViveTrackerScene.unity`
  (la `old.unity` è vecchia, da non toccare salvo recupero asset)
- Scene di gioco (Scene List, in ordine): `MainMenu.unity` → `Level1_ShapeRecognition.unity`
  → `Labyrinth.unity` (Level 2). Ognuna si cabla con il suo tool editor (menu
  `HapticResearch/...`, anche headless con `-executeMethod`): `MenuSceneSetupTool`,
  `Level1SetupTool`, `LabyrinthSetupTool`. `SceneDumpTool` scrive un dump testuale di
  una scena (gerarchia, componenti, campi) per confrontarle senza aprire l'editor
- Build: File → Build Settings → PC Standalone
- Runtime aptico richiede WEART Middleware avviato + TouchDIVER Pro connesso. Senza
  middleware il gioco gira ma forza/temperatura/texture non escono
- Runtime VR richiede SteamVR avviato (per OpenVR) oppure Quest Link / OpenXR runtime
- `com.unity.test-framework` v1.6.0 è installato ma non ci sono ancora test

## Setup SteamVR senza HMD (prima volta su una macchina nuova)

I Vive Tracker funzionano senza visore, ma serve abilitare il **null driver** di SteamVR.
Una volta sola per macchina.

1. `C:\Program Files (x86)\Steam\steamapps\common\SteamVR\drivers\null\resources\settings\default.vrsettings`
   - `"driver_null".enable` → `true`
2. `C:\Program Files (x86)\Steam\steamapps\common\SteamVR\resources\settings\default.vrsettings`
   - `"steamvr".requireHmd` → `false`
   - `"steamvr".forcedDriver` → `"null"`
   - `"steamvr".forcedHmd` → `false`
   - `"steamvr".activateMultipleDrivers` → `true`
3. Riavviare SteamVR.

XR Plug-in Management nel progetto è già configurato — non toccare i loader senza
coordinarsi.

**Hardware:**
- Accendere i Vive Tracker, collegare i dongle USB al PC, posizionare le base station
  in alto con linea di vista libera ai tracker. Se la linea di vista si perde, il
  tracking si ferma finché non torna valida
- Pairing tracker: https://www.vive.com/us/support/tracker3/category_howto/pairing-vive-tracker.html
- Base station: https://www.vive.com/us/support/vive-pro/category_howto/installing-the-base-stations.html
- Le base station NON si connettono al PC: sparano solo i laser. I tracker comunicano
  via dongle e informano SteamVR della loro esistenza

## Principi non negoziabili

1. **Accessibilità prima di tutto.** L'esperienza deve funzionare *senza vista*. Ogni
   feature visiva ha un equivalente audio o aptico.
2. **Niente input dipendenti dalla vista.** No UI che richieda di "guardare" un bottone,
   no laser pointer come unico mezzo di selezione.
3. **Feedback audio sempre presente.** Ogni evento di stato (grab, errore, completamento
   task, cambio zona) ha voice-over o suono dedicato.
4. **Calibrazione per partecipante.** Intensità forza e range termico parametrizzabili
   via ScriptableObject o config runtime — niente valori hard-coded nei MonoBehaviour.
5. **Tutto sul piano del tavolo.** Gli oggetti sono **tile flat** o bump bassi a
   y≈0.86 (table top y=0.85). Niente oggetti alti, niente esplorazione verticale,
   niente interazioni "in aria". La mano scorre in XZ, la differenziazione tattile
   arriva dalla forma del top + texture/stiffness/temperatura.

## Architecture

### Interface-driven
Le interazioni core sono astratte in `Assets/Scripts/Interface/`:
- `IGrabbable` — grab/release oggetti fisici
- `ITouchable` — touch → spawn copie di prefab
- `IPressable` — azioni bottone
- `IScenari` — navigazione scenari (Next/Back/Reset)
- `IGridOrientable` — rotation snapping su griglia

### Hand system (`Assets/Scripts/Hands/`)
Simulazione bimanuale con articolazione per-dito:
- `HandInputManager` — switch mano (Space), cursor lock, fullscreen
- `HandPhysicsController` — movimento Rigidbody (mouse + tastiera), confinato al tavolo
- `HandCollisionController` — tracking parti mano ↔ oggetti, audio feedback
- `HandColliderPart` — collisione per segmento di dito (`touchDistance` = 0.03m)
- `HandCloseController` — selezione dita (0-5) e chiusura via scroll
- `HandGrabController` — left grab/press, right destroy, S/D ruota in mano
- `FingerController` / `ThumbController` — interpolazione giunti tra pose open/closed

Lo `WeArtHandController` ufficiale del SDK è **intenzionalmente disabilitato**: il
movimento mano lo fanno i nostri script. L'output aptico passa comunque per
`WeArtHapticObject` / `WeArtTouchableObject` su trigger collisions → `WeArtController` →
TCP:13031 → middleware → device.

### Haptics (`Assets/Scripts/Haptics/`)
Bridge custom sopra il SDK Weart per coesistenza con l'hand controller nostro:
- `WeArtHapticBridge` — instradamento eventi aptici tra mani simulate e device
- `HapticActuationEnabler` — abilita/disabilita attuazione per setup mono/bi-manuale
- `HapticFingerSetup` — mappatura dita Unity → attuatori TouchDIVER
- `WeArtTrackingSetup` — config tracking quando si usa SDK nativo
- `Assets/Scripts/Debug/HapticTriggerMonitor` — diagnostica trigger aptici

### Vive Tracker (`Assets/Scripts/ViveTracker/`)
Tracking esterno opzionale (tracker montato sul TouchDIVER per posizione mano reale):
- `ViveTrackerManager` — bind dei tracker SteamVR ai target Unity. Va riempito con i
  serial dei due tracker (`Left Tracker Serial` / `Right Tracker Serial`, formato
  `LHR-XXXXXXXX`) e i Transform target. Tracking Origin = `Tracking Universe Standing`
- `ViveTrackerCalibrationManager` — allineamento spazio SteamVR ↔ coordinate Unity.
  Premere **Space** in gioco con il palmo della mano di calibrazione (lato definito
  da `CalibrationHandSide`) completamente appoggiato al tavolo, dita perpendicolari
  al bordo
- `TrackerDebugger` — abilitare temporaneamente come component per loggare seriali e
  posizioni di tutti i tracker connessi, poi disabilitarlo (`Show Debug Objects` ON
  mostra anche i gameobject di calibrazione `RightCalibrationTarget` /
  `LeftCalibrationTarget` a runtime)

**Procedura per assegnare i tracker a una nuova installazione:**
1. Abilitare `TrackerDebugger` → console stampa `LHR-XXXXXXXX` di ciascun tracker e la
   sua posizione
2. Identificare dx/sx dal posizionamento fisico (vedi sotto)
3. Copiare i seriali nei campi di `ViveTrackerManager`
4. Disabilitare `TrackerDebugger`

**Orientamento fisico sui TouchDIVER:**
- Tracker **sinistro**: LED verde **lontano** dalla persona, in direzione delle dita
- Tracker **destro**: LED verde **verso** la persona, in direzione delle dita
- I fori del tracker si allineano con i pin del supporto stampato (lato L/R inciso
  sul supporto)

**Opzioni in `ViveTrackerCalibrationManager`:**
- `FreezeFingersClosure` — le dita ignorano i valori di chiusura/abduzione
- `AllowOnlyLateralRotation` — mano ruota solo sull'asse Y
- `FreezeHeight` — mano non si muove su/giù
- `FreezeAllRotation` — mano forzata nella direzione di calibrazione (richiede
  `FreezeHeight` + `AllowOnlyLateralRotation` attivi)

### Braille (`Assets/Scripts/Braille/`)
Apprendimento braille a 3 livelli:
- `BrailleDatabase` — encoding statico lettera/digit → pattern 6 punti
- `BrailleGrid` — spawna righe×colonne di `BrailleCell` (cell size 0.2×0.3m)
- `BrailleCell` / `BrailleDot` — rappresentazione 6 punti con stati raised/hidden
- `BrailleGameManager` — L1: char singolo · L2: parola random · L3: due parole
- `BrailleWordProvider` — carica parole da TextAsset

### Scenari (`Assets/Scripts/Scenarios/`)
- `TableScenarioManager` — controller top-level (menu + 3 scenari gameplay)
- `Scenario1SubManager` — placement oggetti su griglia
- `Scenario2Manager` — lettura braille con resize griglia per livello (1×2 → 1×5 → 2×5)

### Livelli (`Assets/Scripts/Levels/`)
- `LevelController` — base astratta di ogni livello (id, numero, titolo, stato,
  `StatusLine`, `ElapsedSeconds`, `StartLevel`, `RepeatAnnouncement`). Chi ha bisogno
  "del livello della scena" usa `LevelController.Find()`: comandi vocali, HUD, menu
  in-level, flusso di fine livello e demo girano identici in tutti i livelli
- `ShapeRecognitionManager` — Level 1: annuncia una forma, il partecipante la afferra e la
  tiene 5 s per confermare; 4 forme in ordine casuale
- `LabyrinthManager` — Level 2: prima trova l'ingresso (faro sonoro 2D che batte più
  veloce avvicinandosi), poi segue il corridoio con l'indice fino all'uscita passando
  per le tappe (`MazeZone`: ingresso, checkpoint, uscita, in ordine, solo XZ). Tocco
  muro = colpetto + conteggio + log (`Collider.ClosestPoint` sulla punta proiettata a
  y=0.9). Punte = `WeArtHapticObject` Index: demo ON → mani mouse sotto `HandManager`,
  demo OFF → mani `WEART/Hands` mosse dai tracker. Il labirinto di Luca (34 cubi +
  pad termici Caldino/Freddo) non ha un unico corridoio obbligato: le tappe sono
  provvisorie nel corridoio in basso e si spostano dall'Inspector
- `LevelFlowController` — a livello completato: suggerimento parlato, "avanti"/N (o
  "menu"/N nel labirinto) → scena successiva; Invio = nuovo partecipante
- `MainMenuManager` — benvenuto parlato in-level a livello fermo, R lo ripete
- `OperatorControls` — pannello storico, si spegne da solo quando c'è l'`OperatorHud`

### Grid & Objects
- `BuildGrid` (`Assets/Scripts/Grid/`) — snap grid 13×8, cell size 0.075m, niente overlap
- `GrabbableObject` / `TouchableObject` (`Assets/Scripts/Objects/`) — physics grab con
  snapping opzionale, factory touch-to-spawn
- `TriangleGridOrientation` — rotation snapping a 90° per prismi

### Audio & Camera
- `ObjectAudioFeedback` (`Assets/Scripts/Audio/`) — audio spaziale 3D differenziato per
  tipo (table / pressable / grabbable / touchable / default)
- `NarrationManager` (`Assets/Scripts/Audio/`) — battute vocali pre-generate caricate per
  chiave da `Resources/Voice/<chiave>.mp3`; `CurrentKey` = battuta in riproduzione
- `VoiceLines` (`Assets/Scripts/Audio/`) — testi delle battute da
  `Assets/Resources/Voice/voice_lines.json`. È l'**unica fonte** dei testi: la leggono
  sia gli script `Tools/generate_voice*.py` (per generare gli mp3) sia i sottotitoli.
  Per aggiungere una battuta: nuova chiave nel JSON, poi
  `python3 Tools/generate_voice_macos.py --only <chiave>` (voce macOS Alice) o
  `generate_voice.py` (ElevenLabs). Chiavi Level 2: `level2_*`, `menu_back`
- Suoni sintetici del labirinto in `Assets/Audio/Level2/` (generati con ffmpeg:
  colpetto muro, beep del faro, campanella checkpoint)
- `VoiceSubtitles` (`Assets/Scripts/UI/`) — sottotitoli per l'operatore: riga SENTO
  (frase riconosciuta dal microfono, confidenza, esito) e riga DICO (testo della battuta
  in corso). Si auto-installa in ogni scena, toggle **F2**. Posizione per scena
  (`Placement`): in basso al centro nei livelli, in alto a sinistra nel menu (lo imposta
  `MainMenuSceneController`, in basso ci sono i crediti). I controller vocali segnalano
  ogni frase con `VoiceSubtitles.ReportHeard(...)` DOPO che l'azione ha deciso
- `TopCameraFitTable` (`Assets/Scripts/Camera/`) — ortho top-down fittata al tavolo
  (legacy desktop; in VR non viene usata)

### HUD operatore (`Assets/Scripts/UI/`)
- `OperatorHud` — interfaccia unica dell'operatore vedente (dal mockup grafico), uguale in
  tutti i livelli, auto-installata nelle scene con un `LevelController` (non nel menu):
  sidebar a sinistra (logo, livello e titolo, stato, righe hardware in sola lettura dal
  SDK: middleware, TouchDIVER, calibrazione, Vive Tracker; bottoni Avvia/Ripeti/livello
  successivo; footer tasti), pill in alto a destra (mani demo ON/OFF cliccabile,
  partecipante, timer). Le camere su Display 1 vengono ristrette a destra della
  sidebar. **F3** nasconde l'HUD. I pannelli storici (OperatorControls, toggle demo,
  indicatore voce, watermark) si spengono da soli quando l'HUD è attivo
- `HudTheme` — palette e font di sistema (Georgia/Consolas su Windows) condivisi
- La vista 3D resta com'è nel codice: il mockup vale solo per sidebar, pill e barra

## Hardware Weart

- TouchDIVER Pro: 6 punti di attuazione (Thumb, Index, Middle, Annular, Pinky, Palm) —
  forza, vibrazione, temperatura
- Il feedback termico richiede **~2-3s** per raggiungere target → non triggerare cambi
  termici rapidi consecutivi, non avranno effetto e confondono il partecipante
- Configurazioni: 1 o 2 TouchDIVER (dx, sx, o entrambe)
- Verificare stato middleware prima dell'inizio sessione
- SDK docs: https://weart.it/docs/sdkunity/2.2.0/

**`WeArtController` prefab in scena:**
- `Device Generation` = `TD_Pro`
- `Start Calibration Automatically` ON — calibrazione TouchDIVER all'avvio della scena
- `Allow Gestures`, `Use External Grasp System`, `Start Raw Data Automatically` OFF

## Aggiungere oggetti touchable

Regole per un nuovo oggetto da rendere tattile (vedi `Cube`, `Cylinder`, `Prism`, `Star`
nella scena come riferimento):

**Mesh Collider** (se non è una primitiva tipo Cube/Sphere)
- `Convex` ON — necessario per il physics system
- `Is Trigger` ON — l'oggetto si lascia attraversare (la sensazione la generano i pad
  aptici, non c'è blocco fisico)

**Rigidbody** (obbligatorio: il sistema haptic reagisce solo a oggetti con Rigidbody)
- `Use Gravity` OFF, `Is Kinematic` ON → oggetto floating, controllato a mano
- `Mass = 1`, `Angular Drag = 0.05` (default)

**`WeArtTouchableObject`**
- Spuntare `Stiffness` / `Texture` / `Temperature` secondo la sensazione desiderata
- `Disable Dynamic Force` ON — senza questo la forza viene applicata in modo sbagliato
  sulle dita
- `Graspable` ON solo se l'oggetto deve essere afferrabile

**Oggetti complessi (non convex)**
Split in più mesh convex separate (es. una stella = 1 cubo + 4 prismi). Ogni parte ha
il suo Rigidbody + WeArtTouchableObject. Il parent è un GameObject vuoto che fa solo
da container — niente collider/rigidbody sul parent.

## Controlli desktop (fallback)

| Input | Azione |
|---|---|
| Mouse | Posizione mano |
| Q / E | Mano su / giù |
| Frecce, Z / X | Rotazione mano |
| Space | Switch sx / dx |
| 0-5 + scroll | Seleziona dita + chiudi / apri |
| Left click | Grab / Press |
| Right click | Distruggi oggetto in mano |
| S / D | Ruota oggetto in mano |
| F1 | Pannello diagnostico presa (guanti, bridge, grabPoint) |
| F2 | Mostra / nasconde sottotitoli voce (SENTO / DICO) |
| F3 | Mostra / nasconde HUD operatore |
| Invio / R | Avvia (o riavvia) livello / ripeti annuncio |
| N | Livello successivo (Level 1) o torna al menu (Level 2), solo a livello completato |
| M | Muta / riattiva il microfono |

In VR la mappatura passa al controller / tracking nativo — la sorgente attiva è gestita
da `HandInputManager`.

## Convenzioni codice

- Commenti **in italiano** (convenzione storica, manteniamola anche sui nuovi script)
- `SerializeField` privato invece di public field
- Niente `GetComponent` / `Find` in `Update` — cachare in `Awake`
- Configurazione via Inspector (`[SerializeField]`), non costanti hard-coded
- Physics nei `FixedUpdate` con Rigidbody
- API Unity 6: `FindObjectsByType<T>(FindObjectsSortMode.None)`, non `FindObjectOfType`
- Logging dati sperimentali via un `SessionLogger` centralizzato (da introdurre in
  `Assets/Scripts/Experiment/` quando arriviamo al raccolto dati), non `Debug.Log` sparsi
- Namespace `HapticResearch.<Sottosistema>` sui nuovi script — i vecchi vanno
  retro-fittati a poco a poco, non in un colpo solo

## Logging dati sperimentali

- Formato: CSV in `<persistentDataPath>/SessionLogs/`
- Campi minimi: timestamp ISO 8601, `participantId` (anonimo), level, condition,
  eventType, eventData (JSON)
- **MAI** dati personali identificativi — solo ID anonimi assegnati prima della sessione

## Workflow scene

`SampleScene.unity` e `ViveTrackerScene.unity` sono **template**, non si toccano. Per
sviluppare un nuovo livello:

1. Duplicare `ViveTrackerScene.unity` (è la base con tutto già configurato: WEART,
   Vive Tracker, mani, tavolo)
2. Rinominare in `LevelN_<NomeBreve>.unity` (es. `Level1_ShapeRecognition.unity`)
3. Lavorare nella copia, non nell'originale

Stessa cosa per i prefab: se serve modificarne uno esistente per un livello, duplicarlo
prima.

## Cosa NON fare

- **MAI** cambiare il path del pacchetto WEART in `Packages/manifest.json` senza
  coordinarsi: è specifico per macchina e la cartella SDK è in `.gitignore`
- **MAI** modificare guid/fileID nei `.unity` / `.prefab` / `.asset` — generati da Unity
  e legati all'installazione locale; il collegamento Inspector lo fa la persona che apre
  la scena
- **MAI** toccare file dentro `Packages/WEART-UNITY-SDK/` o `Assets/SteamVR/` — è codice
  di terze parti, si aggiorna sostituendo il pacchetto
- Non toccare `Library/`, `Temp/`, `Logs/`, `UserSettings/` (sono in `.gitignore`)
- Non aggiungere dipendenze pesanti senza chiedere prima
- Non usare API Unity deprecate (vedi convenzioni sopra)

## Branch policy

- `main` = stabile, solo merge da feature branch dopo test su hardware reale
- `feature/*` = sviluppo attivo, un branch per feature
  (es. `feature/haptic-feedback`, `feature/port-vive-tracker`)
- PR + review prima del merge in `main`

## Team

UniBS — Prof.ssa Anna Richelli (supervisione), Lorenzo Ghiro (ricercatore),
Luca Castelnovo (tesista), Simone Saleri (stagista).
