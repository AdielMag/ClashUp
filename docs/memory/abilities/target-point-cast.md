---
name: target-point-cast
description: "CastMode.TargetPoint — joystick direction+distance picks a world point, cast originates there; joystick-magnitude input pipeline, telegraph, editor"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7b00e1cd-0bda-46ff-924c-8f7c4f997a9e
---

# Point-target cast mode (`CastMode.TargetPoint`)

Built 2026-06. A third `CastMode` (after `Instant=0`, `Aimed=1`) → **`TargetPoint=2`**. The joystick's
**direction AND distance-from-center** resolve a world point; the cast **originates at that point**,
not the caster. The Mage's manual ability **Blast** uses it (projectile spawns at the point and
detonates there — no travel from the player).

Target resolution (server): `target = caster + dir(yaw) × clamp01(magnitude) × maxDist`, where
**`maxDist` = `TelegraphConfig.ForwardOffset`** — the single source of truth shared by the client
telegraph preview AND the server. In directional modes `ForwardOffset` is a fixed offset; in
TargetPoint it's the MAX reach (magnitude scales 0..max).

## Joystick magnitude pipeline (was discarded before this)
- **`AbilityButton.cs`** — already computed `norm = clamped.magnitude/_radius`; now stored as
  `CurrentAimMagnitude` (live, during drag) + `AimMagnitude` (committed on release).
- **`IAbilityInput`** — `AimMagnitude` + `LiveAimMagnitude` (0..1). `AbilityInputProvider` exposes
  both; `SetAim(aimed, yaw, magnitude)`. Desktop maps mouse offset → 0..1 over ~40% screen height.
- **`InputCommand.AimDistanceQ`** (Key 6) — **repurposed the previously-unused `AimPitchQ`** slot;
  quantized via `MovementModel.EncodeAxis`/`DecodeAxis` (AxisScale 32767). `LocalInputPublisher`
  sends it; `AetherServerSimulation.ApplyInput` decodes and passes to `AbilityExecutor.ProcessInput`.

## Server (`AbilityExecutor`)
- `ProcessInput(playerId, buttonMask, aimYaw, aimMagnitude, world, health, tick)` — for a
  `TargetPoint` slot calls `ActivateTargetPoint`:
  - **Tap (AutoAimFlag set / no drag): nearest enemy's POSITION** (`FindNearestEnemyPoint`, mirrors
    `FindNearestEnemyYaw`); **if none → `caster + facing(playerYaw) × maxDist`** (forward fallback).
  - Drag: `caster + dir(aimYaw) × clamp01(magnitude) × maxDist`.
- `ActiveAbility` carries `HasTargetPoint` + `TargetX/TargetZ`. `EvaluateHitbox` uses the target as
  origin (ignores `OffsetForward`); `EvaluateProjectile` spawns at the target with
  **`ProjectileSimulation.Spawn(detonateAtOrigin: true)`** (see [[projectile-system]]).
- `ability_cast` payload includes `targetX/targetZ` when `HasTargetPoint`.

## Client visuals
- **`TelegraphController`** stores `_primaryCastMode` (from `AbilityClientInfo.CastMode`). For a
  `TargetCircle` primary: `offset = (CastMode==TargetPoint ? LiveAimMagnitude : 1) × ForwardOffset`
  → the circle slides nearer/farther with the stick.
- **`AbilityVisualHandler.HandleCast`** — for TargetPoint abilities, **skips all caster-side
  flash/VFX** (user wants nothing at the player); the projectile spawn→explosion at the target
  carries the visual. Cast sound still plays.

## Editor (Ability Editor) — also FIXED a latent drop bug
`RootNode` now exposes **Trigger Mode / Cast Mode / Auto Range** Enum/Float fields, and
`AbilityGraphSerializer` round-trips them. **Before this, the editor silently dropped
`TriggerMode`/`CastMode`/`AutoRange` on save** (only Id/DisplayName/Cooldown/Button/Telegraph/Root/
VisualGuid were written) — re-saving any ability through the tool zeroed those enums. See
[[feedback-ability-editor-sync]]. The `TelegraphForwardOffsetField` doubles as the TargetPoint max
distance (tooltip notes this).

## Tuning (Mage Blast)
`ability_mage_blast.json`: `CastMode: "TargetPoint"`, Projectile root (AoE r2.5 / 18), Telegraph
`TargetCircle` r2.5 `ForwardOffset 10` (= max target distance). Mirror in `AbilitiesConfig.Default`.
