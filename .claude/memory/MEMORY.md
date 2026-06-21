# ClashUp Project Memory

## Project Overview
Unity multiplayer game with C# server backend (ASP.NET Core 8 + MagicOnion 7.10.0).

## Key Paths
- **Server solution**: `src/Server/ClashUp.Server.sln` (Services, GameServer, Server.Common)
- **Root solution**: `ClashUp.sln` (all projects including Shared)
- **Shared project**: `src/Shared/ClashUp.Shared/` — also a Unity local package via `file:` reference
- **Unity project**: `client/ClashUp.Unity/`
- **Docker**: `ops/docker/docker-compose.yml` (mongo + services + gameserver)
- **Build artifacts**: `.artifacts/` (redirected from bin/obj via Directory.Build.props)
- **Dotnet path (Windows)**: `"/c/Program Files/dotnet/dotnet.exe"`
- **AetherNet vendor clone**: `external/AetherNet/` (gitignored, run `tools/setup-aethernet.ps1` after cloning)

## Client Folder Structure (Unity Assets)
Scripts live in typed subfolders (Interfaces/, Services/, Clients/, Models/, Config/, Scopes/, EntryPoints/, Presenters/, UI/, Receivers/). See [folder-conventions.md](folder-conventions.md).

## Assembly Definitions (asmdef)
| Name | Namespace | Location |
|------|-----------|----------|
| ClashUp.AppStarter | ClashUp.Client.AppStarter | _Bootstrap/AppStarter/Scripts/ |
| ClashUp.Core | ClashUp.Client.Core | Core/Scripts/ |
| ClashUp.UI | ClashUp.Client.UI | Core/UI/Scripts/ |
| ClashUp.Networking | ClashUp.Client.Networking | Core/Networking/Scripts/ |
| ClashUp.Gameplay | ClashUp.Client.Gameplay | Core/Gameplay/Scripts/ — subfolders: Interfaces/, Services/, Input/, Player/, Camera/ |
| ClashUp.Match | ClashUp.Client.Match | Core/Match/Scripts/ |
| ClashUp.CoreStarter | ClashUp.Client.CoreStarter | Core/CoreStarter/Scripts/ |
| ClashUp.Lobby | ClashUp.Client.Lobby | Core/Lobby/Scripts/ |
| ClashUp.Matchmaking | ClashUp.Client.Matchmaking | Core/Matchmaking/Scripts/ |

## Unity Package Versions (manifest.json)
- `com.unity.cinemachine`: 3.1.6 — namespace `Unity.Cinemachine`; `BindingMode` in `Unity.Cinemachine.TargetTracking`
- `com.unity.inputsystem`: 1.19.0 — use `Keyboard.current`, `Touchscreen.current`, `Mouse.current` for raw polling
- Player Settings → Active Input Handling: must be **"Both"** for new Input System + legacy UGUI to coexist
- `ClashUp.Gameplay.asmdef` references: `Unity.Cinemachine`, `Unity.InputSystem`, `AetherNet.Unity`, `Unity.TextMeshPro`; precompiledReferences includes `AetherNet.Shared.dll`
- `ClashUp.Match.asmdef` references: `Unity.Cinemachine` (added for MatchLifetimeScope vcam serialized field)

## Server Package Versions (Directory.Packages.props)
- MagicOnion: 7.10.0 (7.10.1 does NOT exist on NuGet)
- MessagePack: 3.1.4
- Grpc: 2.71.0
- MongoDB.Driver: 3.1.0
- AetherNet.Shared: 0.1.0 (fallback NuGet version, normally uses local clone ProjectReference)

## GUID Generation
- Use `python tools/generate-guid.py [count]` for Unity-style GUIDs (32 hex, no dashes)
- Never hand-write or guess GUIDs — always generate
- Custom command: `/generate-guid`

## Architecture Rules
- **Dumb client**: The client is a thin display layer. NEVER put game logic, state decisions, or authoritative behavior on the client. All game state transitions (match start, match end, scoring, etc.) must come from the server. The client only renders what the server tells it.
- **Server-authoritative**: The server is the single source of truth for all game state.

## Physics / AetherNet Architecture
- **Library**: AetherNet (`external/AetherNet/` gitignored clone) — GC-free deterministic 2D physics over Aether.Physics2D (Box2D port)
- **Simulation seam**: `IClientSimulation` (client) / `IServerSimulation` (server) — AetherNet implementations are `AetherClientSimulation` / `AetherServerSimulation`
- **Shared world**: `MatchPhysicsWorld` in `ClashUp.Shared/Simulation/` — same code runs on client (prediction) and server (authority)
- **Coordinate mapping**: game (X, Z) ↔ Aether (x, y); gravity = zero for top-down
- **Player bodies**: dynamic circles, velocity set from input each tick (kinematic move-and-slide style)
- **Player radius**: `MatchPhysicsWorld` constructor parameter (default `0.5f`). Client reads from prefab's `AetherCircleCollider.Radius` (Player.prefab `_radius: 0.5`). Server uses default. **These MUST match** — a mismatch makes client/server resolve wall collisions at different distances → constant position disagreement → reconciliation shimmer against walls (was 0.4 server vs 0.5 prefab).
- **Wire protocol**: `InputCommand` up, `SnapshotPacket → WorldStatePacket → PlayerStateDto{X,Z,Yaw,Health,LastProcessedInputSeq,IsInvulnerable,RespawnInTicks}` down
- **AetherNet.Shared**: `AetherNet.Shared.dll` (netstandard2.0, C# 10) committed in `Assets/Packages/AetherNet.Shared.0.1.0/`. Uses pre-built DLL — Unity can't compile C# 10 file-scoped namespaces.
- **AetherNet.Unity**: Source-only package copied to `Assets/Packages/AetherNet.Unity/` by `setup-aethernet.ps1`. These files ARE C# 9 compatible (block-scoped namespaces). Has Runtime + Editor asmdefs. `AetherSceneBaker.cs` excluded (depends on `AetherNet.Server`).
- **AetherNet.Unity asmdefs**: `AetherNet.Unity` (Runtime, unsafe, precompiled refs: AetherNet.Shared.dll + Aether.Physics2D.dll) and `AetherNet.Unity.Editor` (Editor-only, refs AetherNet.Unity)
- `Aether.Physics2D.dll` installed via NuGetForUnity. Both DLLs listed in `ClashUp.Shared.Unity.asmdef` precompiledReferences.
- **Server DLL wiring**: conditional MSBuild in `AetherNet.refs.props` (repo root) — `ProjectReference` when clone exists, `PackageReference` fallback
- **AetherNetSettings**: ScriptableObject at `Assets/Resources/AetherNetSettings.asset` — configures `SimulationPlane` (XZ) and `PixelsPerMeter` (1). Auto-applies in both editor (`[InitializeOnLoadMethod]`) and runtime (`[RuntimeInitializeOnLoadMethod]`).
- **Determinism watch**: Aether.Physics2D is float-based; monitor for rubber-banding jitter between x86 server and ARM client

### Client Prediction & Interpolation (Gambetta)
- **Local player**: client-side prediction + server reconciliation via `LastProcessedInputSeq` (sequence-based ack, NOT tick-based). Render with sub-tick alpha-lerp (prev/current) for smooth motion between 30 Hz fixed steps.
- **Remote players**: NOT in client physics world. Pure entity interpolation from buffered authoritative snapshots, rendered ~66ms in the past (2 × tick interval). `RemotePlayerInterpolator` ring buffer per player.
- **Lag compensation**: documented but not yet implemented (no combat). See [netcode-architecture.md](netcode-architecture.md).
- **Fixes to AetherNet**: must be generic/non-specific (upstreamable). Key fixes: `Directory.Packages.props` CPM opt-out, `SimulationPlane` enum, configurable `PixelsPerMeter`, `#nullable enable` on Unity files, `using` aliases for type ambiguities (RaycastHit, Vector2)

## Character / Stat / Health System
- **Characters**: `CharacterId` (string struct like PlayerId), `CharacterDefinition`, `CharacterCatalog` (instance, in `ClashUp.Shared/Characters/`)
- **`CharacterRegistry` DELETED** — replaced by `CharacterCatalog` (instance initialized from `CharactersConfig`) + `CharactersConfig` (MessagePack, sent over the wire)
- **`CharactersConfig`**: MessagePack object in `ClashUp.Shared/MessagePackObjects/` — `DefaultCharacterId` + `Characters[]`. Has static `Default` (Brawler). DB key: `characters:registry`, fetched by `CharacterConfigProvider` (60s cache) on Services side, sent in `MatchProvision.Characters` (Key 7) and `JoinResult.Characters`
- **`CharacterCatalog`**: instance class, `Get(CharacterId)` falls back to `Default` on unknown id. Both server (`MatchCharactersHolder`) and client (`MatchCharactersHolder` in Gameplay) hold one, initialized from the wire config.
- **Stats**: `StatBlock` — `MaxHealth` (100), `Damage` (10), `MoveSpeed` (5). Now MessagePack-annotated (sent inside `CharactersConfig`).
- **Per-player move speed**: `MatchPhysicsWorld.EnsurePlayer` accepts `moveSpeed` param, stores per-player speeds. `MovementModel.Step` also accepts optional `moveSpeed` param.
- **Default character**: "Brawler". `CharactersConfig.Default` is the hardcoded fallback used when DB has no config. **Roster now = Brawler + Mage** (both in `CharactersConfig.Default` AND the `ConfigSeeder` `characters:registry` seed). Adding a character = both places; existing/deployed Mongo DBs need the doc dropped/updated to pick it up (seeder only writes when key missing).
- **Character selection EXISTS** (no longer hardcoded to default): pre-matchmaking `CharacterSelectUI`, choice held in `SelectedCharacterStore` (CoreStarter scope, survives scene loads), sent via `MatchJoinRequest.CharacterId` (Key 2), validated server-side in `MatchHub.JoinAsync`. See [character-selection.md](character-selection.md).
- **Health**: `HealthTable` in `ClashUp.Shared/Simulation/` — `Initialize`, `ApplyDamage`, `ApplyHeal`, `SnapHealth`. Owned by both `AetherServerSimulation` and `AetherClientSimulation`.
- **Health in snapshots**: `PlayerStateDto.Health` (Key 4) — sent every tick, client reconciles against it
- **Random seed**: `DeterministicRng` (Xorshift32) in Shared. Per-tick re-seeding via `ForTick(baseSeed, tick)` to avoid drift. Seed generated server-side, sent in `JoinResult.RandomSeed` (Key 6).
- **PlayerSummary.CharacterId** (Key 4) — sent on join
- **PlayerRenderState**: has `Health`, `MaxHealth`, and `Prev{X,Z,Yaw}` fields. Local player synced from `HealthTable` in `SyncRenderStates()`; remote health comes from `RemotePlayerInterpolator`.
- **Combat is LIVE**: abilities deal damage — `AbilityExecutor.EvaluateHitbox` → `HealthTable.ApplyDamage` (Brawler Punch 10 / Charge 20). `WorldSpaceHealthBar` reflects it. Spawn invuln (3s) still guards. **Server respawn**: 5-second delay (`RespawnDelayTicks=150`). `_respawnTimers` dict counts down; at 0 restore HP + invuln + snap position. Dead player input acked but not applied. Client shows "YOU DIED" + countdown via `RespawnScreenController`; hides input controls. See [[stat-health-system]].
- **Health bar UI**: `WorldSpaceHealthBar.cs` in `Core/Gameplay/Scripts/UI/` — uses `Slider _slider` (NOT `Image.fillAmount`). `SetHealth` sets `slider.value = current/max`. Player.prefab: HealthBar has Slider component + WorldSpaceHealthBar; Fill child Image is the Slider's fillRect. `PlayerViewSystem` caches per-player ref and calls `SetHealth(current, max)` each frame.

## Map System
- **Shared POCOs**: `MapData`, `BakedEntityDef`, `BakedFixtureDef`, `SpawnArea` in `ClashUp.Shared/Maps/`
- **SpawnResolver**: Static `GetSpawnPosition(MapData?, teamId, slotIndex)` in Shared — falls back to linear layout when no map
- **Server**: `ServerMapStore` singleton loads `Maps/Data/*.json` (System.Text.Json). `AetherServerSimulation.LoadMap()` + spawn via `SpawnResolver`
- **Client**: `MapDefinition` SO (mapId, displayName, TextAsset json, visual prefab) + `MapRegistry` SO (`SerializedDictionary<string, MapDefinition>`)
- **Client deserialization**: `MapDataDeserializer` uses Newtonsoft.Json (not System.Text.Json — netstandard2.1)
- **Wire protocol**: `MapId` field on `MatchConfig` (Key 4), `MatchProvision` (Key 5), `JoinResult` (Key 7) — default `"arena_tdm"`. `MatchProvision` also carries `PlayerAssignments` (Key 6, `PlayerId→TeamId` list pre-assigned by `Matchmaker`) and `Characters` (Key 7, `CharactersConfig`)
- **Baker**: `ClashUpMapBaker` editor tool ("ClashUp/Bake Map to JSON") — scans `AetherRigidbody` + `SpawnPointMarker` components
- **Visual prefab**: Instantiated by `MatchSessionRunner.LoadMap()`, destroyed on Dispose. NO Unity colliders — physics is AetherNet only
- **Materials**: `Assets/Core/Match/Content/Maps/Materials/` — WallGray, GroundGreen, SpawnZone (transparent)
- **Maps**: `arena_basic` (40×30 landscape, legacy), `arena_tdm` (50×80 portrait, current default) — 24 entities, teams at Z=±35
- **Map JSON location**: server `src/Server/ClashUp.GameServer/Maps/Data/` + client `Assets/Core/Match/Content/Maps/` — must keep both in sync

## Ability System
- **Shared POCOs**: `AbilityDefinition`, `AbilityNode`, `HitboxConfig`, `ProjectileConfig`, `TelegraphConfig` in `ClashUp.Shared/Abilities/`
- **Node types**: `Parallel=0, Hitbox=1, Projectile=2` (string enum in JSON — never integer, fragile on reorder)
- **Sequential chaining**: via `AbilityNode.Next` linked-list; root's output connects to first node, "Next" port chains the rest
- **Parallel execution**: Parallel node's `Children[]` — all run simultaneously, `Next` runs after all finish
- **No Sequence node**: sequential chaining is implicit via Next ports — Sequence node was removed
- **Wire protocol**: `AbilitiesConfig` sent over wire (Key 9 in `JoinResult`, Key 8 in `MatchProvision`) — thin client payload: `AbilityClientInfo { Id(0), Telegraph(1), AutoRange(2), CastMode(3), CastShape(4) }`. Static `AbilitiesConfig.Default` is the fallback when server sends null (old containers). Full node trees still loaded from JSON locally by both server (`ServerAbilityStore`) and future client registry.
- **`AbilityClientInfo.CastShape`** (Key 4, `TelegraphConfig?`): the damage footprint shown by the triggered cast flash. Auto-derived server-side in `ServerAbilityStore.BuildClientConfig` → `DeriveCastShape(root)`: Capsule hitbox→`{Capsule, Length, Width=2*Radius}`, Cone→`{ForwardCone, Length, Angle}`, Circle hitbox→`{TargetCircle, Radius}`, **Projectile root→`{TargetCircle, Radius=AoeRadius>0?AoeRadius:max(Radius,0.3)}`**. Single source of truth = the root node.
- **Projectiles are LIVE** (no longer stubbed) — server-authoritative `ProjectileSimulation`, client `ProjectileViewSystem` dead-reckon + explosion. `ProjectileConfig` now has `AoeRadius/AoeAmount/AoeEffect/LifetimeTicks`. `TelegraphConfig.ForwardOffset` (Key 6) slides a `TargetCircle` downrange for ranged-AoE previews. See [projectile-system.md](projectile-system.md).
- **Cast modes**: `CastMode` = `Instant=0, Aimed=1, TargetPoint=2`. **TargetPoint** = joystick direction+distance picks a world point; the cast originates THERE (not the caster). Mage Blast uses it. Joystick magnitude rides the wire as `InputCommand.AimDistanceQ` (Key 6, repurposed from unused `AimPitchQ`); `IAbilityInput.AimMagnitude`/`LiveAimMagnitude`. `maxDist = TelegraphConfig.ForwardOffset`. See [target-point-cast.md](target-point-cast.md).
- **`MatchAbilitiesHolder`**: client-side lookup (`Dictionary<string, AbilityClientInfo>`), initialized in `MatchSessionRunner` from `JoinResult.Abilities`. Always apply `config ?? AbilitiesConfig.Default` in `Initialize` — MessagePack returns null for missing keys, NOT C# init defaults.
- **Server**: `ServerAbilityStore` loads `Abilities/Data/*.json`; `AbilityExecutor` (per-match) processes input and ticks nodes
- **Executor**: `ActiveAbility.Flatten()` builds flat node array; `EvaluateChain()` follows `Next` pointers; `EvaluateParallel()` uses `Children[]`
- **Telegraph shapes**: `CircleAroundCaster`, `TargetCircle`, `ForwardLine`, `ForwardCone`, `Capsule` — direction always follows `AimYaw`. `TelegraphConfig` has `Width` (Key 5) for Capsule/ForwardLine and `ForwardOffset` (Key 6) for downrange `TargetCircle` previews (Mage Blast).
- **Shaped hitboxes**: `HitboxConfig.Shape` = `Circle`(default) / `Capsule` / `Cone`, plus `Length` (capsule segment / cone reach), `Angle` (cone full degrees); `Radius` = circle radius / capsule half-width. `AbilityExecutor.EvaluateHitbox` does an `OverlapCircle` broad-phase then refines: capsule = point-to-segment dist ≤ Radius; cone = dist ≤ Length AND angle-to-aim ≤ Angle/2. Helpers `ShapeContains` / `PointSegmentDistanceSq`. Damage matches the cast-flash footprint exactly.
- **Brawler tuning**: Punch (auto) = Capsule hitbox L3 R1 Offset0, 10 dmg, AutoRange 4, telegraph `CircleAroundCaster` r4. Charge (manual) = Cone hitbox L3.5 A90, 20 dmg, telegraph `ForwardCone` L3.5 A90.
- **Mage tuning** (2nd character): `mage_bolt` (auto, Projectile root, single-target) spd14 range9 8dmg cd24, telegraph CircleAroundCaster r9. `mage_blast` (manual Aimed, Projectile root, AoE) spd11 range10, 12 direct + 18 AoE r2.5, cd90, telegraph TargetCircle r2.5 ForwardOffset10.
- **Editor tool**: `Tools → Ability Editor` (UIToolkit GraphView, `ClashUp.AbilityEditor.asmdef`). Save to the **server** `Abilities/Data/ability_*.json` path only — there is NO client ability-JSON folder; the client gets ability config over the wire (`AbilitiesConfig`). RootNode now exposes **Trigger Mode / Cast Mode / Auto Range** (previously these were silently DROPPED on save — fixed). See [[feedback-ability-editor-sync]].
- **JSON serialization**: `JsonStringEnumConverter` (server, System.Text.Json) + `StringEnumConverter` (editor, Newtonsoft) — MUST use string enums
- **Wiring**: `CharacterDefinition.Abilities AbilityId[]` — defined in `CharactersConfig` (DB or static default). `AetherServerSimulation.EnsurePlayer` calls `AbilityExecutor.InitPlayer` with the character's ability list on first spawn. `_knownPlayers` hashset prevents double-init on reconnect.
- **AbilityVisualConfig**: one SO per ability (`CreateAssetMenu: ClashUp/Ability Visual Config`). Holds VFX prefabs, sounds, telegraph visuals. Connected in editor Root Node → GUID written to JSON as `VisualConfigGuid`.
- **AbilityVisualRegistry**: SO (`ClashUp/Ability Visual Registry`) with `Entry[] { Guid, AbilityId, Config }`. `GetByGuid()`/`GetByAbilityId()` for lookups. Custom editor "Refresh GUIDs" button fills Guid strings from asset refs. `MatchLifetimeScope._abilityVisualRegistry` (was `_abilityVisualConfig` — inspector must be re-wired).
- **AbilityVisualHandler**: injects `AbilityVisualRegistry` + `MatchAbilitiesHolder`, resolves visuals by `GetByAbilityId` on `ability_cast` events. On cast it spawns an `AbilityAreaFlash` of the ability's `CastShape`, oriented by `aimYaw` (now in the `ability_cast` payload). Optional `CastVfxPrefab` is instantiated with `Quaternion.Euler(0,aimYaw,0)`.
- **AbilityAreaFlash** (`Core/Gameplay/Scripts/Abilities/`): triggered ground-flash MonoBehaviour rendering the exact damage footprint, fades alpha over `CastFlashDuration` then self-destroys. `AbilityShapeMesh` (same folder) = shared static mesh builders (`BuildCircle/ForwardLine/Cone/Capsule` + `Build(mesh,config,aimYaw)`), reused by `TelegraphRenderer` AND `AbilityAreaFlash` (capsule = forward rect + rounded caps). Per-ability `AbilityVisualConfig.CastFlashColor`/`CastFlashDuration` (alpha must be >0 to be used) — Punch yellow-gold (1,0.85,0.2,0.6), charge orange (1,0.45,0.12,0.7)@0.28s, set via `assets-modify`.
- **TelegraphController**: `IStartable/ITickable/IDisposable` VContainer service — owns two `TelegraphRenderer` GameObjects (auto + primary). Resolves configs from `MatchAbilitiesHolder` + `MatchCharactersHolder` by local player's `AutoAttackId`/`ActiveAbilityId`. Switches auto↔primary based on `IAbilityInput.OnTouching`. Primary yaw tracks `IAbilityInput.LiveAimYaw`. Registered in `MatchLifetimeScope`.
- **Telegraph materials**: `Assets/Core/Gameplay/Content/Telegraphs/` — `M_AutoTelegraph.mat` (Sprites/Default, yellow 0.35α), `M_PrimaryTelegraph.mat` (Sprites/Default, orange 0.55α). Use `Sprites/Default` NOT `Unlit/Transparent` — the latter has no `_Color` support.
- See [ability-authoring.md](ability-authoring.md) for full schema and examples
- **Editor UI restyled** (USS-driven, category-extensible nodes). To add a node type, edit `NodeVisuals.cs` dicts + register in the menu — no new USS. See [ability-editor-ui.md](ability-editor-ui.md)

## Important Conventions
- Central package management via `Directory.Packages.props`
- Build output redirected to `.artifacts/` to avoid polluting Unity's local package import
- `MsgPack017` suppressed in ClashUp.Shared.csproj (MessagePack v3 stricter about init properties)
- Server projects need both `MagicOnion.Server` AND `MagicOnion.Client` (server-to-server RPC)
- Server projects need `Grpc.Net.Client` for `GrpcChannel`
- EnvironmentConfig is a ScriptableObject using `SerializedDictionary<ServerEnvironment, string>` from editor-toolbox
- Toolbox package: `com.browar.editor-toolbox` (asmdef name: `Toolbox`)

## User Preferences
- Prefers concise, action-oriented responses — do it, don't explain it
- Wants hierarchical folder structure, not flat
- Automate Unity Editor steps via MCP tools first, editor scripts second — NEVER leave manual instructions
- Only leave to user what truly can't be scripted (e.g. creating ScriptableObject .asset files)
- Wants persistent learnings across sessions (memory files, /reflect command)
- Uses custom commands and subagents — see `.claude/commands/` and `.claude/agents/`
- Doesn't want manual rebuild steps — automate everything (e.g. `pull_policy: build` in docker-compose)
- Prefers quick iterative fixes over lengthy exploration/planning when the problem is clear
- **Fix vendored packages at the source** — never create project-side workarounds for issues in vendored packages (AetherNet, etc.). Fix the package itself so it works correctly.

## Match Camera Architecture
- **MatchCamera** and **MatchVirtualCamera** are scene objects in `Match.unity` (NOT created via code)
- `MatchCamera`: Camera + CinemachineBrain + CameraRegistrant (`IsMatchCamera=true`, tag=MainCamera)
- `MatchVirtualCamera`: CinemachineCamera + CinemachineFollow (offset `(0, 32.5, -32.8)`, WorldSpace binding, 0.15 damping, FOV=35, rotation X=46.1)
- `MatchCameraRig` (VContainer `ITickable`) receives `CinemachineCamera` via DI, polls `PlayerViewSystem.LocalPlayerTransform` each tick, sets `_vcam.Follow` once player spawns
- `MatchLifetimeScope` has `[SerializeField] CinemachineCamera _virtualCamera` — must be wired to scene vcam

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

## Android / IL2CPP Build Requirements
- **MagicOnion Source Generator**: `[MagicOnionClientGeneration(...)]` attribute required in `ClashUp.Networking` for IL2CPP. See `MagicOnionGeneratedClientInitializer.cs`.
- **Standard shader**: Must be in `AlwaysIncludedShaders` (fileID: 46) — player materials use it.
- **Custom AndroidManifest.xml**: Do NOT add one — Unity generates it correctly. Adding a minimal one strips the launcher activity.
- **Emulator ports**: `adb reverse tcp:5001 tcp:5001` AND `tcp:5101 tcp:5101` (Services + GameServer).
- **Package name**: `com.DefaultCompany.ClashUp.Unity`
- **adb path**: `C:\Users\Adiel\AppData\Local\Android\Sdk\platform-tools\adb.exe`

## See Also
- [project-structure.md](project-structure.md) — Detailed architecture notes
- [scene-ownership.md](scene-ownership.md) — Domain ownership rules (assets live where their lifespan is)
- [folder-conventions.md](folder-conventions.md) — Script subfolder rules (Interfaces/, Services/, etc.)
- [patterns.md](patterns.md) — Code patterns (IDebugLogger, canvas scaler, etc.)
- [debugging.md](debugging.md) — Common pitfalls and solutions (incl. full match-end freeze sequence)
- [unity-mcp.md](unity-mcp.md) — Unity MCP CLI usage patterns and gotchas
- [dev-environment.md](dev-environment.md) — CLASHUP_DEV define, Tailscale phone testing, ServerEnvironment enum
- [stat-health-system.md](stat-health-system.md) — Character stats, health table, deterministic RNG architecture, respawn system
- [netcode-architecture.md](netcode-architecture.md) — Gambetta netcode: prediction, reconciliation, entity interpolation
- [feedback-ticket-status.md](feedback-ticket-status.md) — Never mark tickets Done without user confirmation it's working
- [feedback-ability-editor-sync.md](feedback-ability-editor-sync.md) — Editing the ability data model? Also update the Ability Editor tool (or fields get dropped on save)
- [ability-authoring.md](ability-authoring.md) — How to create ability JSON files: editor tool, schema, node types, wiring to characters
- [projectile-system.md](projectile-system.md) — Server-authoritative projectile sim + client dead-reckon visuals, AoE explosions, projectile events (fire-and-forget)
- [target-point-cast.md](target-point-cast.md) — CastMode.TargetPoint: joystick dir+distance picks a world point; magnitude input pipeline, telegraph, editor round-trip
- [character-selection.md](character-selection.md) — Pre-matchmaking character picker, SelectedCharacterStore, MatchJoinRequest.CharacterId wiring
- [lobby-ui.md](lobby-ui.md) — Lobby pager UI: horizontal scroll, vertical per-page scroll, bottom bar, play button wiring, VContainer scope
- [gcp-ops-gotchas.md](gcp-ops-gotchas.md) — Deploy/verify gotchas: GITHUB_TOKEN env breaks push (use `env -u`), MIG update policies (Services PROACTIVE / GameServer OPPORTUNISTIC), verifying GS via MIG health + Monitoring REST API, CI dev-vs-release paths
- [deployment-architecture.md](deployment-architecture.md) — Version-aware gateway (one image, two tiers), on-demand backend spawning, prewarm-newest + version-transition lifecycle, mutually-exclusive CCU model, dashboard delete/hidden-latest
- [dashboard-tool.md](dashboard-tool.md) — Local fleet dashboard (`src/Tools/ClashUp.Dashboard`, :8080, NOT deployed): GCP read clients, `/api` endpoints, uptime calendar (awake-hours from `instance/uptime`), build/verify gotchas (Duration ambiguity, exe-lock, preview-tool verification)
