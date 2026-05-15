# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6 (6000.3.10f1) haptic thesis project — a tactile learning experience combining hand simulation, braille reading, and object manipulation with haptic feedback via WEART glove SDK. Desktop standalone target.

## Build & Run

This is a standard Unity project. Open it in Unity 6.0.3f1 (Hub or CLI). There are no custom build scripts, Makefiles, or CI/CD pipelines.

- **Open project:** Unity Hub → Add → select this directory
- **Main scene:** `Assets/Scenes/SampleScene.unity`
- **Build:** File → Build Settings → Build (PC Standalone)
- **Test framework:** `com.unity.test-framework` v1.6.0 is installed but no tests are configured yet

## Architecture

### Rendering & Input
- **URP** (Universal Render Pipeline v17.3.0) with separate PC and Mobile renderer assets under `Assets/Settings/`
- **New Input System** (v1.17.0) — action map defined in `Assets/InputSystem_Actions.inputactions`

### Interface-driven design
Core interactions are abstracted via interfaces in `Assets/Scripts/Interface/`:
- `IGrabbable` — grab/release physics objects
- `ITouchable` — touch to spawn copies of prefabs
- `IPressable` — button press actions
- `IScenari` — scenario navigation (Next/Back/Reset)
- `IGridOrientable` — rotation snapping for grid placement

### Hand system (`Assets/Scripts/Hands/`)
Dual-hand simulation with per-finger joint articulation:
- **HandInputManager** — orchestrates hand switching (Space), cursor lock, fullscreen
- **HandPhysicsController** — Rigidbody movement (mouse + keyboard), confined to table bounds
- **HandCollisionController** — tracks which hand parts touch which objects, manages audio feedback
- **HandColliderPart** — per-finger-segment collision detection (`touchDistance` = 0.03m)
- **HandCloseController** — finger selection (keys 0-5) and scroll-wheel closure
- **HandGrabController** — left-click grab/press, right-click destroy, S/D rotate in hand
- **FingerController / ThumbController** — joint interpolation between open/closed poses

### Braille system (`Assets/Scripts/Braille/`)
3-level braille learning progression:
- **BrailleDatabase** — static letter/digit→6-dot pattern encoding
- **BrailleGrid** — spawns configurable rows×columns of BrailleCells (cell size 0.2×0.3m)
- **BrailleCell / BrailleDot** — 6-dot representation with raised/hidden states
- **BrailleGameManager** — level progression (L1: single char, L2: random word, L3: two words)
- **BrailleWordProvider** — loads words from TextAsset

### Scenario management (`Assets/Scripts/Scenarios/`)
- **TableScenarioManager** — top-level controller for 3 scenarios (menu + 3 gameplay)
- **Scenario1SubManager** — grid-based object placement sub-levels
- **Scenario2Manager** — braille reading with grid resizing per level (1×2 → 1×5 → 2×5)

### Grid & object systems
- **BuildGrid** (`Assets/Scripts/Grid/`) — 13×8 cell snap grid (cell size 0.075m), prevents overlapping
- **GrabbableObject** / **TouchableObject** (`Assets/Scripts/Objects/`) — physics grab with optional grid snapping, touch-to-spawn factory
- **TriangleGridOrientation** — rotation snapping at 90° increments for prism shapes

### Audio (`Assets/Scripts/Audio/`)
- **ObjectAudioFeedback** — spatial 3D audio feedback differentiated by object type (table/pressable/grabbable/touchable/default)

### Camera
- **TopCameraFitTable** (`Assets/Scripts/Camera/`) — orthographic top-down view fitted to table dimensions

### External SDK
- **WEART SDK v2.1.5_preview** — haptic glove integration, referenced as local package with absolute path in `Packages/manifest.json`
- **Device:** TouchDIVER Pro — 6 actuation points (Thumb, Index, Middle, Annular, Pinky, Palm)
- **SDK docs:** https://weart.it/docs/sdkunity/2.1.0_preview/
- **Haptic coexistence:** Hand movement uses custom scripts (HandPhysicsController, FingerController); WeArtHandController is intentionally disabled. Haptic output (temperature, force, texture) flows through WeArtHapticObject/WeArtTouchableObject trigger collisions → WeArtController → TCP:13031 → middleware → device
- **Runtime requires:** WEART Middleware running + TouchDIVER Pro connected

## Controls (defined in scripts)

| Input | Action |
|---|---|
| Mouse move | Hand position |
| Q / E | Hand up / down |
| Arrow keys, Z / X | Hand rotation |
| Space | Switch left/right hand |
| 0-5 + scroll | Select finger(s) + close/open |
| Left click | Grab / Press |
| Right click | Destroy held object |
| S / D | Rotate grabbed object |

## Conventions

- Comments are in **Italian**
- Scripts are organized by feature domain under `Assets/Scripts/`
- Prefabs in `Assets/Prefabs/`: Braille, Button, CellGrid, Cube, Cylinder, Prism
- Configuration is done via Unity Inspector (`[SerializeField]` fields) rather than code constants
- Physics interactions use `FixedUpdate` and Rigidbody-based movement
- **MAI cambiare i path SDK/pacchetti in `manifest.json`** senza coordinamento esplicito — il path è specifico per macchina e la cartella SDK (`Packages/WEART-UNITY-SDK*/`) è in `.gitignore`
- **MAI modificare riferimenti guid/fileID nei file `.unity` / `.prefab` / `.asset`** — sono generati da Unity e legati all'installazione locale
