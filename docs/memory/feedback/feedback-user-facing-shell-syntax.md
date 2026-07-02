---
name: feedback-user-facing-shell-syntax
description: "When giving the user copy-paste commands to run in THEIR OWN terminal, use PowerShell syntax, not bash — their attached terminal is PowerShell even though I use the Bash tool"
metadata:
  node_type: memory
  type: feedback
  originSessionId: aa814896-3a5c-42c2-97d0-b647923fe9ca
---

Gave the user `cd ops/terraform && terraform output -raw fleet_resolve_key` to run themselves, copied straight from my own Bash-tool habits. Their attached terminal is Windows PowerShell, which doesn't support `&&` as a statement separator (`InvalidEndOfLine` parser error) — they hit the error and had to paste it back to me.

**Why:** I have two different shells to think about: the Bash tool I invoke myself (POSIX/git-bash), and the user's own terminal (PowerShell on this project — see the environment header). Muscle memory defaults to bash syntax for any command I type, including ones meant for the user to paste, not me to run.

**How to apply:** Whenever a message includes a command block the user is meant to run themselves (not something I execute via a tool), write it in PowerShell syntax — separate statements on their own lines, or `;` to chain, never `&&`/`||`. Reserve bash chaining (`&&`, `export VAR=x`, etc.) for commands going through my own Bash tool calls.
