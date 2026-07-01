---
name: boot-flow
description: "Client boot sequence, scene loading, environment picker, reconnect/disconnect handling, and pause reset"
metadata: 
  node_type: memory
  type: project
  originSessionId: 345c10fe-3909-4f06-ac80-7e8543804aea
---

## Boot Flow Architecture
- **UniTask + VContainer** are core client frameworks (async + DI)
- **Scene loading**: `ISceneLoader` / `UniTaskSceneLoader` — additive load/unload via UniTask
- **Loading screen**: `LoadingScreenPresenter` in `PersistentUI` scene (Core/UI) — not DI-registered, found via `FindAnyObjectByType` after scene load
- **Lobby**: child scope of AppStarter via `LifetimeScope.EnqueueParent`. `LobbyLifetimeScope` must exist as a root GameObject in `Lobby.unity` (Transform + `autoRun:1`) — without it, `LobbyEntryPoint` is never created and play button does nothing. See [lobby-ui.md](lobby-ui.md).
- **Environment picker**: prefab-based TMP UI loaded via `Resources.Load`, `#if CLASHUP_DEV || UNITY_EDITOR`
- **Environments**: Local (`localhost:5001`), Tailscale (`100.68.118.109:5001`), Dev (remote). Tailscale for phone→local-server testing. Emulator uses `adb reverse` + Local.
- **Critical**: Server ping must block & retry — never proceed to lobby on failure
- **Boot sequence**: load PersistentUI → show loading → env picker (dev) → identity → ping → load lobby → hide loading
- **Active scene**: `GameFlowController` calls `SceneManager.SetActiveScene()` after every additive load — ensures `new GameObject()` / `Instantiate()` go into the correct scene (not AppStarter). See [scene-ownership.md](scene-ownership.md).
- **Game flow**: Lobby → (Play) → Matchmaking scene → (matched) → Match scene → (end) → Lobby
- **Reconnect flow**: Lobby checks for active match on startup → if found, skip lobby UI → go straight to Match
- **Disconnect handling**: Server marks player disconnected (not removed), client can rejoin same match. `MatchHub.JoinAsync` replays `OnMatchEnded` to late-joining clients. See [debugging.md](debugging.md) for the full race-condition fix sequence.
- **Pause handling**: `SessionResetHandler` (AppStarter, DontDestroyOnLoad) shows popup on app unpause → user confirms → full boot reset.
- **Client is dumb**: NEVER synthesize match-end on the client. Server always delivers `OnMatchEnded`. See [feedback-client-authority.md](feedback-client-authority.md).
- **Near-end guard**: `CheckActiveMatchAsync` rejects reconnects to matches with <10s remaining — marks Ended, returns Queued.
- **Reconnect loop guard**: `LobbyEntryPoint` limits reconnect attempts (static counter, max 3). Reset on successful match join.
