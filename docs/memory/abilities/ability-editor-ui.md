---
name: ability-editor-ui
description: Ability Editor (UIToolkit GraphView) visual restyle + how to add a new node type
metadata: 
  node_type: memory
  type: project
  originSessionId: e8a24dc9-d555-4ccd-bf6d-a1f59c6f1e14
---

The Ability Editor (`Tools → Ability Editor`) was restyled (USS-driven) in the
`ClashUp.Gameplay.Editor` asmdef (NOT `ClashUp.AbilityEditor` — that name in old
notes is wrong). Files under
`client/ClashUp.Unity/Assets/Core/Gameplay/Scripts/Editor/AbilityEditor/`:

- `AbilityEditor.uss` — single stylesheet, all design tokens + structural styling
  (loaded by the window onto `rootVisualElement`). NOTE UI Toolkit has no
  gradients / box-shadow / numeric font-weight — those design specs are
  approximated (solid mid-colour, omitted shadow, `-unity-font-style: bold`).
- `NodeVisuals.cs` — **the extensibility seam.** `NodeCategory` enum + three
  `Dictionary<NodeCategory,…>` lookups (AccentColors, Shapes, Tags) + `NodeIcon`
  (draws shape via Painter2D). Categories: Root(blue/circle), Action(green/
  triangle), Flow(purple/diamond), Hitbox(red/square), Spawn(teal/plus).
- `Nodes/AbilityGraphNode.cs` — base: takes `(title, NodeCategory)`, applies
  accent stripe + icon chip + category tag + `StylePort()` (sets `port.portColor`
  = accent so edges/dots inherit colour). Concrete nodes pass only their category.
- `DottedGridBackground.cs`, `ZoomControlPill.cs` — custom canvas chrome.
- `AbilityGraphView.cs` — dotted grid, MiniMap (bottom-right), zoom pill
  (bottom-left), node factory + context menu.
- `AbilityGraphEditorWindow.cs` — 34px titlebar + `Toolbar` (3 zones: New/Browse/
  Load/Save · identity chip · search + Add Node menu).

**To add a node type:** (1) add a `NodeCategory` value, (2) add its colour/shape/
tag to the 3 dicts in NodeVisuals, (3) make a `XNode : AbilityGraphNode` passing
that category, (4) register in `AbilityGraphView.BuildContextMenu` +
`AbilityGraphEditorWindow.ShowAddNodeMenu`. No new USS. It auto-inherits all
styling (verified with the Spawn node).

**Gotchas learned:**
- New nodes spawn at the cursor via `AbilityGraphView.LastMousePosition`, tracked
  from `MouseMove`/`MouseDown` `localMousePosition` + `ChangeCoordinatesTo(contentViewContainer,…)`.
  Do NOT use `contentViewContainer.WorldToLocal(evt.mousePosition)` — it's wrong
  when the GraphView isn't the whole window (nodes land far off-screen).
- An `anchored` `MiniMap` ignores `style.left/top`; position it with `SetPosition`.
  Put the custom "MINIMAP" header as a child of the minimap so it tracks + covers
  the built-in title.
- Node field input boxes: set bg on `.unity-base-field__input`; only zero-bg the
  inner `.unity-base-text-field__input > .unity-text-element`, never the input box
  itself (that hides the field background).
- Toolbar ghost buttons must be composite `VisualElement`s (icon + Label), NOT
  `Button`s — a Button is a TextElement and its child icon overlaps the text.
- **Field hover tooltips**: set `field.tooltip` at creation (covers the input
  area). But a `BaseField` label has a built-in *truncation* tooltip (shows the
  full label text) that shadows yours on the label. Fix once in the base node via
  `RegisterCallback<AttachToPanelEvent>` → query all `.unity-base-field`, copy
  each field's `tooltip` onto its `.unity-base-field__label` child. Auto-applies to
  every node type; new types just set `tooltip` on their fields.

**Spawn caveat:** `SpawnNode` is currently a VISUAL placeholder — the shared data
model (`AbilityNodeType` in ClashUp.Shared) has no Spawn entry, so the serializer
ignores it (`BuildChain` `_ => null`). Wire `AbilityNode` + `AbilityGraphSerializer`
when runtime Spawn support lands. See [[ability-authoring]].
