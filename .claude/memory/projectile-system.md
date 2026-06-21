---
name: projectile-system
description: "Server-authoritative projectile simulation + client dead-reckon visuals: movement, collision, AoE explosions, projectile_spawn/destroy events, telegraph preview"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7b00e1cd-0bda-46ff-924c-8f7c4f997a9e
---

# Projectile System

Built in 2026-06 (previously fully stubbed — `EvaluateProjectile` only emitted a spawn event and did nothing else). Used by the Mage (`mage_bolt` single-target, `mage_blast` AoE explosion). Follows the **server-authoritative / dumb client** rule: server simulates movement + damage; client renders a cosmetic dead-reckon that self-corrects on the authoritative destroy event.

## Data model — `ProjectileConfig` (`ClashUp.Shared/Abilities/`)
Plain JSON inside `AbilityDefinition` (NOT `[MessagePackObject]` — no `[Key]`). Fields:
`Speed` (units/sec), `Radius` (collision), `MaxRange`, `MaxPierceCount` (unused in v1 — detonates on first hit), `OnHitEffect`/`OnHitAmount` (direct hit), **`AoeRadius`** (0 = single-target; >0 = explode), **`AoeAmount`**, **`AoeEffect`**, **`LifetimeTicks`** (0 ⇒ derived `ceil(MaxRange/(Speed*dt))`).

## Server — `ProjectileSimulation.cs` (`ClashUp.GameServer/Simulation/`)
- Owned by `AetherServerSimulation` as `_projectiles`. Ticked in `Step()` **after** `_abilities.Tick(...)`, **before** `CurrentTick++`. `DrainAbilityEvents()` concatenates `_abilities.DrainEvents()` + `_projectiles.DrainEvents()`.
- `AbilityExecutor.EvaluateProjectile` hands off to `_projectiles.Spawn(...)` (executor stores `_projectiles`/`_dt` as fields set at top of `Tick` to avoid threading through the eval chain).
- **Monotonic `_nextProjectileId`** — fixes the old bug of reusing the caster's entity id (non-unique).
- Per tick: advance `Speed*dt`; `OverlapCircle(x,z,Radius)` for nearest non-caster alive enemy; detonate on first hit OR `Traveled>=MaxRange` OR lifetime elapsed. Reuses the `EvaluateHitbox` damage pattern.
- Detonation: direct hit applies `OnHitAmount`; if `AoeRadius>0`, a fresh `OverlapCircle(impact, AoeRadius)` applies `AoeAmount` to everyone non-caster/alive **excluding the direct-hit target** (no double damage). Each victim emits `ability_hit` (so existing client hit-VFX fires for free). Then emits `projectile_destroy`.
- **No team filtering** (consistent with hitboxes — excludes only the caster). Invuln respected automatically via `HealthTable.ApplyDamage`.
- Wall collision is OUT (v1) — `PhysicsWorldManager.Raycast` exists one layer under `MatchPhysicsWorld` if added later.

## Events (System.Text.Json payloads)
- `projectile_spawn`: `{ id, abilityId, x, z, yaw, speed, maxRange, aoeRadius }`
- `projectile_destroy`: `{ id, x, z, aoeRadius, reason }` — `x,z` = **authoritative** impact; client places the explosion exactly there.
- **MatchEvents are fire-and-forget / unreliable** (`MatchTickLoop.BroadcastAsync` → `group.All.OnMatchEvent`; NOT part of the snapshot baseline, never resent). This is why the client must self-despawn projectiles independently — a dropped `projectile_destroy` must never leave one flying forever.

## Client — `ProjectileViewSystem.cs` (`Core/Gameplay/Scripts/Abilities/`)
- `IStartable/ITickable/IDisposable`, registered in `MatchLifetimeScope` inside the `if (_abilityVisualRegistry != null)` block (beside `AbilityVisualHandler`/`TelegraphController`). Deps: `MatchHubReceiver`, `AbilityVisualRegistry`.
- `projectile_spawn` → instantiate `AbilityVisualConfig.ProjectilePrefab` (fallback: blue primitive sphere), store dir/speed/maxRange.
- `Tick()` → dead-reckon `pos += dir*speed*Time.deltaTime`; **self-despawn at `Traveled>=MaxRange`** (safety for missed destroy events).
- `projectile_destroy` → destroy GO; spawn `HitVfxPrefab` + (if `aoeRadius>0`) `AbilityAreaFlash.Spawn(TargetCircle r=aoeRadius)` explosion at authoritative impact, even if the visual already self-despawned.
- The old empty `projectile_*` stubs in `AbilityVisualHandler` are harmless no-ops (both subscribe; ProjectileViewSystem does the work).

## detonate-at-origin (TargetPoint casts)
`Spawn(..., detonateAtOrigin: true)` makes a projectile **appear at the spawn point and explode
there on tick 1** (no travel): sets `StepPerTick=0, MaxRange=0, lifetime=1`, and the spawn event
sends `speed=0, maxRange=0`. Used by `CastMode.TargetPoint` projectiles (see [[target-point-cast]]).
**Client guard:** `ProjectileViewSystem.Tick` must only self-despawn travelling projectiles
(`MaxRange > 0 && Traveled >= MaxRange`) — a `MaxRange==0` projectile would otherwise be culled on
frame 1 before the explosion. A separate `Age >= MaxVisualSeconds` timeout covers missed destroy
events for both kinds.

## Telegraph preview (ranged AoE)
`TelegraphController.Tick` offsets the primary `TargetCircle` renderer origin by `forward(LiveAimYaw) * ForwardOffset` so it previews where a projectile-AoE lands. `AbilityShapeMesh` renders `TargetCircle` centered, so only the transform moves.

## Verifying
Server build proves types. The runtime JSON path (System.Text.Json + `JsonStringEnumConverter`) can be smoke-tested with a throwaway console referencing `ClashUp.Shared` that deserializes `ability_mage_*.json` and asserts the AoE/ForwardOffset fields — clean up after. Live combat (damage) needs 2 players; solo still shows projectile + explosion + telegraph. Server logs `[PROJ]` spawn/detonate + reused `[HIT]`.

See [[netcode-architecture]], [[ability-authoring]], [[stat-health-system]].
