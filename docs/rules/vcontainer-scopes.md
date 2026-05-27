# VContainer LifetimeScopes

**When this applies:** Adding or restructuring any `LifetimeScope` on the Unity client.

## The rule

Two independent root LifetimeScopes — neither is a child of the other.

- **`AppStarterLifetimeScope`** — boot-scene root. Lives for the whole app session. Owns: device-ID store, Services channel, auth client, app settings, top-level UI navigation.
- **`CoreStarterLifetimeScope`** — gameplay root. Instantiated when the user enters the gameplay flow (e.g. presses Play). Owns: matchmaking client, per-GS channel lifecycle, match-session factory.

Gameplay child scopes parent under **`CoreStarter`**, not under `AppStarter`:

- `MatchLifetimeScope` — child of `CoreStarterLifetimeScope`. Created on match join, disposed on match end.
- Future per-mode scopes (e.g. `TutorialLifetimeScope`) — also children of `CoreStarter`.

## Why two independent roots

`AppStarter` is the long-lived boot/identity context: it survives every match. `CoreStarter` is the gameplay-session context: it is created and torn down around the gameplay loop without affecting boot-level services.

Keeping them independent (rather than `CoreStarter ⊂ AppStarter`) means:

- Gameplay-tier services can be wiped cleanly without touching auth/network/settings.
- Memory and channel lifetimes for the GS connection are scoped to the gameplay session, not the whole app.
- The boundary between "user is in the menu" and "user is in gameplay" is explicit in the scope graph.

## Cross-root handoff

When `AppStarter` needs to hand data to `CoreStarter` (e.g. the cached JWT, the player profile), do it via plain DTOs or a small handoff service registered in both roots — **not** via scope inheritance.

Concretely: define a `SessionHandoff` value type in `Assets/Core/Scripts/`, populate it in `AppStarter`, pass it as an argument when instantiating `CoreStarter`.

## Adding a new scope

1. Decide which root it lives under (`AppStarter` for app-lifetime concerns, `CoreStarter` for gameplay-lifetime concerns).
2. Update this doc and [`README.md`](README.md)'s index entry.
3. Place the scope class in the owning domain folder under `Scripts/`. See [`unity-folder-structure.md`](unity-folder-structure.md).

## Boot sequencing

Use VContainer's `IAsyncStartable` (UniTask-based) for boot work. See [`async-discipline.md`](async-discipline.md) for `UniTask` rules.
