# MagicOnion Hub Discipline

**When this applies:** Any `StreamingHubBase` on the server, primarily `MatchHub`.

## The rule

A hub method does three things, in order:

1. Validate the incoming call (JWT claims, rate limits, sanity bounds on payload).
2. Enqueue work into the per-match buffer (`InputBuffer`, `MatchContext` mutation queue).
3. Return immediately.

That's it. The match's tick loop drives **all** broadcasts via `Group.All.OnSnapshot(...)` (or per-client receivers for personalised state). Hubs never:

- Run AetherNet sim steps inside a hub method.
- Issue broadcasts from inside a hub method.
- Block on I/O (Mongo, external HTTP) inside a hub method.

Hub methods are O(small) and return in microseconds-to-milliseconds. Anything heavier moves to the tick loop, to a background service, or to an `IHostedService`.

## Why

Every hub method runs on a thread that is also driving message dispatch for that client. Blocking it stalls that client's outbound stream and starves the StreamingHub group. Worse, doing sim work in hubs serialises authoritative state changes through a request thread, breaking the deterministic-tick model the AetherNet simulation depends on.

## Reconnect / connect lifecycle

- JWT validation happens in `OnConnecting`. See [`jwt-auth.md`](jwt-auth.md) for claim names.
- `Group.AddAsync(matchId, playerData)` happens in `OnConnecting`. Use the `MatchId.ToString()` as the group key; one group per match.
- `OnDisconnected` decrements presence but does not end the match. Sticky-reconnect semantics let the player rejoin the same `MatchContext`.
- Business RPCs (`JoinAsync`, `LeaveAsync`, `SubmitInputAsync`) MUST NOT do reconnect logic — that's already done in `OnConnecting`.

## Groups

One MagicOnion `Group` per `MatchId`. The hub instance pulls the `MatchContext` from `IMatchRegistry`. The Group doubles as broadcast target and per-member state holder — use it that way; don't maintain a parallel dictionary of connected players.

## Inputs

`SubmitInputAsync(InputCommand)` is the high-frequency path. It:

- Validates the JWT claim against the hub's expected `matchId`.
- Drops the command on the floor if outside the legal tick window.
- Enqueues into the per-match `InputBuffer` (a lock-free / bounded ring).
- Returns immediately. Acks happen implicitly via the next snapshot.

No `await` on anything but the buffer enqueue. No allocation in the steady state — pool the command objects if profiling shows it's worth it.
