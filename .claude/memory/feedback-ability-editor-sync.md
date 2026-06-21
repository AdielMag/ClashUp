---
name: feedback-ability-editor-sync
description: "When editing the ability system data model, also update the Ability Editor tool to match"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 7b00e1cd-0bda-46ff-924c-8f7c4f997a9e
---

When changing the ability **data model** (`ProjectileConfig`, `HitboxConfig`, `TelegraphConfig`, `AbilityDefinition`, `AbilityNode`, or node types in `src/Shared/ClashUp.Shared/Abilities/`), you MUST also update the UIToolkit **Ability Editor** tool so new fields are authorable and round-trip losslessly.

**Why:** The editor serializes/deserializes ability JSON. If a new field isn't added to the editor, editing any ability through the tool silently drops that field on save. The user flagged this explicitly ("if you're editing stuff to the ability system make sure to update the editor tool as well!!").

**How to apply:** Editor lives at `client/ClashUp.Unity/Assets/Core/Gameplay/Scripts/Editor/AbilityEditor/`. For each new field touch all three: (1) the node UI class (`Nodes/ProjectileNode.cs`, `HitboxNode.cs`, or `RootNode.cs` for telegraph) — add the `FloatField`/`EnumField`/etc. and append to `extensionContainer`; (2) `AbilityGraphSerializer.SerializeGraph`/`BuildHitbox`/`BuildProjectile` — write the field; (3) `AbilityGraphSerializer.DeserializeToGraph`/`PlaceNode` — read it back. Note: the editor already omits `TelegraphConfig.Width` (pre-existing gap). See [[ability-authoring]].
