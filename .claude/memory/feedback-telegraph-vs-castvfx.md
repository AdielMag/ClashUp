---
name: ""
metadata: 
  node_type: memory
  originSessionId: 33cbf27a-951e-448a-8363-4a9134277285
---

In ClashUp, **"telegraph" and "the visual when the ability triggers" are two different systems** — don't conflate them.

- **Telegraph** = the *persistent* range/aim indicator drawn on the ground while idle/aiming (`TelegraphController` + `TelegraphRenderer`). It only communicates **range/direction**.
- **Cast visual / VFX / flash** = the *triggered* effect that fires on cast (`ability_cast` event → `AbilityVisualHandler` → `AbilityAreaFlash` / `CastVfxPrefab`). This is what shows the **area of damage** at the moment of the hit.

**What happened:** User asked to "change the default ability range and make the visual telegraph represent that" + "add a visual to the auto attack… capsule showing area of damage." I planned (and nearly exited plan mode) treating the telegraph itself as the area-of-damage visual. User corrected: *"the telegraph just shows the range, I was talking about visuals when the ability gets triggered!"* — forcing a full plan rewrite toward the cast-VFX (`AbilityAreaFlash`).

**Why:** The word "telegraph" is overloaded. The user uses it strictly for the range indicator; "visual" / "VFX" for the triggered effect.

**How to apply:** When a request mentions an ability "visual," decide up front whether it's the telegraph (range) or the cast VFX (triggered area), and **ask if ambiguous before planning**. Cue words: "shows the range" → telegraph; "when it triggers / on hit / area of damage / capsule/cone effect" → cast VFX (`AbilityAreaFlash`, derived from the hitbox via `CastShape`). See [[ability-authoring]].
