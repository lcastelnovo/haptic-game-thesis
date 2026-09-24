# Haptic Research – Unity VR Experiment

Progetto di ricerca UniBS (tesi) sull'uso di feedback aptico (Weart TouchDIVER Pro) per
supportare la percezione tattile in VR per utenti non vedenti e ipovedenti.

Si parte da un prototipo desktop (top-down su tavolo, mani simulate) e si sta migrando a
VR nativo con OpenXR + SteamVR e tracking opzionale via Vive Tracker.

## Stack

- Unity **6000.3.15f1**
- URP 17.3.0: renderer separati PC/Mobile in `Assets/Settings/`
- New Input System 1.19.0: action map in `Assets/InputSystem_Actions.inputactions`
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
  → `Labyrinth.unity` (Level 2) → `Level3_Breakfast.unity` (Level 3). Ognuna si cabla con il suo tool editor (menu
  `HapticResearch/...`, anche headless con `-executeMethod`): `MenuSceneSetupTool`,
  `Level1SetupTool`, `LabyrinthSetupTool`, `Level3SetupTool`. Alternativa al Level 3: `Level3_Memory.unity` (memory tattile), scelta dall'operatore nel menu (tasto 4). Tool: `MemorySceneBuilder` / `MemorySetupTool`; il cablaggio comune a Level 3 e memory sta in `LevelSceneWiring`. `SceneDumpTool` scrive un dump testuale di
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

XR Plug-in Management nel progetto è già configurato, non toccare i loader senza
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
   via ScriptableObject o config runtime, niente valori hard-coded nei MonoBehaviour.
5. **Tutto sul piano del tavolo.** Gli oggetti sono **tile flat** o bump bassi a
   y≈0.86 (table top y=0.85). Niente oggetti alti, niente esplorazione verticale,
   niente interazioni "in aria". La mano scorre in XZ, la differenziazione tattile
   arriva dalla forma del top + texture/stiffness/temperatura.

## Architecture

### Interface-driven
Le interazioni core sono astratte in `Assets/Scripts/Interface/`:
- `IGrabbable`: grab/release oggetti fisici
- `ITouchable`: touch → spawn copie di prefab
- `IPressable`: azioni bottone
- `IScenari`: navigazione scenari (Next/Back/Reset)
- `IGridOrientable`: rotation snapping su griglia

### Hand system (`Assets/Scripts/Hands/`)
Simulazione bimanuale con articolazione per-dito:
- `HandInputManager`: switch mano (Space), cursor lock, fullscreen
- `HandPhysicsController`: movimento Rigidbody (mouse + tastiera), confinato al tavolo
- `HandCollisionController`: tracking parti mano ↔ oggetti, audio feedback
- `HandColliderPart`: collisione per segmento di dito (`touchDistance` = 0.03m)
- `HandCloseController`: selezione dita (0-5) e chiusura via scroll
- `HandGrabController`: left grab/press, right destroy, S/D ruota in mano
- `FingerController` / `ThumbController`: interpolazione giunti tra pose open/closed

Lo `WeArtHandController` ufficiale del SDK è **intenzionalmente disabilitato**: il
movimento mano lo fanno i nostri script. L'output aptico passa comunque per
`WeArtHapticObject` / `WeArtTouchableObject` su trigger collisions → `WeArtController` →
TCP:13031 → middleware → device.

### Haptics (`Assets/Scripts/Haptics/`)
Bridge custom sopra il SDK Weart per coesistenza con l'hand controller nostro:
- `WeArtHapticBridge`: instradamento eventi aptici tra mani simulate e device
- `HapticActuationEnabler`: abilita/disabilita attuazione per setup mono/bi-manuale
- `HapticFingerSetup`: mappatura dita Unity → attuatori TouchDIVER
- `WeArtTrackingSetup`: config tracking quando si usa SDK nativo
- `Assets/Scripts/Debug/HapticTriggerMonitor`: diagnostica trigger aptici
- `WeArtSceneTransition`: chiude il `WeArtController` della scena vecchia prima che la nuova
  venga attivata (lo chiama `SceneFader`). Senza, al passaggio livello → livello il controller
  nuovo si auto-distrugge (trova ancora il vecchio in `_instance`), socket e thread del vecchio
  restano aperti e il guanto sembra "bloccato". Ferma anche la sessione del middleware: la
  scena nuova la riavvia da sé. **Ogni cambio scena deve passare da `SceneFader`**
- `GloveCalibration`: calibrazione unica = dita del TouchDIVER + allineamento Vive Tracker,
  stessa posa (palmo sul tavolo, dita distese). La usano **Space** e "Avvia livello"
  (`LevelController.RequestStart()`: voce "appoggia la mano", calibrazione, voce "fatto", poi
  `StartLevel`). Esito nel log (`calibration_start` / `calibration_end`); senza middleware si
  salta il guanto e il livello parte lo stesso

### Vive Tracker (`Assets/Scripts/ViveTracker/`)
Tracking esterno opzionale (tracker montato sul TouchDIVER per posizione mano reale):
- `ViveTrackerManager`: bind dei tracker SteamVR ai target Unity. Va riempito con i
  serial dei due tracker (`Left Tracker Serial` / `Right Tracker Serial`, formato
  `LHR-XXXXXXXX`) e i Transform target. Tracking Origin = `Tracking Universe Standing`
- `ViveTrackerCalibrationManager`: allineamento spazio SteamVR ↔ coordinate Unity.
  Premere **Space** in gioco con il palmo della mano di calibrazione (lato definito
  da `CalibrationHandSide`) completamente appoggiato al tavolo, dita perpendicolari
  al bordo
- `TrackerDebugger`: abilitare temporaneamente come component per loggare seriali e
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
- `FreezeFingersClosure`: le dita ignorano i valori di chiusura/abduzione
- `AllowOnlyLateralRotation`: mano ruota solo sull'asse Y
- `FreezeHeight`: mano non si muove su/giù
- `FreezeAllRotation`: mano forzata nella direzione di calibrazione (richiede
  `FreezeHeight` + `AllowOnlyLateralRotation` attivi)

### Braille (`Assets/Scripts/Braille/`)
Apprendimento braille a 3 livelli:
- `BrailleDatabase`: encoding statico lettera/digit → pattern 6 punti
- `BrailleGrid`: spawna righe×colonne di `BrailleCell` (cell size 0.2×0.3m)
- `BrailleCell` / `BrailleDot`: rappresentazione 6 punti con stati raised/hidden
- `BrailleGameManager` - L1 char singolo, L2 parola random, L3 due parole
- `BrailleWordProvider`: carica parole da TextAsset

### Scenari (`Assets/Scripts/Scenarios/`)
- `TableScenarioManager`: controller top-level (menu + 3 scenari gameplay)
- `Scenario1SubManager`: placement oggetti su griglia
- `Scenario2Manager`: lettura braille con resize griglia per livello (1×2 → 1×5 → 2×5)

### Livelli (`Assets/Scripts/Levels/`)
- `LevelController`: base astratta di ogni livello (id, numero, titolo, stato,
  `StatusLine`, `ElapsedSeconds`, `StartLevel`, `RepeatAnnouncement`). Chi ha bisogno
  "del livello della scena" usa `LevelController.Find()`: comandi vocali, HUD, menu
  in-level, flusso di fine livello e demo girano identici in tutti i livelli
- `ShapeRecognitionManager` - Level 1: annuncia una forma, il partecipante la afferra e la
  tiene 5 s per confermare; 4 forme in ordine casuale
- `LabyrinthManager` - Level 2: vedi la sezione **Labirinto** più sotto
- `ExplorationManager` - Level 3: vedi la sezione **Colazione** più sotto
- `FingerProbeSource` / `WallContactTracker` / `ProximityBeacon`: pezzi estratti dal
  labirinto ma riusabili da qualunque livello. Punte = `WeArtHapticObject` con
  `ActuationPointFlags.Index`, filtrate su `HandDemoModeController.DemoActive`: demo ON →
  mani mouse sotto `HandManager`, demo OFF → mani `WEART/Hands` mosse dai tracker.
  `WallContactTracker` conta gli **episodi** di contatto (strisciare lungo una parete è
  un episodio solo), non i frame
- `LevelFlowController` gestisce la fine del livello: suggerimento parlato, "avanti"/N (o
  "menu"/N nel labirinto) → scena successiva; Invio = nuovo partecipante
- `MainMenuManager`: benvenuto parlato in-level a livello fermo, R lo ripete
- `OperatorControls`: pannello storico, si spegne da solo quando c'è l'`OperatorHud`

### Labirinto (`Assets/Scripts/Labyrinth/`) - Level 2

Il livello è un **albero di decisioni con bivi termici**, non un labirinto a guida termica
continua. Il motivo è fisico e va ricordato prima di riproporre l'idea del gradiente:
l'attuatore Peltier impiega **2-3 s** a raggiungere il set-point e l'SDK considera 15 cm/s
la velocità "veloce" del dito (`WeArtCommon.MaxSpeedForMaxTextVelocity`), quindi quando il
calore arriva il dito è già uno o due corridoi più in là. Il canale termico non è
campionabile abbastanza in fretta per fare da gradiente; una **scelta discreta a sosta** sì.

Flusso: trova l'ingresso (il perimetro è chiuso da un solo varco, lo si trova seguendo il
bordo col dito; il faro sonoro aiuta ma non è l'unico appiglio) → segue il corridoio → a
ogni bivio appoggia il dito all'imbocco di un ramo e lo tiene fermo ~2.5 s → caldo = strada
giusta, freddo = sbagliata. **Ne basta una**: l'altro ramo è l'opposto per costruzione.

- `MazeLayoutAsset` — ScriptableObject in `Assets/Settings/Labyrinth/`: griglia, misure,
  percorso, bivi, varchi di ingresso/uscita, specularità per i mancini. È **il dato
  dell'esperimento**, non un dettaglio: per una variante si duplica l'asset, così resta
  scritto quale labirinto ha giocato chi. `Validate()` rifiuta anelli, vicoli ciechi
  condivisi e rami scollegati **prima** di generare: in Scene view non si noterebbero
- `MazeMap` — classe pura (niente MonoBehaviour): griglia fine dove gli indici pari sono
  linee di muro e i dispari corsie di corridoio. Espone `Locate` (punto → cella / muro /
  varco / fuori, in O(1)), `WallRuns()` (blocchi di muro già fusi) e il percorso. La
  logica di gioco interroga **questa**, non i collider: i collider servono solo a far
  sentire muri e piastrelle ai guanti
- `MazeRuntime` — il labirinto di questa scena: tiene il layout, costruisce la mappa e
  disegna i gizmo. **Il sistema di riferimento è il suo stesso `Transform`**, non quello
  del tavolo: la geometria è generata come sua figlia, così logica e cubi in scena non
  possono divergere. Va appeso al root `Table` (non a `TableTop`, che è scalato)

**Da che parte è seduto il partecipante.** Il layout è scritto in *coordinate del
partecipante*: lui a −z che guarda verso +z, la sua destra a +x. In `Labyrinth.unity` è
dalla parte opposta — `FrontalCamera` sta a z = +0.8 e guarda verso −z, e
`RightCalibrationTarget` è a z = +0.291 — quindi **il bordo vicino a lui è z = +0.4** e
`participantYaw` del layout vale 180°. Il builder applica quella rotazione al root `Maze`.
Sbagliare segno qui non dà nessun errore: mette solo il labirinto nella metà del tavolo
che da seduto non si raggiunge. `Tools/MazeMapTest` lo controlla apposta (ingombro nella
metà vicina, tutto entro 40 cm di allungo, ingresso dal lato della mano dominante)
- `IJunctionCue` + `JunctionCueBase` + `ThermalJunctionCue` / `AudioJunctionCue` — la
  condizione sperimentale. La **sosta sta nella base comune**, non nelle sottoclassi: il
  timing dev'essere identico fra le due condizioni, altrimenti il confronto misurerebbe
  la durata dell'interazione invece del canale sensoriale
- `HapticProfile` — ScriptableObject di taratura del partecipante (caldo/freddo/neutro,
  durata sosta, stiffness e texture di muri e piastrelle, mano attuata)
- `ThermalCalibrationStep` — scala adattiva 2-down/1-up che misura la soglia termica del
  partecipante e ne ricava i valori di gioco (soglia × margine). **K** avvia, si risponde
  a voce ("caldo"/"freddo") o con **C**/**F** quando il microfono non c'è

**Geometria: si rigenera, non si sposta a mano.** `HapticResearch/Level 2/Genera geometria
labirinto` (`MazeGeometryBuilder`) costruisce muri, piastrelle e tappe dal layout. Il
vecchio labirinto (34 cubi + pad `Caldino`/`Freddo`) finisce sotto il root disattivato
`Labirinto_vecchio`, e `Assets/Scenes/Labyrinth_old.unity` è la copia intera della scena
com'era.

**Perché la temperatura NON passa dai `WeArtTouchableObject`:** va armata un bivio per
volta, va riportata a neutro appena il dito lascia la piastrella (così il raffreddamento
comincia durante lo spostamento) e la via diretta
`WeArtController.Instance.Client.SendMessage(new SetTemperatureMessage {...})` non passa
dal gate `isActuating`, che nel setup desktop non si apre. Senza middleware non esplode:
il livello resta giocabile, solo senza canale termico.

**Igiene sperimentale:** ramo giusto e ramo sbagliato sono **visivamente identici**. Se
l'operatore vedesse la risposta sul tavolo rischierebbe di segnalarla senza volerlo. La
distinzione vive solo nei gizmo dell'editor.

**Condizione A/B** su `SessionLogger.condition`: `audio` usa il cue sonoro, qualunque
altro valore quello termico (`forcedCondition` sul manager forza la scelta nei test).

**Prova dell'aritmetica:** `cd Tools/MazeMapTest && dotnet run` compila i `MazeMap.cs` e
`MazeLayoutValidator.cs` veri ed esegue i controlli fuori da Unity: celle che si
rilocalizzano, blocchi di muro non sovrapposti, area che torna, perimetro chiuso,
orientamento rispetto al partecipante, e sei layout malformati che devono essere
rifiutati. Se tocchi la griglia, rilancia il test. Gira solo su codice **gestito**, niente
`Quaternion.Euler` o altre API che chiamano il runtime nativo di Unity.

### Colazione (`Assets/Scripts/Exploration/`) - Level 3

Sandbox di esplorazione libera senza fallimento. Il partecipante scopre gli oggetti sul
tavolo, tocca le loro superfici, sente caldo o freddo, e se ha voglia smette a voce
("ho finito") o l'operatore preme **Fine**.

Approccio ibrido: la **geometria sta nella scena** (è arte: una fetta di pane non si genera
da codice), i **parametri tattili e i testi stanno in `TableSceneAsset`** (`Assets/Settings/Exploration/`)
che è **il dato dell'esperimento** e si duplica per una variante. Così resta scritto quale
asset ha giocato chi.

**La temperatura funziona diversamente dal labirinto** perché qui gli oggetti sono sette e
vicini: il dito rimbalza tra loro più in fretta di quanto l'attuatore Peltier sappia seguire.
Nel labirinto la lettura è una scelta a sosta; qui è una proprietà dell'oggetto, e il canale
si comporta così:

- un solo oggetto armato per volta;
- si arma **all'ingresso** sull'oggetto, non dopo una sosta: l'attuatore così scalda mentre
  il dito ne segue il contorno;
- una volta armato si tiene per almeno **3 s** (`minHoldSeconds`), anche se il dito se n'è
  andato subito: sotto quella soglia il Peltier non ha ancora raggiunto il valore;
- all'uscita c'è una grazia di **0,4 s** (`releaseGraceSeconds`) prima di lasciar andare:
  un tremolio della mano non deve spegnere niente;
- finché il minimo non è scaduto, un secondo oggetto termico **non commuta**: tace, e lo scrive
  nel log come `thermal_suppressed`. Meglio nessuna informazione che un tepore ambiguo, che il
  partecipante leggerebbe come un dato quando è solo un attuatore a metà strada. Quella riga di
  log serve in analisi: senza, resterebbe un partecipante che sembra non aver percepito il freddo,
  e non si saprebbe mai che il freddo non gli è stato mandato.

Per questo motivo, "questa cosa è calda" o "fredda" si **dice solo se la temperatura è
davvero stata erogata in precedenza al dito** — non per ipotesi, non per il nome
dell'oggetto. Senza middleware la sensazione termica non arriva, ma il livello resta
giocabile: il partecipante scopre comunque forma, texture e stiffness.

I **suoni di contatto e scoperta sono riusati dal Level 2 di proposito**: il partecipante
non impara due vocabolari sonori per le stesse cose.

Sugli oggetti della colazione il campo `Temperature` del `WeArtTouchableObject` va lasciato
**spento**, contro la checklist generale più sotto: la temperatura passa solo dal messaggio
diretto di `ThermalObjectCue`, e il campo del SDK scavalcherebbe in silenzio isteresi,
minimo di tenuta e soppressione. `HapticResearch/Level 3/Valida oggetti` lo controlla.

La **taratura termica del partecipante** (soglia di percezione, range warm/cool) si
eredita da `HapticProfile` del Level 2 invece di rifarla: il livello precedente ha già
calibrato il giocatore. Lo stesso profilo decide **quale mano** si attua, e il
rilevamento del contatto guarda solo quella: con entrambe, una mano appoggiata ferma sul
tavolo vincerebbe ogni confronto di distanza e la mano che esplora diventerebbe invisibile.

**Comandi vocali del partecipante** (l'operatore deve conoscerli per condurre la sessione):
«cos'è questo» dice cosa c'è sotto il dito, «cosa manca» dice *quanti* oggetti restano (mai
quali), «ho finito» chiude il livello — lo stesso del tasto **Fine** dell'operatore.

L'isteresi termica, il comportamento dello scheduler delle richieste e i controlli sul dato
di scena si verificano con `cd Tools/ExplorationTest && dotnet run`.

### Memory tattile (`Assets/Scripts/Memory/`) - Level 3 alternativo

Alternativa alla colazione, pronta nel caso il guanto non regga il realismo di "questa e'
una tazza". Le sensazioni sono un **codice astratto**, come il braille: servono solo che le
firme si distinguano e che il partecipante se le ricordi. Spec:
`docs/superpowers/specs/2026-09-23-level3-memory-tattile-design.md`.

Griglia 4x3 di tessere piatte da 8 cm; la firma e' texture + stiffness. Si gira una tessera
tenendoci il **dito fermo ~1 s** (tono che sale, clic); l'operatore puo' girarla con **G**.
Fase 1 (riscaldamento): 3 coppie **scoperte**, misura la discriminazione pura. Fase 2: 6
coppie **coperte**, misura la memoria. I `mismatch` nel log portano `partnerSeenBefore` e
`secondSeenBefore` per separare errore di memoria ed errore di percezione.

- `MemoryLayoutAsset` in `Assets/Settings/Memory/`: **il dato dell'esperimento**. La
  variante `MemoryLayout_Termico_v1` aggiunge il freddo ai due "metallo" e allunga la sosta a
  3 s; la temperatura passa da `ThermalObjectCue` come nella colazione
- `MemoryGridMap` / `DwellDetector` / `MemoryBoard` / `MemoryLayoutValidator`: classi pure,
  provate con `cd Tools/MemoryTest && dotnet run`. Se tocchi la griglia, rilancia il test
- La griglia **si rigenera**: `HapticResearch/Level 3 Memory/Rigenera griglia`
- Le tessere sono **visivamente identiche** in ogni stato tranne "fuori gioco": l'operatore
  non deve vedere dov'e' la coppia

### Grid & Objects
- `BuildGrid` (`Assets/Scripts/Grid/`): snap grid 13×8, cell size 0.075m, niente overlap
- `GrabbableObject` / `TouchableObject` (`Assets/Scripts/Objects/`): physics grab con
  snapping opzionale, factory touch-to-spawn
- `TriangleGridOrientation`: rotation snapping a 90° per prismi

### Audio & Camera
- `ObjectAudioFeedback` (`Assets/Scripts/Audio/`): audio spaziale 3D differenziato per
  tipo (table / pressable / grabbable / touchable / default)
- `NarrationManager` (`Assets/Scripts/Audio/`): battute vocali pre-generate caricate per
  chiave da `Resources/Voice/<chiave>.mp3`; `CurrentKey` = battuta in riproduzione
- `VoiceLines` (`Assets/Scripts/Audio/`): testi delle battute da
  `Assets/Resources/Voice/voice_lines.json`. È l'**unica fonte** dei testi: la leggono
  sia gli script `Tools/generate_voice*.py` (per generare gli mp3) sia i sottotitoli.
  Per aggiungere una battuta: nuova chiave nel JSON, poi
  `python3 Tools/generate_voice_macos.py --only <chiave>` (voce macOS Alice) o
  `generate_voice.py` (ElevenLabs). Chiavi Level 2: `level2_*`, `menu_back`
- Suoni non verbali del labirinto in `Assets/Audio/Level2/`, generati da
  `python3 Tools/generate_sfx.py` (`--force` rigenera, `--only <nomi>` fa i singoli):
  colpetto muro, beep del faro, campanella, tono della sosta, colpetto di lettura
  pronta, esiti su/giù della condizione audio, vicolo cieco, loop fuori percorso.
  **Non farli a mano**: la ricetta ffmpeg sta nello script. I clip che ciclano davvero
  sono WAV, non mp3: l'mp3 ha il padding dell'encoder e farebbe un clic a ogni giro
- `VoiceSubtitles` (`Assets/Scripts/UI/`): sottotitoli per l'operatore: riga GIOCATORE
  (frase riconosciuta dal microfono, confidenza, esito) e riga NARRATORE (testo della
  battuta in corso). Le due etichette si cambiano dall'Inspector. Si auto-installa in ogni scena, toggle **F2**. Posizione per scena
  (`Placement`): in basso al centro nei livelli, in alto a sinistra nel menu (lo imposta
  `MainMenuSceneController`, in basso ci sono i crediti). I controller vocali segnalano
  ogni frase con `VoiceSubtitles.ReportHeard(...)` DOPO che l'azione ha deciso
- `TopCameraFitTable` (`Assets/Scripts/Camera/`): ortho top-down fittata al tavolo
  (legacy desktop; in VR non viene usata)

### HUD operatore (`Assets/Scripts/UI/`)
- `OperatorHud`: interfaccia unica dell'operatore vedente (dal mockup grafico), uguale in
  tutti i livelli, auto-installata nelle scene con un `LevelController` (non nel menu):
  sidebar a sinistra (logo, livello e titolo, stato, righe hardware in sola lettura dal
  SDK: middleware, TouchDIVER, calibrazione, Vive Tracker; bottoni Avvia/Ripeti/livello
  successivo; footer tasti), pill in alto a destra (mani demo ON/OFF cliccabile,
  partecipante, timer). Le camere su Display 1 vengono ristrette a destra della
  sidebar. **F3** nasconde l'HUD. I pannelli storici (OperatorControls, toggle demo,
  indicatore voce, watermark) si spengono da soli quando l'HUD è attivo
- `HudTheme`: palette e font di sistema (Georgia/Consolas su Windows) condivisi
- La vista 3D resta com'è nel codice: il mockup vale solo per sidebar, pill e barra

## Hardware Weart

- TouchDIVER Pro: 6 punti di attuazione (Thumb, Index, Middle, Annular, Pinky, Palm) con
  forza, vibrazione e temperatura
- Il feedback termico richiede **~2-3s** per raggiungere target → non triggerare cambi
  termici rapidi consecutivi, non avranno effetto e confondono il partecipante
- Configurazioni: 1 o 2 TouchDIVER (dx, sx, o entrambe)
- Verificare stato middleware prima dell'inizio sessione
- SDK docs: https://weart.it/docs/sdkunity/2.2.0/

**`WeArtController` prefab in scena:**
- `Device Generation` = `TD_Pro`
- `Start Calibration Automatically` ON: calibrazione TouchDIVER all'avvio della scena
- `Allow Gestures`, `Use External Grasp System`, `Start Raw Data Automatically` OFF

## Aggiungere oggetti touchable

Regole per un nuovo oggetto da rendere tattile (vedi `Cube`, `Cylinder`, `Prism`, `Star`
nella scena come riferimento):

**Mesh Collider** (se non è una primitiva tipo Cube/Sphere)
- `Convex` ON: necessario per il physics system
- `Is Trigger` ON: l'oggetto si lascia attraversare (la sensazione la generano i pad
  aptici, non c'è blocco fisico)

**Rigidbody** (obbligatorio: il sistema haptic reagisce solo a oggetti con Rigidbody)
- `Use Gravity` OFF, `Is Kinematic` ON → oggetto floating, controllato a mano
- `Mass = 1`, `Angular Drag = 0.05` (default)

**`WeArtTouchableObject`**
- Spuntare `Stiffness` / `Texture` / `Temperature` secondo la sensazione desiderata.
  **Eccezione Level 3**: lì `Temperature` va lasciato SPENTO, la temperatura la comanda
  `ThermalObjectCue` (`SceneObjectBinding.Bind` lo spegne comunque e
  `HapticResearch/Level 3/Valida oggetti` segnala chi lo riaccende)
- `Disable Dynamic Force` ON: senza questo la forza viene applicata in modo sbagliato
  sulle dita
- `Graspable` ON solo se l'oggetto deve essere afferrabile

**Oggetti complessi (non convex)**
Split in più mesh convex separate (es. una stella = 1 cubo + 4 prismi). Ogni parte ha
il suo Rigidbody + WeArtTouchableObject. Il parent è un GameObject vuoto che fa solo
da container, niente collider/rigidbody sul parent.

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
| F2 | Mostra / nasconde sottotitoli voce (giocatore / narratore) |
| F3 | Mostra / nasconde HUD operatore |
| Space | Calibrazione completa (guanto + Vive Tracker), palmo appoggiato al tavolo |
| Invio / R | Avvia (o riavvia) livello, preceduto dalla calibrazione / ripeti annuncio |
| N | Livello successivo (Level 1 e Level 2) o torna al menu (Level 3), solo a livello completato |
| G | Gira la tessera sotto il dito (memory tattile) |
| Fine | Chiude il livello 3 (colazione o memory) |
| M | Muta / riattiva il microfono |
| K | Avvia la taratura termica (Level 2), Esc la interrompe |
| C / F | Risposta "caldo" / "freddo" in taratura, se il microfono non c'è |

In VR la mappatura passa al controller / tracking nativo, la sorgente attiva è gestita
da `HandInputManager`.

## Convenzioni codice

- Commenti **in italiano** (convenzione storica, manteniamola anche sui nuovi script)
- `SerializeField` privato invece di public field
- Niente `GetComponent` / `Find` in `Update`: cachare in `Awake`
- Configurazione via Inspector (`[SerializeField]`), non costanti hard-coded
- Physics nei `FixedUpdate` con Rigidbody
- API Unity 6: `FindObjectsByType<T>(FindObjectsSortMode.None)`, non `FindObjectOfType`
- Logging dati sperimentali via un `SessionLogger` centralizzato (da introdurre in
  `Assets/Scripts/Experiment/` quando arriviamo al raccolto dati), non `Debug.Log` sparsi
- Namespace `HapticResearch.<Sottosistema>` sui nuovi script: i vecchi vanno
  retro-fittati a poco a poco, non in un colpo solo

## Logging dati sperimentali

- Formato: CSV in `<persistentDataPath>/SessionLogs/`
- Campi minimi: timestamp ISO 8601, `participantId` (anonimo), level, condition,
  eventType, eventData (JSON)
- **MAI** dati personali identificativi: solo ID anonimi assegnati prima della sessione

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
- **MAI** modificare guid/fileID nei `.unity` / `.prefab` / `.asset`: generati da Unity
  e legati all'installazione locale; il collegamento Inspector lo fa la persona che apre
  la scena
- **MAI** toccare file dentro `Packages/WEART-UNITY-SDK/` o `Assets/SteamVR/`: è codice
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

UniBS, Prof.ssa Anna Richelli (supervisione), Lorenzo Ghiro (ricercatore),
Luca Castelnovo (tesista), Simone Saleri (stagista).
