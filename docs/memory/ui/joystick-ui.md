---
name: joystick-ui
description: "In-match UI Toolkit joystick architecture — shared circular touch-zone routing, floating re-anchor, cancel bar, cooldown badges"
metadata: 
  node_type: memory
  type: project
  originSessionId: 345c10fe-3909-4f06-ac80-7e8543804aea
---

The movement + ability joysticks (`InGameHUD.uxml`/`.uss`, [[ui-toolkit-migration]] Phase 4) went through several redesigns in one session (2026-07-01). This documents the **current** architecture — treat any earlier description as superseded.

## Files
- [MatchHudController.cs](../../client/ClashUp.Unity/Assets/Core/Gameplay/Scripts/Input/MatchHudController.cs) — builds `InGameHUD.uxml`, owns both joystick instances, wires cross-references between them
- [UiJoystick.cs](../../client/ClashUp.Unity/Assets/Core/Gameplay/Scripts/Input/UiJoystick.cs) — movement
- [UiAbilityJoystick.cs](../../client/ClashUp.Unity/Assets/Core/Gameplay/Scripts/Input/UiAbilityJoystick.cs) — ability aim
- `Assets/Resources/UI/InGameHUD.uxml` + `.uss`

## Shared touch region, split by a circle (not a rectangle)
Both joysticks register on the **same** `ControlArea` VisualElement instead of separate left/right zones (the old 60/40% rectangular split was abandoned — see "why" below). Routing is a distance check in C#, not element bounds:
- `UiJoystick.Contains(pos)` — true if `pos` is within `MoveTouchRadius` (260px, `.move-touch-zone` CSS circle) of the movement ring's rest center (`_base.layout.center`).
- `UiAbilityJoystick` takes a `Func<Vector2,bool> isInMovementZone` callback (wired to `Movement.Contains` in `MatchHudController.EnsureBuilt`, so `Movement` must be constructed before `Ability`). Each `OnDown` checks the other's territory and returns early if the touch isn't theirs.
- Movement ring sits at `left:50%` (center) of the zone; ability ring's rest position is offset `+340px right, +200px down` via `margin-left`/`margin-bottom` overrides on `.joystick-outer--ability` (not literal CSS anchoring to a sibling — Yoga/USS has no relative-to-sibling positioning, so the offset is hand-computed from both rings' shared `left:50%` baseline).
- A visual-only `.move-touch-zone` circle (radius must match `MoveTouchRadius` in code) and `.control-area` background fill communicate the split to the player; they're purely decorative, not part of hit-testing.

## Floating re-anchor: clamp-to-zone-bounds, NOT distance-threshold
**Bug fixed this session**: the first version only floated the ring to the touch if the press landed within `radius * 1.5` of the ring's rest position. Since the touch zone is much bigger than the ring, most natural taps fell *outside* that threshold — the ring almost never moved, and any such tap became an instant max-deflection input from a base that never budged. Felt completely broken.

**Fix**: `ClampToArea(press, fallback)` in both classes — always floats the ring to the touch position, clamped only so the ring stays fully inside its zone bounds (`_area.layout`, inset by the ring's own radius). No arbitrary "closeness" gate. If you ever reintroduce a floating-joystick threshold check, verify the threshold actually covers a meaningful fraction of the real touch zone — don't just pick a multiplier of the ring radius without checking it against the zone's actual pixel size.

## Cancel bar (ability only)
- NOT on the joystick itself (moved off it per explicit user feedback) — `AbilityCancelBar` is a fixed **rectangle** at bottom-center of the screen (`bottom: 50px`, not attached to the joystick's position), shown only while dragging the ability joystick.
- Hit-tested via panel-space coordinates: `_cancelBar.worldBound.Contains(e.position)` (NOT `e.localPosition`, which is relative to whatever `currentTarget` is — cross-element hit tests need panel-space `.position` + `.worldBound`).
- Releasing over it aborts the cast entirely (`IsOverCancelZone` check in `OnUp`) — distinct from the existing `AimDeadZone` (0.25 magnitude → auto-aim instead of directional aim).
- `IAbilityInput.IsCanceling` surfaces this to `TelegraphController`, which hides the primary telegraph preview while canceling (extra visual feedback, on top of the arrow tinting red and the bar itself brightening via an `--active` USS class).

## Cooldown mini badge — must check IsOnCooldown, not just visibility
The compact `AbilityCooldownMini` badge fills in for the cooldown readout while the main ability ring is hidden (declutter during a movement drag). **Bug fixed**: it initially showed whenever the ring was hidden, regardless of cooldown state. Fix: gate on BOTH `!abilityBaseVisible && Ability.IsOnCooldown`, re-evaluated every `Tick()` (not just on the visibility-toggle callback) since cooldown expiry happens mid-frame.

## Real bug: shared VisualElement + PointerCaptureOutEvent needs pointerId filter
Once both joysticks captured pointers on the SAME `ControlArea` element (needed for the circular-zone routing to work), a latent bug appeared: `PointerCaptureOutEvent` fires on **every** callback registered for that event type on the shared element, regardless of which `pointerId` actually lost capture. The original handlers ignored the event payload (`_ => Reset()` / `_ => Cancel()`), so one joystick losing capture would incorrectly reset/cancel the OTHER joystick's unrelated active drag. **Fix**: `e => { if (e.pointerId == _pointerId) Reset(); }` in both classes. This only became a bug because of the shared-element redesign — when each joystick had its own dedicated area, there was no cross-talk. **Rule**: any time two independent input handlers share one VisualElement for pointer capture, audit every event callback registered on it for missing `pointerId` filtering.

## USS: combining `%` position with `px` margin for a fixed offset
Unity USS has no `calc()`. But `bottom: 9%; margin-bottom: 200px;` on the same absolutely-positioned element DOES compose correctly (Yoga adds the margin to the percentage-based offset) — this is how "9% from the bottom, plus a further fixed 200px lift" was achieved without hardcoding the 9% as a pixel value. Same trick used for `left: 50%; margin-left: -150px;` (which was already in use for centering, i.e. this composition behavior was implicitly relied on before it was made explicit for the "move up 200px" request).

## Not device-tested
None of the joystick touch-routing/floating/cancel-bar behavior in this session was verified on an actual phone (Unity MCP `ai-game-developer` can't drive Play Mode multi-touch). All fixes were reasoned from code + Unity UI Toolkit event-model knowledge. Flag this explicitly to the user rather than claiming verified success — this session did so consistently and it was the right call (the user's own device testing is what surfaced the "reanchor doesn't work well" bug in the first place).
