---
name: feedback-reread-before-edit
description: Always re-read files immediately before editing — code evolves between plan and implementation
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9c39a0b4-9bf3-4ac5-80ce-770b10ebcc95
---

Re-read files immediately before editing, especially if time passed since the initial read (e.g., during planning phase).

**Why:** In this session, `PlayerViewSystem.cs` and `MatchLifetimeScope.cs` had evolved between planning exploration and implementation — they gained `RemotePlayerInterpolator` and `ClientPredictionWorld` dependencies. The Edit tool rejected changes because file contents didn't match. Had to re-read and adapt the edits to the current code.

**How to apply:** When transitioning from plan to implementation, re-read every file you intend to edit before the first edit. Don't trust the content from the exploration phase — the codebase may have been modified between sessions or by other tools. This is especially important after `assets-refresh` triggers Unity compilation, which can auto-format or update files.
