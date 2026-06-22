---
name: character-selection
description: "Pre-matchmaking character picker: CharacterSelectUI, SelectedCharacterStore, MatchJoinRequest.CharacterId, server-side validation"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7b00e1cd-0bda-46ff-924c-8f7c4f997a9e
---

# Character Selection

Built 2026-06. Previously every player was hardcoded to the default character in `MatchHub.JoinAsync`; the lobby `CharacterPage` was a placeholder. Now there's a generic pre-matchmaking picker (used across all game modes for now).

## Flow
`LobbyEntryPoint.StartAsync` runs a `while` loop: show `LobbyUI` → await Play → destroy lobby → show `CharacterSelectUI` → await confirm or back. **Back button** (`OnBackClicked`) destroys the select UI and `continue`s the loop (re-creates the lobby). **Battle/confirm** (`OnConfirmed`) stores the choice in `SelectedCharacterStore` and calls `_flow.EnterMatchmaking()`. Implementation: a `bool goBack` flag + single `UniTaskCompletionSource<CharacterId>` — back sets the flag and resolves with `default` to unblock the await. The reconnect path (`CheckActiveMatchAsync` → `EnterMatchFromLobby`) **skips** selection — the server already remembers the player's `PlayerSummary`.

## Pieces
- **`CharacterSelectUI`** (`Core/Lobby/Scripts/UI/CharacterSelectUI.cs`) — programmatic full-screen overlay (mirrors the `LobbyUI.Create()` pattern: own Canvas + CanvasScaler 1920×1080 + GraphicRaycaster). Champion carousel with spotlight, stat chips, ability card. `static Create(CharactersConfig, CharacterId current)` + `event Action<CharacterId> OnConfirmed` + `event Action OnBackClicked` + `Destroy()`. Back button in header fires `OnBackClicked`. Roster source = compiled-in `CharactersConfig.Default.Characters` (future: a Services RPC for the live DB roster).
- **`SelectedCharacterStore`** (`Core/Networking/Scripts/Services/`) — `public CharacterId Selected { get; set; } = new("brawler")`. Registered `Singleton` in `CoreStarterLifetimeScope`. **CoreStarter is the parent scope of Lobby/Matchmaking/Match**, so a singleton there survives all scene transitions — the standard place for cross-scene state.
- **Wire**: `MatchJoinRequest.CharacterId` (Key 2, `CharacterId`). `MatchSession.ConnectInternalAsync` injects `SelectedCharacterStore` and sets it on the request.
- **Server validation**: `MatchHub.JoinAsync` (new-player branch) does `CharacterCatalog.FromConfig(provision.Characters).Get(request.CharacterId).Id` — unknown/empty normalizes to the default, keeping the server authoritative. Stats/abilities then flow via `AetherServerSimulation.EnsurePlayer` → catalog lookup.

## Caveats
- Client lists `CharactersConfig.Default` but the server honors only what's in the match's catalog (from Services DB `characters:registry` or the static default). If the DB roster lacks a character the client offers, the server silently falls back to the default — so keep the DB seed and `CharactersConfig.Default` in sync (drop the `characters:registry` doc to reseed).
- Per-character body visuals come from `CharacterPrefabMap` (id→prefab); missing id → `_fallbackPrefab`.

See [character-selection] sibling [[lobby-ui]] and [[scene-ownership]].
