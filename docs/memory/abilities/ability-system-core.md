---
name: ability-system-core
description: "Ability node/executor architecture, wire protocol, telegraph/hitbox shapes, tuning numbers, and visual/VFX wiring — the runtime side (pairs with ability-authoring.md for JSON schema)"
metadata: 
  node_type: memory
  type: project
  originSessionId: 345c10fe-3909-4f06-ac80-7e8543804aea
---

Companion to [ability-authoring.md](ability-authoring.md) (JSON schema/editor workflow), [projectile-system.md](projectile-system.md), [target-point-cast.md](target-point-cast.md), and [ability-editor-ui.md](ability-editor-ui.md). This file covers the runtime architecture those don't.

## Core Model
- **Shared POCOs**: `AbilityDefinition`, `AbilityNode`, `HitboxConfig`, `ProjectileConfig`, `TelegraphConfig` in `ClashUp.Shared/Abilities/`
- **Node types**: `Parallel=0, Hitbox=1, Projectile=2` (string enum in JSON — never integer, fragile on reorder)
- **Sequential chaining**: via `AbilityNode.Next` linked-list; root's output connects to first node, "Next" port chains the rest. **Parallel** node's `Children[]` — all run simultaneously, `Next` runs after all finish. No Sequence node (removed — chaining is implicit via Next).
- **Server**: `ServerAbilityStore` loads `Abilities/Data/*.json`; `AbilityExecutor` (per-match) processes input and ticks nodes. `ActiveAbility.Flatten()` builds flat node array; `EvaluateChain()` follows `Next` pointers; `EvaluateParallel()` uses `Children[]`
- **Wiring**: `CharacterDefinition.Abilities AbilityId[]` — defined in `CharactersConfig` (DB or static default). `AetherServerSimulation.EnsurePlayer` calls `AbilityExecutor.InitPlayer` with the character's ability list on first spawn. `_knownPlayers` hashset prevents double-init on reconnect.

## Wire Protocol
- `AbilitiesConfig` sent over wire (Key 9 in `JoinResult`, Key 8 in `MatchProvision`) — thin client payload: `AbilityClientInfo { Id(0), Telegraph(1), AutoRange(2), CastMode(3), CastShape(4) }`. Static `AbilitiesConfig.Default` is the fallback when server sends null. Full node trees still loaded from JSON locally by server (`ServerAbilityStore`).
- **`AbilityClientInfo.CastShape`** (Key 4, `TelegraphConfig?`): the damage footprint shown by the triggered cast flash. Auto-derived server-side in `ServerAbilityStore.BuildClientConfig` → `DeriveCastShape(root)`: Capsule hitbox→`{Capsule, Length, Width=2*Radius}`, Cone→`{ForwardCone, Length, Angle}`, Circle hitbox→`{TargetCircle, Radius}`, Projectile root→`{TargetCircle, Radius=AoeRadius>0?AoeRadius:max(Radius,0.3)}`. Single source of truth = the root node.
- **`MatchAbilitiesHolder`**: client-side lookup (`Dictionary<string, AbilityClientInfo>`), initialized in `MatchSessionRunner` from `JoinResult.Abilities`. Always apply `config ?? AbilitiesConfig.Default` in `Initialize` — MessagePack returns null for missing keys, NOT C# init defaults.
- **Cast modes**: `CastMode` = `Instant=0, Aimed=1, TargetPoint=2`. See [target-point-cast.md](target-point-cast.md).

## Shapes
- **Telegraph shapes**: `CircleAroundCaster`, `TargetCircle`, `ForwardLine`, `ForwardCone`, `Capsule` — direction always follows `AimYaw`. `TelegraphConfig` has `Width` (Key 5) for Capsule/ForwardLine and `ForwardOffset` (Key 6) for downrange `TargetCircle` previews (Mage Blast).
- **Shaped hitboxes**: `HitboxConfig.Shape` = `Circle`(default) / `Capsule` / `Cone`, plus `Length` (capsule segment / cone reach), `Angle` (cone full degrees); `Radius` = circle radius / capsule half-width. `AbilityExecutor.EvaluateHitbox` does an `OverlapCircle` broad-phase then refines: capsule = point-to-segment dist ≤ Radius; cone = dist ≤ Length AND angle-to-aim ≤ Angle/2. Damage matches the cast-flash footprint exactly.

## Tuning Reference
- **Brawler**: Punch (auto) = Capsule hitbox L3 R1 Offset0, 10 dmg, AutoRange 4, telegraph `CircleAroundCaster` r4. Charge (manual) = Cone hitbox L3.5 A90, 20 dmg, telegraph `ForwardCone` L3.5 A90.
- **Mage**: `mage_bolt` (auto, Projectile root, single-target) spd14 range9 8dmg cd24, telegraph CircleAroundCaster r9. `mage_blast` (manual Aimed, Projectile root, AoE) spd11 range10, 12 direct + 18 AoE r2.5, cd90, telegraph TargetCircle r2.5 ForwardOffset10.

## JSON serialization
`JsonStringEnumConverter` (server, System.Text.Json) + `StringEnumConverter` (editor, Newtonsoft) — MUST use string enums.

## Visual / VFX System
- **AbilityVisualConfig**: one SO per ability (`CreateAssetMenu: ClashUp/Ability Visual Config`). Holds VFX prefabs, sounds, telegraph visuals. Connected in editor Root Node → GUID written to JSON as `VisualConfigGuid`.
- **AbilityVisualRegistry**: SO (`ClashUp/Ability Visual Registry`) with `Entry[] { Guid, AbilityId, Config }`. `GetByGuid()`/`GetByAbilityId()` for lookups. Custom editor "Refresh GUIDs" button fills Guid strings from asset refs. `MatchLifetimeScope._abilityVisualRegistry` (was `_abilityVisualConfig`).
- **AbilityVisualHandler**: injects `AbilityVisualRegistry` + `MatchAbilitiesHolder`, resolves visuals by `GetByAbilityId` on `ability_cast` events. Spawns an `AbilityAreaFlash` of the ability's `CastShape`, oriented by `aimYaw`. Optional `CastVfxPrefab` instantiated with `Quaternion.Euler(0,aimYaw,0)`.
- **AbilityAreaFlash** (`Core/Gameplay/Scripts/Abilities/`): triggered ground-flash MonoBehaviour rendering the exact damage footprint, fades alpha over `CastFlashDuration` then self-destroys. `AbilityShapeMesh` (same folder) = shared static mesh builders (`BuildCircle/ForwardLine/Cone/Capsule`), reused by `TelegraphRenderer` AND `AbilityAreaFlash`. Per-ability `AbilityVisualConfig.CastFlashColor`/`CastFlashDuration` (alpha must be >0 to be used) — Punch yellow-gold, charge orange @0.28s.
- **TelegraphController**: `IStartable/ITickable/IDisposable` VContainer service — owns two `TelegraphRenderer` GameObjects (auto + primary). Resolves configs from `MatchAbilitiesHolder` + `MatchCharactersHolder` by local player's `AutoAttackId`/`ActiveAbilityId`. Switches auto↔primary based on `IAbilityInput.OnTouching`. Primary yaw tracks `IAbilityInput.LiveAimYaw`. Also hides the primary telegraph while `IAbilityInput.IsCanceling` (see [[joystick-ui]]). Registered in `MatchLifetimeScope`.
- **Telegraph materials**: `Assets/Core/Gameplay/Content/Telegraphs/` — `M_AutoTelegraph.mat` (Sprites/Default, yellow 0.35α), `M_PrimaryTelegraph.mat` (Sprites/Default, orange 0.55α). Use `Sprites/Default` NOT `Unlit/Transparent` (see [[debugging]]).
- **Editor tool**: `Tools → Ability Editor` (UIToolkit GraphView, `ClashUp.AbilityEditor.asmdef`). Save to the **server** path only. RootNode exposes Trigger Mode/Cast Mode/Auto Range — see [[feedback-ability-editor-sync]] if you edit the data model. Editor UI is USS-driven/category-extensible — see [ability-editor-ui.md](ability-editor-ui.md).
