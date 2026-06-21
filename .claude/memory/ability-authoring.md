---
name: ability-authoring
description: "How to create, edit, and deploy ability JSON files — editor tool, file locations, JSON schema, enum values, and wiring to characters"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 7a83d40d-6bd9-4d34-a21b-138fb82f5d78
---

## Creating Abilities

### Preferred: Unity Editor Tool

Open **Tools → Ability Editor** in Unity. It provides a node graph with live validation and serializes to JSON automatically.

1. Click **New Ability** (or Load an existing one)
2. Fill in the Root node: ID (snake_case), Display Name, Cooldown (seconds), Button (0–3)
3. Configure Telegraph (see below)
4. Connect an `AbilityVisualConfig` asset in the **Visual Config** field on the Root node (optional — see Visual Config below)
5. Right-click the graph to add nodes; connect Root **Out** → first node, then chain via each node's **Next** port
6. Click **Save JSON** — save to BOTH locations (see File Locations below)

The editor reads/writes seconds; tick conversion (×30) is handled automatically on save/load.

---

## File Locations

Ability JSON lives in **ONE** place — the server:

| Location | Purpose |
|---|---|
| `src/Server/ClashUp.GameServer/Abilities/Data/ability_<id>.json` | Server runtime (`ServerAbilityStore` globs `ability_*.json`) |

There is **NO** client ability-JSON folder. The client receives ability config over the wire as `AbilitiesConfig` (`JoinResult` Key 9 / `MatchProvision` Key 8), built by `ServerAbilityStore.BuildClientConfig()`. The csproj already copies `Abilities/Data/ability_*.json` to output — new files are picked up with no csproj change.

---

## JSON Schema

All enum values are **strings** (not integers). Null/missing fields are omitted.

### Root

```json
{
  "Id": { "Value": "ability_id" },
  "DisplayName": "Human Name",
  "CooldownTicks": 30,
  "ButtonIndex": 0,
  "Telegraph": { ... },
  "RootNode": { ... }
}
```

- `CooldownTicks`: integer ticks (30 = 1 second at 30 Hz)
- `ButtonIndex`: 0–3, maps to `InputCommand.ButtonMask` bits

### Telegraph

```json
{
  "Shape": "CircleAroundCaster",
  "Radius": 1.5,
  "Length": 3.0,
  "Angle": 45.0,
  "ShowDurationTicks": 0
}
```

**Shape values:**

| Value | UI label | Relevant fields |
|---|---|---|
| `CircleAroundCaster` | Circle / Caster | `Radius` |
| `TargetCircle` | Circle / Target | `Radius`, `ForwardOffset` |
| `ForwardLine` | Line / Caster | `Length`, `Width` |
| `ForwardCone` | Line / Target | `Length`, `Angle` (spread degrees) |
| `Capsule` | (derived from Capsule hitbox cast shape) | `Length`, `Width` |

For Line/Cone shapes the direction always follows the player's aim input (AimYaw) — it is not configurable. `Width` is Key 5; **`ForwardOffset` is Key 6** — slides a `TargetCircle` downrange along aim (e.g. Mage Blast previews where its projectile-AoE lands). The Ability Editor shows the Forward Offset field only for Target-origin shapes.

### Nodes

Sequential chaining uses the `Next` field. Parallel branching uses `Children`.

**Structure:**

```json
{
  "Type": "Hitbox",
  "DelayTicks": 0,
  "Next": { ... },
  "Hitbox": { ... }
}
```

**Type values:** `Hitbox`, `Projectile`, `Parallel`

#### Hitbox node

```json
{
  "Type": "Hitbox",
  "DelayTicks": 6,
  "Hitbox": {
    "Effect": "Damage",
    "Amount": 10.0,
    "Radius": 1.5,
    "OffsetForward": 1.0,
    "DurationTicks": 1,
    "HitIntervalTicks": 0,
    "HitSelf": false,
    "HitAllies": false
  },
  "Next": null
}
```

- `Effect`: `"Damage"` or `"Heal"`
- `OffsetForward`: meters in front of caster (0 = on caster)
- `DurationTicks`: 0 or 1 = instant; >1 = linger (hits every `HitIntervalTicks` ticks)
- `HitIntervalTicks`: 0 = hit once; >0 = re-hit every N ticks while active

#### Projectile node

```json
{
  "Type": "Projectile",
  "DelayTicks": 0,
  "Projectile": {
    "Speed": 11.0,
    "Radius": 0.4,
    "MaxRange": 10.0,
    "MaxPierceCount": 0,
    "OnHitEffect": "Damage",
    "OnHitAmount": 12.0,
    "AoeRadius": 2.5,
    "AoeAmount": 18.0,
    "AoeEffect": "Damage",
    "LifetimeTicks": 0
  },
  "Next": null
}
```

Projectiles are **fully simulated server-side** (see [[projectile-system]]).
- `Speed`: units/second. `MaxRange`: detonates/expires at this distance.
- `MaxPierceCount`: currently unused — projectiles detonate on the first enemy hit.
- `OnHitEffect`/`OnHitAmount`: direct-hit damage on the struck target.
- `AoeRadius`: 0 = single-target bolt; >0 = explode at impact applying `AoeAmount` (`AoeEffect`) to everyone in the circle except the caster and the direct-hit target.
- `LifetimeTicks`: 0 ⇒ auto-derived from `MaxRange`/`Speed`.

#### Parallel node

```json
{
  "Type": "Parallel",
  "DelayTicks": 0,
  "Children": [
    { "Type": "Hitbox", ... },
    { "Type": "Projectile", ... }
  ],
  "Next": null
}
```

`Children` all run simultaneously. `Next` runs after all children finish.

---

## Sequential Chaining Example

Root → Hitbox (instant) → Hitbox (delayed 0.2s):

```json
"RootNode": {
  "Type": "Hitbox",
  "DelayTicks": 0,
  "Hitbox": { "Effect": "Damage", "Amount": 5.0, "Radius": 1.0, "OffsetForward": 0.0, "DurationTicks": 1, "HitIntervalTicks": 0, "HitSelf": false, "HitAllies": false },
  "Next": {
    "Type": "Hitbox",
    "DelayTicks": 6,
    "Hitbox": { "Effect": "Damage", "Amount": 10.0, "Radius": 1.5, "OffsetForward": 1.0, "DurationTicks": 1, "HitIntervalTicks": 0, "HitSelf": false, "HitAllies": false }
  }
}
```

---

## Visual Config

Each ability can have an `AbilityVisualConfig` ScriptableObject that defines VFX prefabs, sounds, and telegraph visuals.

### Creating a Visual Config

1. In Project window: right-click → **Create → ClashUp → Ability Visual Config**
2. Fill in fields: `CastVfxPrefab`, `HitVfxPrefab`, `ProjectilePrefab`, `CastSound`, `HitSound`, `Telegraph` (material + color)
3. In Ability Editor: drag the asset into the **Visual Config** field on the Root node
4. Save JSON — the GUID of the asset is written to `VisualConfigGuid` in the JSON

### Registering in AbilityVisualRegistry

The `AbilityVisualRegistry` SO (`Assets/Core/Match/Content/ScriptableObjects/AbilityVisualRegistry.asset`) connects GUIDs to Unity references for runtime lookup. Lookup is by `GetByAbilityId(id)` at runtime (the GUID is for editor wiring). Per-ability visuals are OPTIONAL — `ProjectileViewSystem`/`AbilityVisualHandler` use coded fallbacks (blue projectile sphere, area-flash explosion, fallback cast-flash color) when no `AbilityVisualConfig` is registered.

1. Open the registry asset in the Inspector
2. Add an entry: set **Config** to the `AbilityVisualConfig` asset, set **Ability Id** to match the ability's ID (e.g. `brawler_punch`)
3. Click **Refresh GUIDs from References** — fills the Guid string automatically
4. Save

At runtime, `AbilityVisualHandler` calls `registry.GetByAbilityId(abilityId)` on `ability_cast` events.

---

## Wiring an Ability to a Character

`CharacterRegistry` is DELETED. A character references abilities via `CharacterDefinition.AutoAttackId` (auto) + `ActiveAbilityId` (manual). Set these in TWO places (keep in sync):

1. `CharactersConfig.Default` in `src/Shared/ClashUp.Shared/MessagePackObjects/CharactersConfig.cs` (the compiled-in fallback / client roster).
2. The `characters:registry` seed JSON in `src/Server/ClashUp.Services/Persistence/ConfigSeeder.cs` (the DB source of truth; only seeds when the key is MISSING — drop the doc to reseed an existing dev DB).

```csharp
new CharacterDefinition {
  Id = new CharacterId("mage"), DisplayName = "Mage",
  BaseStats = new StatBlock { MaxHealth = 70f, Damage = 8f, MoveSpeed = 4.5f },
  AutoAttackId = new AbilityId("mage_bolt"),
  ActiveAbilityId = new AbilityId("mage_blast"),
}
```

`AetherServerSimulation.EnsurePlayer` → `AbilityExecutor.InitPlayer(autoAttackId, activeAbilityId)` on first spawn. `StatBlock.Damage` is NOT used by damage math — damage comes from ability `Amount`/`OnHitAmount`/`AoeAmount`. For a new client body, add an `id→prefab` entry to `CharacterPrefabMap`. See [[character-selection]].

---

## Tick Rate Reference

All time values in JSON use **ticks** (30 Hz):

| Seconds | Ticks |
|---|---|
| 0.033s | 1 |
| 0.1s | 3 |
| 0.2s | 6 |
| 0.5s | 15 |
| 1.0s | 30 |
| 2.0s | 60 |
