# Codebase Overview

This document captures the major subsystems, data-flow, and architectural patterns found in the *test-worldgen-game* Unity project.  It is **not** an exhaustive API reference; instead it explains how the pieces fit together so that future contributors can quickly orient themselves.

---

## 1. Top-Level Layout

```
Assets/Src              → Runtime code grouped by domain
Assets/Art, Prefabs …   → Authoring assets referenced by scripts
Assets/Libraries        → 3rd-party code (Unity-Delaunay, helpers)
Documentation           → Project & code documentation (this file, etc.)
```

All gameplay scripts live under `Assets/Src`, separated into functional folders (Components, Gameplay, UI, General, Extensions).

---

## 2. Core Runtime Architecture

### 2.1 GameCore (Singleton)
* **Location:** `Assets/Src/General/GameCore.cs`
* Bootstraps the game, survives scene loads (via `DontDestroyOnLoad`).
* Holds a list of `CoreSystem` instances that implement reusable domains (Prefab loading, UI, Gameplay, …).
* Drives the main update loop by forwarding Unity's `Update` phases (`EarlyUpdate`, `Update`, `LateUpdate`) to each system.
* Listens to `SceneManager.activeSceneChanged` and propagates scene-change events to systems.

### 2.2 CoreSystem lifecycle
`CoreSystem` is an abstract class offering empty virtual hooks:
```
EarlyStart → Start → LateStart
EarlyUpdate → Update → LateUpdate
Destroy
OnSceneChanged
```
Systems override only what they need, keeping responsibilities isolated and discoverable.

---

## 3. Key Systems

### 3.1 PrefabSystem
* Loads *Prefabs* and *ScriptableObjects* referenced in the `GameCore` inspector into case-insensitive dictionaries.
* Provides fast, type-safe retrieval helpers used across the project.

### 3.2 UISystem
* Implements a lightweight **MVC** stack for screens.
* Maintains a `Dictionary<ViewEnum, ViewController>` and a `Stack<ViewEnum>` to handle navigation.
* Each `ViewController` owns a `View` which instantiates a prefab when constructed.
* Supports hiding previous views, clearing history, and simple back-navigation.

### 3.3 GameplaySystem
* Responds to UI events (e.g., *Play* button) and orchestrates a **world generation** pass when the *Game* scene (build index 2) loads.
* Bridges the UI & generation pipeline, ensuring heavy work happens only after the loading screens.

---

## 4. World Generation Pipeline

```
WorldGeneratorData (ScriptableObject)
        │  ↑ tweakable parameters in editor
WorldGenerator
        │  — builds Voronoi graph via Unity-Delaunay
World
        │  — converts graph to Zones / Corners / Edges models
        │  — applies GraphGrammarRules to assign Rooms
WorldMapExtruder (Component)
        │  — generates 3D mesh for zones & edges
Scene hierarchy ← Mesh objects parented under a "Map" GameObject
```

### 4.1 Data Model
* **Zone** – A Voronoi cell; knows its `Center`, list of `Corners`, `Edges`, `Neighbors`, and assigned `Room` (can be *null* for empty).
* **Corner** – Shared vertex between zones; tracks adjacency.
* **Edge** – Connects two Corners and references the zones on either side.
* **Room** (SO) – Visual/semantic descriptor (material, color, texture, display name).

### 4.2 Grammar System
* `GraphGrammarRule` (SO) encapsulates an **Input** filter (`GrammarInput`, with nested `GraphGrammarRequirement` list) and an **Output** room.
* `World.ApplyGrammarRules` iterates rules, repeatedly scans candidate zones, and converts them while honouring limits, randomness, and complex spatial requirements (neighbor counts, connectivity, distance from edge, etc.).

### 4.3 Mesh Creation
* `World.GenerateMesh` spawns a root *Map* object rotated 90° on X to provide a top-down 2.5D view.
* `WorldMapExtruder` traverses zones:
  * Creates a *floor* mesh per zone by triangulating polygon points.
  * Generates *edge* quads for visual wall thickness.
  * Uses the new Unity `MeshDataArray` API for zero-allocation, manual vertex layout; colours and materials derive from the owning `Room`.

### 4.4 Reusable Extruders
Located under `Assets/Src/Components/Extruders`, these components can turn 2D collider shapes (Box, Circle, Polygon, Composite) into 3D meshes with configurable height/depth, shadow mode, layering, etc.  They share logic via the abstract `Extruder` base which offers helpers for vertex creation, triangulation, and GameObject wiring.

---

## 5. UI Implementation Details

* **LoadingScreen** – Simple animated ellipsis while next scene loads. Immediately queues the main menu (or dev-override scene) on construction.
* **MainMenu** – Contains a *Play* button that triggers `GameplaySystem.LoadGame`.
* Views locate child elements via the `GameObjectExtensions.FindChild<T>` recursive helper, avoiding hard-coded hierarchies.

---

## 6. Scene Flow

1. **Loading Screen** scene (build 0) starts by default (enforced in editor by `EditorStartFromLoading`).
2. Loading screen pushes `LoadingScreenView` and then loads scene 1.
3. **Main Menu** scene (build 1) initialises and `UISystem` switches to `MainMenuView`.
4. Pressing *Play* → `GameplaySystem` flags that the world must be generated, loads scene 2.
5. On scene 2 load, `GameplaySystem` kicks off `WorldGenerator` → meshes appear under *Map* GameObject.

---

## 7. External Dependencies

* **Unity-Delaunay** – fortune-based Voronoi implementation providing sites, edges, and convenience methods.
* **MyBox Attributes** – used for nicer inspector controls (`MinMaxRange` etc.).
* **TextMeshPro**, URP assets, and custom shaders supply UI & rendering support but are mostly authored assets.

---

## 8. Extensibility Notes

* Adding a new screen: create a `View` prefab & script, a `ViewController`, extend `ViewEnum`, and register in `UISystem.LoadView()` switch.
* Adding new grammar rules or rooms: create `Room` & `GraphGrammarRule` ScriptableObjects and assign them in `WorldGeneratorData`.
* Systems can be added by inheriting `CoreSystem` and registering in `GameCore.InitializeSystems()`.

---

## 9. TODOs & Observations

* Several *todo* notes suggest optimisation opportunities (connected-region caching, grammar loop performance).
* Extruder rotation helpers assume parent rotation only; full transform hierarchy may need revisiting.
* No explicit save/load; world resets every run.

---

With these building blocks, the project delivers a modular, data-driven roguelike foundation: procedural world topology, flexible rule-based theming, and a minimalist UI stack—all orchestrated through a lightweight custom system framework.
