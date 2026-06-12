# Haptic Research – Unity VR Experiment

Progetto di ricerca dell'**Università degli Studi di Brescia (UniBS)**.

Studio sperimentale sull'uso del feedback aptico come supporto alla percezione tattile
in VR per **persone non vedenti e ipovedenti**.

L'obiettivo della ricerca è valutare come dispositivi aptici avanzati possano aiutare
gli utenti non vedenti a riconoscere forme, distinguere proprietà degli oggetti
(temperatura, materiale) e orientarsi in uno spazio virtuale, contribuendo allo
sviluppo di un "senso tattile" digitale.

Hardware di riferimento: **Weart TouchDIVER Pro** (force feedback, vibrazione,
temperatura).

## Overview

L'esperienza è strutturata in 3 livelli progressivi, ciascuno pensato per allenare un
diverso aspetto della percezione tattile in assenza di feedback visivo:

1. **Livello 1 – Riconoscimento di forme**
   Tavolo con oggetti di forme diverse. L'utente esplora il piano e afferra gli
   oggetti tramite grab; al contatto/grab riceve feedback aptico (forza, vibrazione)
   che permette di percepire la forma. Obiettivo: sviluppare la capacità di "vedere
   con le mani" gli oggetti virtuali.

2. **Livello 2 – Discriminazione termica**
   Due zone sul tavolo (calda e fredda) e 5 forme da smistare in base alla loro
   temperatura percepita tramite il dispositivo aptico. Obiettivo: allenare la
   discriminazione di proprietà non visive degli oggetti.

3. **Livello 3 – Orientamento spaziale (labirinto aptico)**
   Percorso a labirinto in cui la strada corretta è segnalata dal feedback termico
   (caldo/freddo) tramite il sensore aptico. L'utente naviga senza riferimenti
   visivi, basandosi esclusivamente sulle sensazioni tattili. Obiettivo: testare
   l'orientamento e la navigazione spaziale guidata dall'aptica.

Feedback aptici utilizzati:
- Vibrazione
- Temperatura
- Forza (sensore aptico TouchDIVER)

Tracking della mano reale tramite **2× HTC Vive Tracker 3.0** montati direttamente sui
TouchDIVER, calibrazione spazio SteamVR ↔ Unity al runtime.

## Accessibilità

Il progetto è pensato per essere utilizzato da persone non vedenti o ipovedenti.
Considerazioni di design:
- Tutta la grafica visiva è **secondaria**: l'esperienza deve funzionare anche senza
  vista
- Istruzioni e feedback di stato sono forniti tramite **audio** (TTS o voice-over)
- L'interfaccia di avvio dell'esperimento è navigabile senza visore montato (per
  assistere il partecipante)
- I parametri di calibrazione aptica (intensità forza, range termico) sono regolabili
  per singolo partecipante
- Nessun input dipendente esclusivamente dalla vista (no laser pointer come unico
  mezzo di selezione, no UI da "guardare")

## Requisiti

### Software
- **Unity 6000.3.15f1** (esattamente questa versione)
- **Weart Middleware** installato e in esecuzione ([download](https://weart.it/))
- **Weart Unity SDK v2.3.0** (incluso come pacchetto locale in `Packages/WEART-UNITY-SDK/`)
- **SteamVR** (necessario per il driver Vive Tracker)
- XR Plug-in Management + OpenXR (già configurato nel progetto)

### Hardware
- 1× o 2× **Weart TouchDIVER Pro**
- 2× **HTC Vive Tracker 3.0** + dongle USB
- 2× **SteamVR Base Station 2.0**
- (Opzionale) Visore VR compatibile OpenXR — Meta Quest via Link, Valve Index, Varjo
- PC Windows con specifiche VR-ready

I Vive Tracker funzionano **anche senza visore** dopo aver configurato il null driver
di SteamVR (vedi `CLAUDE.md`).

## Setup

```bash
git clone <repo-url>
cd haptic-game-thesis
```

1. Aprire il progetto in Unity Hub selezionando la cartella clonata
2. Lasciare che Unity importi le dipendenze (la prima apertura richiede qualche minuto)
3. Configurare il **null driver di SteamVR** per funzionamento senza HMD (una volta
   sola per macchina — procedura in `CLAUDE.md`)
4. Accendere i TouchDIVER, avviare il **Weart Middleware**, verificare che i
   dispositivi siano calibrati e riconosciuti
5. Accendere i Vive Tracker, collegare i dongle USB, posizionare le base station con
   linea di vista libera ai tracker
6. Recuperare i seriali dei tracker via `TrackerDebugger` e inserirli in
   `ViveTrackerManager` (procedura in `CLAUDE.md`)

## Run

### In Editor
1. Aprire la scena `Assets/Scenes/ViveTrackerScene.unity` (scena principale con
   tracking aptico) oppure `SampleScene.unity` (versione desktop senza VR)
2. Verificare che il middleware Weart sia connesso e i Vive Tracker visibili in
   SteamVR
3. Premere **Play**
4. Calibrare lo spazio: premere **Space** con il palmo della mano di calibrazione
   completamente appoggiato al tavolo, dita perpendicolari al bordo

### Build standalone
1. `File → Build Settings → PC, Mac & Linux Standalone → Windows x64`
2. Build → eseguire l'`.exe` con middleware Weart e SteamVR attivi in background

## Struttura del progetto

```
Assets/
├── Scenes/             # SampleScene, ViveTrackerScene
├── Scripts/
│   ├── Hands/          # simulazione bimanuale con articolazione per-dito
│   ├── Haptics/        # bridge custom sopra il SDK Weart
│   ├── ViveTracker/    # tracking esterno + calibrazione
│   ├── Braille/        # database 6-punti e progressione lettura
│   ├── Scenarios/      # gestione livelli e sub-livelli
│   ├── Grid/           # snap grid per placement
│   ├── Objects/        # GrabbableObject, TouchableObject
│   ├── Interface/      # IGrabbable, ITouchable, IPressable, ...
│   ├── Audio/          # feedback audio spaziale
│   └── Debug/          # tool di diagnostica
├── Prefabs/            # Cube, Cylinder, Prism, Braille, Button, CellGrid
├── XR/                 # settings OpenXR + OpenVR
├── SteamVR/            # pacchetto OpenVR locale (terze parti, READ-ONLY)
└── ...
Packages/
└── WEART-UNITY-SDK/    # SDK Weart v2.3.0 (READ-ONLY)
```

## Logging dati

I dati di sessione (timestamp, condizione, interazioni, errori) vengono salvati in:

```
<persistentDataPath>/SessionLogs/
```

Su Windows: `%USERPROFILE%\AppData\LocalLow\<Company>\<ProductName>\SessionLogs\`

Formato CSV, solo ID anonimi — **nessun dato personale identificativo**.

## Documentazione

- `CLAUDE.md` — riferimento tecnico completo: setup SteamVR senza HMD, procedura di
  assegnazione tracker, regole per nuovi oggetti touchable, convenzioni codice,
  cosa non toccare

## Team

**Università degli Studi di Brescia (UniBS)**

- Prof.ssa **Anna Richelli** – Supervisione accademica
- **Lorenzo Ghiro** – Ricercatore
- **Luca Castelnovo** – Tesista
- **Simone Saleri** – Stagista

## Note

- Calibrare sempre i TouchDIVER prima di ogni sessione
- Per i feedback termici, attendere che il dispositivo raggiunga la temperatura target
  (~2-3s) prima di iniziare il task
- Branch `main` = stabile, `dev` = sviluppo attivo
