# Scene & Domain Ownership Rules

## Principle: Assets Live Where Their Lifespan Is

Place assets (scenes, prefabs, scripts) under the domain that matches their lifetime, not under whoever first uses them.

## Lifespan Tiers

| Tier | Lifetime | Location | Examples |
|------|----------|----------|----------|
| **App-wide** | Entire application | `Core/` (own additive scene) | Loading screen, persistent UI, audio manager |
| **Boot-only** | Boot sequence, then done | `_Bootstrap/AppStarter/` | BootBootstrapper, environment picker |
| **Session** | One gameplay session | `Core/CoreStarter/` | Gameplay HUD, session services |
| **Mode** | One game mode/screen | `Core/{Mode}/` (e.g. Lobby, Match) | Lobby UI, match scope |

## Why This Matters

- **Boot scope (AppStarter)** owns things needed only during boot. It should NOT own things used across the game — even if boot is the first consumer.
- **Core/** owns cross-cutting concerns that outlive any single scope.
- A loading screen is used during boot, lobby transitions, match loading, etc. → it's app-wide → it lives in `Core/UI/` with its own `PersistentUI` scene.

## Scene Architecture

| Scene | Domain | Build Index | Loaded By | Unloaded |
|-------|--------|-------------|-----------|----------|
| `AppStarter` | Boot | 0 | Build Settings (scene 0) | Never (root scope) |
| `CoreStarter` | Core/CoreStarter | 1 | BootBootstrapper (additive) | Never |
| `PersistentUI` | Core/UI | 2 | BootBootstrapper (additive) | Never |
| `Lobby` | Core/Lobby | 3 | GameFlowController (additive) | When entering matchmaking |
| `Match` | Core/Match | 4 | GameFlowController (additive) | When match ends |
| `Matchmaking` | Core/Matchmaking | 5 | GameFlowController (additive) | When match found or cancelled |

## Rule of Thumb

> If more than one scope/feature will ever use it, it doesn't belong to any single scope — it belongs to Core.
