---
name: grass-stealth
description: "Grass stealth zones — server-authoritative per-viewer visibility filtering, hidden-player wire protocol, and the MagicOnion per-connection send mechanism it's built on"
metadata:
  node_type: memory
  type: project
  originSessionId: 3e63192a-817e-4c0a-a7c1-76bb20ac77eb
---

Added 2026-07-02. A player standing in a "grass" zone is invisible to any enemy (not teammate) who isn't standing in that *same* zone — classic MOBA-bush rule. Applies uniformly to bots (they use the same visibility check as human viewers).

## Why this required breaking the "one broadcast packet" assumption

Every tick previously built ONE `WorldStatePacket`/`SnapshotPacket` and broadcast it identically to all clients (`group.All.OnSnapshot`, see [[netcode-architecture]]). Hiding a player from *some* viewers but not others is impossible with a single shared packet — it required moving to per-connection sends when (and only when) someone is actually hidden.

## MagicOnion per-connection send API (previously undocumented in this repo)

`IGroup<T>` (`MagicOnion.Server.Hubs`, v7.10.0) is NOT limited to `.All` — it also exposes:
- `T Single(Guid connectionId)` — send to exactly one connection
- `T Except(IEnumerable<Guid> excepts)`
- `T Only(IEnumerable<Guid> targets)`

The connection id to use is `ServiceContext.ContextId` (a `Guid`), available as `Context.ContextId` inside any `StreamingHubBase` (e.g. `MatchHub`). Capture it per player on `JoinAsync` (`context.SetConnection(playerId, Context.ContextId)`) — a reconnect naturally overwrites the stale mapping. `StreamingHubBase` itself has no direct `ConnectionId` property; go through `Context.ContextId`.

**How I found this**: the type wasn't documented anywhere obvious in this repo. Reflected directly into the installed NuGet DLL instead of trusting memory/docs — see the "Reflecting into installed NuGet packages" note in [[debugging]]. `IGroup<T>.GetMembers()` alone only shows `RemoveAsync`/`CountAsync`; the `All`/`Except`/`Only`/`Single` members live on a **base interface** which `Type.GetMembers()` does NOT surface by default — you must also walk `type.GetInterfaces()` and call `GetMembers()` on those.

## Fast-path-by-default architecture

`MatchTickLoop.BroadcastAsync` checks `IServerSimulation.AnyPlayerHidden()` first. If nobody is in a grass zone this tick, it takes the exact old code path (one `EncodeDelta` + `group.All.OnSnapshot`) — zero added cost for the common case. Only when someone is hidden does it loop connected human players and send an `EncodeDeltaFor(viewerId, 0)` packet per connection via `group.Single(connId)`.

## Wire protocol: hidden means near-empty DTO, not omission

A hidden player's `PlayerStateDto` is NOT omitted from the array (that would leave a frozen "ghost" — `RemotePlayerInterpolator` keeps a track and its last sample until explicitly removed, see [[netcode-architecture]]) and NOT sent with real position + a flag (that would let a memory-reading client extract true position even though the UI hides it — defeats the whole point). Instead: `IsHidden=true` + every other field left at MessagePack default (`Id` is the only real field). Client (`ClientPredictionWorld.ApplyServerSnapshot`) skips `AddSample` entirely for a hidden dto so the interpolator's last real sample is preserved for a clean reveal-lerp later; `PlayerViewSystem` tracks a `_hidden` HashSet fed from `OnSnapshotDecoded` (same shape as the existing `_eliminated` set) and folds it into the `SetActive`/position-update gate in `Tick()`.

## Visibility rule implementation

`AetherServerSimulation.CanSee(viewerId, targetId)`: self always visible; same team (`_playerTeams` — already used by `TryGetBotView`'s enemy filter) always visible; otherwise resolve the target's zone via `TryGetZoneId` (linear scan of `MapData.GrassZones`, first match wins, zones assumed non-overlapping) — not in any zone → visible; in a zone → visible only if the viewer is in that *same* zone index. `TryGetBotView` reuses the identical `CanSee` check to exclude hidden enemies from "nearest enemy" candidates — this is the ONLY change needed to make bots stealth-aware, since bots have no separate perception system (see [[bot-system]]).

## IServerSimulation has 4 implementations — use default interface methods for optional capabilities

`IServerSimulation` is implemented by `AetherServerSimulation` (real), `MovementServerSimulation`, `NullServerSimulation`, and a test `FakeSim` in `BotTests.cs`. Adding `AnyPlayerHidden()`/`EncodeDeltaFor()` as **default interface methods** (`bool AnyPlayerHidden() => false;`) meant only `AetherServerSimulation` needed a real override — the other three compiled untouched. Reuse this pattern for any future optional-capability addition to this interface.

## MapData grass zones

`GrassZoneDef { X, Z, Width, Height }` — axis-aligned rectangle, array index = zone id (no explicit Id field, matching `SpawnArea`/`BoxSpawnDef` convention). Authored via a new `GrassZoneMarker` MonoBehaviour (mirrors `SpawnPointMarker`/`BoxSpawnMarker`), baked by `ClashUpMapBaker.BuildGrassZones()`. `arena_tdm.json` (both server and client copies, see [[map-system]]) got two hand-added test zones at X=±10 since no bake scene currently exists for that map (only `Arena_Basic_Bake.unity` does).

See [[server-test-suite]] for the test-writing technique this feature's tests required (avoid simulating movement — place players via explicit spawn coordinates instead).

## Visual patches (client, non-gameplay)

`arena_tdm` has an **authored** visual prefab (`Assets/Core/Match/Content/Maps/ArenaTdm_Visual.prefab`), not the procedural fallback — see [[map-visual-builder]]. Added a `GrassZones` container GameObject with two flat `Quad` decals (`GrassZone_0`/`GrassZone_1`) at the exact same X/Width/Height as the two `GrassZoneDef` entries baked into `arena_tdm.json`, so the visual always matches where the server actually hides players. Cloned the existing `SpawnZone` decal's exact recipe (Quad mesh, rotation `{x:90,y:0,z:0}` Euler / `{0.7071068,0,0,0.7071068}` quaternion to lay flat facing up, `y≈0.01` to sit just above the ground plane) rather than deriving the rotation from scratch. New material `GrassZone.mat` — copied `SpawnZone.mat`'s exact structure (Standard shader, `_Mode: 3` Transparent, `RenderType: Transparent`) with a green `_Color` tint; this is the established convention for flat ground-overlay decals in this project (`SpawnZone.mat`, `GrassZone.mat`) — prefer it over `Sprites/Default` for this specific asset category (see the shader note in [[debugging]] for the general case). `MeshCollider` auto-added by `gameobject-create primitiveType:Quad` was stripped, matching the "no Unity colliders in visual prefabs" rule.
