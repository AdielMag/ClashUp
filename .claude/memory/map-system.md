---
name: map-system
description: "Shared map data model, server/client loading, the map baker editor tool, and procedural visual builder"
metadata: 
  node_type: memory
  type: project
  originSessionId: 345c10fe-3909-4f06-ac80-7e8543804aea
---

## Map System
- **Shared POCOs**: `MapData`, `BakedEntityDef`, `BakedFixtureDef`, `SpawnArea` in `ClashUp.Shared/Maps/`
- **SpawnResolver**: Static `GetSpawnPosition(MapData?, teamId, slotIndex)` in Shared — falls back to linear layout when no map
- **Server**: `ServerMapStore` singleton loads `Maps/Data/*.json` (System.Text.Json). `AetherServerSimulation.LoadMap()` + spawn via `SpawnResolver`
- **Client**: `MapDefinition` SO (mapId, displayName, TextAsset json, visual prefab) + `MapRegistry` SO (`SerializedDictionary<string, MapDefinition>`)
- **Client deserialization**: `MapDataDeserializer` uses Newtonsoft.Json (not System.Text.Json — netstandard2.1)
- **Wire protocol**: `MapId` field on `MatchConfig` (Key 4), `MatchProvision` (Key 5), `JoinResult` (Key 7) — default `"arena_tdm"`. `MatchProvision` also carries `PlayerAssignments` (Key 6, `PlayerId→TeamId` list pre-assigned by `Matchmaker`) and `Characters` (Key 7, `CharactersConfig`)
- **Baker**: `ClashUpMapBaker` editor tool ("ClashUp/Bake Map to JSON") — scans `AetherRigidbody` + `SpawnPointMarker` components
- **Visual prefab**: Instantiated by `MatchSessionRunner.LoadMap()`, destroyed on Dispose. NO Unity colliders — physics is AetherNet only
- **Materials**: `Assets/Core/Match/Content/Maps/Materials/` — WallGray, GroundGreen, SpawnZone (transparent)
- **Maps**: `arena_basic` (40×30 landscape, legacy), `arena_tdm` (50×80 portrait, current default) — 24 entities, teams at Z=±35
- **Map JSON location**: server `src/Server/ClashUp.GameServer/Maps/Data/` + client `Assets/Core/Match/Content/Maps/` — must keep both in sync

See [map-visual-builder.md](map-visual-builder.md) for the procedural fallback renderer used when a map has no authored prefab.
