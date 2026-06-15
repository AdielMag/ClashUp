---
name: feedback-ui-scope
description: "When user says \"hide UI\", distinguish between input UI (joysticks, buttons) and informational HUD (timer, status labels) — don't over-scope"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 46aebde2-c727-48ce-910a-d9c4ae4088ed
---

When the user says "hide all other UI input" during gameplay, they mean the other *input controls* (joystick canvas, ability button canvas) — NOT the informational HUD elements (timer, status label, player count).

**Why:** Input controls are the ones that visually clutter while actively using another control. Informational HUD (timer, match status) should always remain visible because the player needs that info regardless of what they're touching.

**How to apply:** When asked to hide UI on input interaction, only toggle visibility of input-related canvases (JoystickCanvas, AbilityCanvas). Leave MatchUI HUD elements untouched. Ask for clarification if the scope is ambiguous between "input UI" and "all UI".
