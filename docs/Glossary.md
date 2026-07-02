# Glossary

Domain terms for ClashUp. When a term below is ambiguous in a request, prefer this definition (and ask
if still unclear).

## Netcode
- **Tick** — one fixed server simulation step. The server advances state deterministically per tick.
- **Snapshot** — authoritative world state the server broadcasts to clients each tick. See [[netcode-architecture]].
- **Client prediction** — the client applies the local player's input immediately (before server
  confirmation) to hide latency.
- **Server reconciliation** — on receiving a snapshot, the client rewinds the local player to the
  authoritative state and replays unacknowledged inputs.
- **Remote interpolation** — remote entities are buffered and interpolated between snapshots for smooth
  motion (they are NOT predicted).

## Abilities / combat
- **Telegraph** — a *persistent range/aim indicator* shown while aiming an ability (where it *would*
  land). NOT a damage effect. See [[feedback-telegraph-vs-castvfx]].
- **Cast VFX** — the *triggered* visual effect for an ability's actual area-of-damage when it fires.
  Distinct from the telegraph — do not conflate the two.
- **Ability node / executor** — abilities are authored as nodes; the executor runs them server-side. See
  [[ability-system-core]].

## Modes / gameplay
- **TDM** — Timed Team Deathmatch (2 teams, timed, kills = points, respawns).
- **Elimination** — no-respawn mode with a points economy and breakable boxes/orbs. See [[elimination-mode]].
- **Bot fill** — matchmaking provisions server-side AI bots when there aren't enough humans. See [[bot-system]].
- **Forfeit / Leave Match** — mid-match exit flow (client + server). See [[forfeit-leave-match]].
- **Grass stealth zone** — grass tiles hide occupants from other viewers via per-viewer snapshot
  filtering. See [[grass-stealth]].

## Server / infra
- **MatchHub** — the MagicOnion `StreamingHubBase` for a live match. See [[magiconion-hub-discipline]].
- **Session cache** — planned in-memory per-player cache loaded from Mongo on connect. See [[mvp1-architecture]].
- **Write-behind** — planned persistence strategy: buffer mutations in memory, flush to Mongo with a
  zero-data-loss mandate. See [[mvp1-architecture]].
- **Version-aware gateway** — routes clients to the backend matching their app version. See [[deployment-architecture]].
