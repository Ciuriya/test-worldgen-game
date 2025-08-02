# Codebase Overview

This document captures the major subsystems, data-flow, and architectural patterns found in the *test-worldgen-game* Unity project.  It is **not** an exhaustive API reference; instead it explains how the pieces fit together so that future contributors can quickly orient themselves.

---

## 1. Top-Level Layout

```
Assets/Src                      → Runtime code grouped by domain
Assets/Src/Components/Extruders → 2D-to-3D mesh generators (incl. WorldMap)
Assets/Src/Graphics             → Rendering helpers (IndexMaps, wrappers)
Assets/Src/Graphics/Editor      → Custom editor tooling (Tilemap Painter)
Assets/Art/IndexMaps            → Authored index-map textures produced by Painter
Assets/Art/USS                  → UI Toolkit style-sheets used by editors
Assets/Art/Icons                → Editor icons
Assets/Art, Prefabs …           → Game art referenced by scripts
Assets/Libraries                → 3rd-party code (Unity-Delaunay, helpers)
Documentation                   → Project & code documentation (this file, etc.)
```

All gameplay scripts live under `Assets/Src`, separated into functional folders (Components, Gameplay, UI, General, Extensions).

---

## 2. Core Runtime Architecture

### 2.1 `GameCore` (Singleton)
* **File:** `Assets/Src/General/GameCore.cs`
* Bootstraps the game and survives scene loads (`DontDestroyOnLoad`).
* Holds a list of `CoreSystem` instances that each implement a reusable domain (Prefab loading, UI, Gameplay, …).
* Drives the main update loop by forwarding Unity’s `Update` phases (`EarlyUpdate`, `Update`, `LateUpdate`) to every system.
* Listens to `SceneManager.activeSceneChanged` and propagates scene-change events.

### 2.2 `CoreSystem` lifecycle
`CoreSystem` is an abstract base that exposes empty virtual hooks:
```
EarlyStart → Start → LateStart
EarlyUpdate → Update → LateUpdate
Destroy
OnSceneChanged
```
Systems override only what they need, keeping responsibilities isolated and discoverable.

---

## 3. Key Systems

### 3.1 `PrefabSystem`
* Loads *Prefabs* and *ScriptableObjects* referenced in the `GameCore` inspector into case-insensitive dictionaries.
* Provides fast, type-safe retrieval helpers used across the project.

### 3.2 `UISystem`
* Lightweight **MVC** stack for screens.
* `Dictionary<ViewEnum, ViewController>` + `Stack<ViewEnum>` to handle navigation.
* Each `ViewController` owns a `View` that instantiates a prefab when constructed.
* Supports hiding previous views, clearing history, and simple back-navigation.

### 3.3 `GameplaySystem`
* Responds to UI events (e.g. *Play* button) and orchestrates the **world-generation** pass when the *Game* scene (build 2) loads.
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
        │  — multi-threaded mesh bake (floors & walls)
Scene hierarchy ← Mesh objects parented under a "Map" GameObject
```

### 4.1 Data Model
* **Zone** – A Voronoi cell; knows its `Center`, list of `Corners`, `Edges`, `Neighbors`, and assigned `Room` (may be *null*).
* **Room** (SO) – Visual/semantic descriptor (material, colour, texture sets).
  * Includes:
    * `WallIndexMaps` / `FloorIndexMaps` – lists of `IndexMapWrapper` describing how to tile textures.
    * `PickDifferentIndexMaps`, `PickSequentially`, `MergeWalls` – rules controlling texture variation.
* **Corner** - Shared vertex between zones; tracks adjacency.
* **Edge** – Connects two Corners and references the zones on either side.
* **IndexMapWrapper** – Helper class that converts atlas-id maps to real UVs.  Provides tile-size, wrap-mode (`None`, `Fit`), UV offset helpers, etc.
* **ZoneRoomWrapper** – Runtime cache tying a `Zone` to its chosen index-maps plus merged-wall bookkeeping.

### 4.2 Grammar System
* `GraphGrammarRule` (SO) encapsulates an **Input** filter (`GrammarInput`, with nested `GraphGrammarRequirement` list) and an **Output** room.
* `World.ApplyGrammarRules` iterates rules, repeatedly scans candidate zones, and converts them while honouring limits, randomness, and complex spatial requirements (neighbor counts, connectivity, distance from edge, etc.).

### 4.3 Mesh Creation – *New Generation Pipeline*
`World.GenerateMesh` spawns a root *Map* object rotated 90° on **X** for a top-down (2.5D) view, then adds a single `WorldMapExtruder` component which performs a **two-phase, job-driven** bake.

1. **Setup** – `WorldMapMeshHelper.Setup()` caches per-zone texturing data (`ZoneRoomWrapper`s) and pre-computes merged-wall offsets.
2. **Phase A – Floors**
   * Builds `WorldMapMeshData` for every zone through `CreateZoneFloorData` (stores points, positions, material info).
   * Packs lightweight structs into `NativeArray<JobMeshInfo>` plus `NativeArray<float2/float3>` for burst jobs.
   * Schedules `CreateWorldMapMeshJob` via `JobHandle` with `ScheduleBatch` (batch size = 32) generating vertices, UVs & triangles in parallel.
   * On completion (`WaitForJobToFinish(false)`), `FinalizeFloorMeshes` materialises `Mesh` instances and immediately assembles a list of edge-jobs.
3. **Phase B – Edges/Walls**
   * `CreateZoneEdgesData` processes every perimeter segment **once** (deduplicated with a `HashSet<MeshEdge>`), generating four-vertex quads that reference both the zone and its neighbour.
   * The same `CreateWorldMapMeshJob` runs again, this time writing two sub-meshes when both sides are textured differently.
   * Finalisation adds the wall meshes, disposes native memory, and records generation time (`GenerationTime`).

Important implementation details:
* **Custom vertex layout** (`WorldMapMeshHelper.CustomVertex`) – Pos(3) / Normal(3) / Tangent(4h) / UV0(2h) / UV1(2h) – keeps memory footprint tiny.
* **Static triangle caches** – `ZoneTriangles`, `NeighborTriangles`, `FlatTriangles` avoid per-mesh allocations for quads.
* **Burst-compiled math** – normals & tangents calculated inside the job; UVs derived using atlas-aware helpers with optional flip.
* **NativeArray safety** – every allocation is disposed in `CleanupAfterGeneratingMeshes` or `WorldMapMeshData.Dispose()`.

### 4.4 Reusable Extruders
All extruder components inherit from `Extruder` (`Assets/Src/Components/Extruders/Extruder.cs`).  Shared knobs include `ExtrusionDepth`, `ExtrusionHeight`, `Offset`, `ShadowMode`, `Layer`, and `MeshDefaultMaterial`.

Other domain-specific extruders (Box, Circle, Polygon, Composite) still use the original immediate-mode mesh builder; they might gradually migrate to the job-based backend.

### 4.5 Index-Map Texturing Pipeline
* `IndexMapWrapper` converts authored **index-map** PNGs into UV space.  An index-map packs the atlas-cell id into R/G bytes and leaves **A = 255** where content exists.
* Per-zone floor & wall materials are runtime clones whose `"_MainTex"` & `"_IndexTex"` are wired by `WorldMapMeshData`.
* The shader receives helper uniforms (`_AtlasDims`, `_Repeat`, `_IndexTex_TexelSize`) and resolves the actual tile per-fragment.
* Merged-wall mode treats the full perimeter as one strip, evenly repeating a single atlas slice.

---

## 5. Editor Tools

### 5.1 Tilemap Painter (NEW)
* **Files:**
  * `TilemapPainterEditor.cs` – window bootstrap + high-level layout.
  * `TilemapPainterAtlasPanel.cs` – atlas selection, grid overlay, brush/erase tools.
  * `TilemapPainterDrawPanel.cs` – interactive painting canvas, save/load workflow.
  * `Art/USS/Editor/TilemapPainterEditorStyleSheet.uss` – UI Toolkit styles.
* **Menu:** *Custom → Tilemap Painter* opens the editor.
* **Atlas Panel**
  * Load any square `Texture2D` atlas; configure `Grid Size`.
  * Click a cell to select; brush/eraser icons switch tools.
  * Under the hood a transparent overlay highlights selection (`ToggleAtlasGridCell`).
* **Draw Panel**
  * Square canvas (`drawImageSize`, default 512²) subdivided by the same grid size.
  * Drag-brush cells to populate an **index-map**; erase reverts to a checkerboard placeholder.
  * `Save Image` serialises a PNG into `Assets/Art/IndexMaps/`, auto-configuring import settings (uncompressed, point, linear).
  * `Load Image` performs the reverse – deconstructs an index-map into the drawing canvas so it can be edited.
* **Encoding scheme** – Atlas cell ID = **low byte → R**, **high byte → G**; A = mask.
* The generated textures plug straight into `Room` → `WallIndexMaps`/`FloorIndexMaps` for in-game use.

---

## 6. UI Implementation Details

* **LoadingScreen** – Simple animated ellipsis while next scene loads; immediately queues the main menu (or dev-override scene) on construction.
* **MainMenu** – Contains a *Play* button that triggers `GameplaySystem.LoadGame`.
* Views locate child elements via the recursive `GameObjectExtensions.FindChild<T>` helper, avoiding hard-coded hierarchies.

---

## 7. Scene Flow

1. **Loading Screen** scene (build 0) starts by default (enforced by `EditorStartFromLoading`).
2. Loading screen pushes `LoadingScreenView` and then loads scene 1.
3. **Main Menu** scene (build 1) initialises and `UISystem` switches to `MainMenuView`.
4. Pressing *Play* → `GameplaySystem` flags that the world must be generated, loads scene 2.
5. On scene 2 load, `GameplaySystem` kicks off `WorldGenerator` → `WorldMapExtruder`; meshes appear under *Map* GameObject.

---

## 8. External Dependencies

* **Unity-Delaunay** – Fortune-based Voronoi implementation.
* **MyBox Attributes** – editor niceties (`MinMaxRange`, …).
* **TextMeshPro**, URP assets, plus custom shaders (for index-map sampling).
* **Unity Collections / Jobs / Burst** – high-performance data & math stack.

---

## 9. Extensibility Notes

* Adding a new screen – create a `View` prefab & script, a `ViewController`, extend `ViewEnum`, register in `UISystem.LoadView()`.
* Adding new rooms – create `Room` & `GraphGrammarRule` ScriptableObjects (and optional index-maps); hook them in `WorldGeneratorData`.
* Extruders – derive from `Extruder`, override `Extrude()`, call helpers to create Job batches where possible.
* Editor tooling – follow the UI Toolkit pattern (`CreateGUI`, panels, style-sheets) demonstrated in Tilemap Painter.

---

## 10. TODOs & Observations (2025-08)

* Port existing Box/Circle/Polygon extruders to the new job-based backend.
* Connected-region caching & grammar loop optimisation remain open.
* No explicit save/load; world resets every run.

With these building blocks the project delivers a modern, data-driven roguelike foundation: procedural Voronoi topology, rule-based theming, job-accelerated mesh generation, and a powerful custom editor for texture authoring — all orchestrated through a lightweight system framework.
