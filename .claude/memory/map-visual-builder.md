---
name: map-visual-builder
description: Client procedural arena visuals (grid floor + walls) when a map has no authored visual prefab
metadata: 
  node_type: memory
  type: project
  originSessionId: 5ba90579-1194-4856-9914-4e0824254ebc
---

`MapVisualBuilder` (`client/.../Core/Match/Scripts/Services/MapVisualBuilder.cs`, added 2026-06-23) procedurally builds a readable arena visual straight from the baked `MapData` so visuals always match physics. `MatchSessionRunner.LoadMap` now: authored `MapDefinition._visualPrefab` wins; otherwise `MapVisualBuilder.Build(mapData)`.

Builds: tiled **grid floor** (procedural `Texture2D`, `mainTextureScale = (width, depth)` → 1 grid cell per world unit so movement is readable), a gray **cube per static box fixture** (`entity.PositionX/PositionY`, scale `Width × WallHeight(2) × Height`), and team **spawn pads** (flat colored cylinders from `SpawnAreas`, team0 blue / team1 red). Runtime materials via `Shader.Find("Standard")` (Built-in RP — ground material is the Standard shader, fileID 46; Standard must stay in AlwaysIncludedShaders). **No Unity colliders** — `StripCollider` removes the primitive's collider (`Object.Destroy` at runtime, `DestroyImmediate` in-editor preview). Breakable boxes are NOT drawn here (streamed/rendered separately).

Why: the elimination map (`ArenaPickup.asset`) had `_visualPrefab: {fileID: 0}` — zero visuals. This makes it render without authoring a prefab. `arena_tdm`/`arena_basic` keep their authored prefabs.

`arena_pickup.json` (kept in sync: server `src/Server/ClashUp.GameServer/Maps/Data/` + client `Assets/Core/Match/Content/Maps/`) gained obstacles (entities 10-17: corner pillars ±18,±18; mid pillars ±18,0→moved to side walls ±12,0; spawn-front cover walls 0,±19). See [[elimination-mode]], [[bot-system]] (bots break these + the boxes).
