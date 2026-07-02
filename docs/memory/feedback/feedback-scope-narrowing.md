---
name: feedback-scope-narrowing
description: User prefers minimal stat/field definitions — ask before designing broad APIs
metadata: 
  node_type: memory
  type: feedback
  originSessionId: ed44a8ee-8136-4a5b-85f8-0d83e840934d
---

When designing new systems (stats, configs, DTOs), start with the minimum fields the user mentions rather than anticipating future needs.

**Why:** During stat system design, I proposed 5 stat fields (MaxHealth, MoveSpeed, AttackDamage, AttackRange, AttackCooldown). User narrowed to just HP and Damage. The extra fields were wasted planning effort.

**How to apply:** When the user describes a system, ask about scope before designing if there's ambiguity about how many fields/features to include. Don't add speculative fields — they can be added later. Related: user also chose "infrastructure only" over "full combat" — build plumbing first, mechanics second.
