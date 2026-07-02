# 🏠 ClashUp Vault Home

Landing page for the ClashUp docs vault. (Tip: install the **Homepage** community plugin to open this
note on startup.)

## Start here
- [[GDD]] — Game Design Document
- [[Glossary]] — domain terms (read this if a term is ambiguous)
- [[MEMORY]] — Claude's auto-memory index (git-tracked, in `memory/`)

## Rules (also auto-loaded by Claude when editing matching code)
- [[server-authority]] · [[shared-contracts]] · [[magiconion-hub-discipline]] · [[mongo-data]] · [[jwt-auth]]
- [[async-discipline]] · [[il2cpp-aot]] · [[naming-conventions]] · [[unity-folder-structure]] · [[vcontainer-scopes]]

## System diagrams (Canvas — editable)
- [[netcode-flow.canvas|Netcode flow]] — prediction / reconciliation / interpolation
- [[boot-flow.canvas|Boot flow]] — app startup → lobby → match
- [[match-lifecycle.canvas|Match lifecycle]] — matchmaking → play → results

## Memory browser
All memory notes grouped by category:

![[Memory.base]]

## Key facts
- Unity client + C# server (ASP.NET Core 8 + MagicOnion 7.10.0), deterministic AetherNet physics.
- Server-authoritative, dumb client. See [[MEMORY]] and the rules above.
