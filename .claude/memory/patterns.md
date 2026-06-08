# Code Patterns & Conventions

## IDebugLogger (Logging Service)
- **Interface**: `ClashUp.Client.Core.IDebugLogger` in `Core/Scripts/Interfaces/IDebugLogger.cs`
- **Implementation**: `ClashUp.Client.Core.UnityDebugLogger` in `Core/Scripts/Services/UnityDebugLogger.cs`
- **Methods**: `Log(string)`, `LogWarning(string)`, `LogError(string)`
- **Registration**: Both `AppStarterLifetimeScope` and `CoreStarterLifetimeScope` register `IDebugLogger → UnityDebugLogger` as Singleton
- **Usage**: Inject via constructor, never use `UnityEngine.Debug.Log` directly in DI-managed classes
- **Editor scripts**: Can still use `Debug.Log` directly (not DI-managed)

## Runtime World Setup (IStartable + IDisposable Services)

Plain C# VContainer services (not MonoBehaviours) that spawn world content at match start and tear it down on scope dispose:
- Implement `IStartable` (VContainer lifecycle) + `IDisposable`
- Register with `builder.RegisterEntryPoint<T>().AsSelf()` if other services inject it by concrete type
- Spawn GameObjects in `Start()`, destroy them in `Dispose()`
- Expose outputs (e.g. `Transform PlayerTransform`) for downstream services

Examples: `PlayerSpawner`, `MatchCameraRig`, `JoystickInputProvider`

## MonoBehaviour Initialize() Pattern

When a MonoBehaviour is created programmatically via `AddComponent<T>()`, `Awake()` fires before you can set any fields. Never rely on `Awake()` for initialization:
- Expose `internal void Initialize(params...)` and call it immediately after `AddComponent`
- `Awake()` stays empty (or omitted)
- Example: `Joystick.Initialize(zone, background, handle, radius)`, `PlayerMovement.Initialize(input, speed)`

## Input Gate Pattern (MatchInputGate)

A simple service that controls whether gameplay input is accepted:
- `MatchInputGate` has `bool IsEnabled`, `event Action<bool> OnChanged`, `Enable()`, `Disable()`
- Controlled by `MatchSessionRunner`: `Enable()` after `ConnectAndJoinAsync` succeeds, `Disable()` in `OnMatchEnded`
- Consumed by `JoystickInputProvider`: checks `_gate.IsEnabled` in `Value` getter; subscribes to `OnChanged` to toggle `InputEnabled` on the `Joystick` MonoBehaviour
- Registered as `builder.Register<MatchInputGate>(Lifetime.Singleton)` in `MatchLifetimeScope`
- Lives in `ClashUp.Gameplay` so both Gameplay and Match assemblies can reference it

## Cinemachine 3.x (com.unity.cinemachine 3.1.6)

- Namespace: `using Unity.Cinemachine;` — NOT the old `Cinemachine` namespace
- Virtual camera component: `CinemachineCamera` (not `CinemachineVirtualCamera`)
- `BindingMode` enum lives in `Unity.Cinemachine.TargetTracking` — add `using Unity.Cinemachine.TargetTracking;`
- Follow behavior: `CinemachineFollow` component (position) + `CinemachineRotationComposer` (aim)
- `CinemachineFollow.TrackerSettings.PositionDamping` is a `Vector3` (per-axis damping)
- Fixed-rotation follow camera (no aim): set `vcamGo.transform.rotation = Quaternion.LookRotation(-followOffset)`, use `BindingMode.WorldSpace`, add NO rotation component
- `CinemachineBrain` goes on the main Camera GameObject

## Procedural Circle Sprite

```csharp
private static Sprite CreateCircleSprite(int diameter)
{
    var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
    float center = (diameter - 1) * 0.5f;
    var pixels = new Color32[diameter * diameter];
    for (int y = 0; y < diameter; y++)
        for (int x = 0; x < diameter; x++) {
            float dist = Mathf.Sqrt((x-center)*(x-center) + (y-center)*(y-center));
            byte a = (byte)(Mathf.Clamp01(center - dist + 0.5f) * 255);
            pixels[y * diameter + x] = new Color32(255, 255, 255, a);
        }
    tex.SetPixels32(pixels); tex.Apply();
    return Sprite.Create(tex, new Rect(0,0,diameter,diameter), new Vector2(0.5f,0.5f), diameter);
}
```

Anti-aliased edge via `center - dist + 0.5f` clamp. Used for joystick background and knob.

## Canvas Scaler Convention
- All code-generated canvases use `matchWidthOrHeight = 1f` (match height, not width)

## Boot Flow Error Handling
- Server connection (ping) must **block and retry** — never swallow exceptions and continue
- Pattern: `while` loop with `try/catch`, retry after delay, only `break` on success
- `OperationCanceledException` must be re-thrown (don't catch-and-retry cancellation)
- Show retry feedback in loading screen ("Connection failed. Retrying...")

## Environment Picker
- **Prefab-based** with TMP_Dropdown (NOT code-generated legacy UI): `Assets/_Bootstrap/AppStarter/Content/Resources/EnvironmentPickerUI.prefab`
- Loaded via `Resources.Load<GameObject>("EnvironmentPickerUI")` at runtime
- Uses `TextMeshProUGUI` + `TMP_Dropdown` — legacy `UnityEngine.UI.Text` fonts broken in Unity 6
- `ClashUp.AppStarter.asmdef` references `Unity.TextMeshPro`
- Confirm button triggers `UniTaskCompletionSource.TrySetResult`
- Must ensure `EventSystem` exists (check + create if null)
- Sort order 200 (above loading screen at 100)

## Camera Service (ICameraService / CameraService)

- **Interface**: `ICameraService` — `ActiveCamera`, `Register(camera, isMatchCamera)`, `Unregister(camera)`
- **Implementation**: `CameraService` — static lazy singleton (`_instance ??= new CameraService()`), lives in `ClashUp.Gameplay`
- **Files**: `Core/Gameplay/Scripts/Interfaces/ICameraService.cs`, `Core/Gameplay/Scripts/Services/CameraService.cs`
- **Access pattern**: `CameraService.Instance.ActiveCamera` — works from any MonoBehaviour without DI injection
- **Behaviour**: When a match camera registers (`isMatchCamera=true`), all other registered cameras are disabled. When it unregisters (destroyed), they re-enable. `ActiveCamera` falls back to `Camera.main` when no match camera is registered.
- **CameraRegistrant** MonoBehaviour: `[RequireComponent(Camera)]`, `IsMatchCamera` property, registers in `Start()`, unregisters in `OnDestroy()`. Location: `Core/Gameplay/Scripts/Camera/CameraRegistrant.cs`
- **Match camera**: `MatchCameraRig.BuildMainCamera()` adds `CameraRegistrant` with `IsMatchCamera = true` programmatically (set after `AddComponent` returns, before `Start()` fires)
- **Non-match cameras**: `CameraRegistrant` (IsMatchCamera=false) added via MCP to Lobby, CoreStarter, Matchmaking scene cameras
- **BillboardLabel**: uses `CameraService.Instance.ActiveCamera` — always faces the correct camera regardless of scene

## Camera Ownership
- Main Camera lives in Lobby scene (Core), NOT AppStarter
- AppStarter scene is bootstrap-only — no visual/rendering objects

## Client Prediction + Interpolation Pattern

Two separate rendering paths for local vs remote players:

**Local player (snappy):**
- `ClientPredictionWorld.Predict()` immediately applies input to physics
- `PlayerRenderState` stores prev and current tick state (shifted each step)
- `PlayerViewSystem` lerps by `RenderAlpha` (accumulator fraction 0..1) → smooth at any fps
- On snapshot: snap to authoritative state, drop acked inputs by `SequenceId`, replay rest

**Remote players (smooth):**
- NOT in client physics. `RemotePlayerInterpolator` buffers snapshots with `serverStampMs`
- Rendered ~66ms behind newest sample, lerping between two bracketing samples
- `PlayerViewSystem` queries interpolator each frame — no physics Lerp or exponential smoothing

**When to snap vs replay:**
- Reconciliation always snaps the local player to the server's position then replays pending inputs
- Remote players are never snapped — interpolation buffer handles everything
- If the interpolation clock falls too far behind (stall/burst), it snaps forward to `target - delay`

## Dumb Client Principle
- Client is a thin display layer — NEVER put game logic on the client
- All game state transitions (match end, scoring, phase changes) must originate from the server
- Client only renders what the server tells it — no local state decisions
- If client misses a server broadcast, the SERVER must re-send it — don't add client-side fallback logic
- Client-side timers are display-only — they don't trigger state changes

## Server-Authoritative Timers
- Match countdown must use server-provided `ElapsedSeconds` as the base, not start from zero
- `JoinResult.ElapsedSeconds` = how far into the match (from `MatchClock.ElapsedSeconds`)
- Client computes: `remaining = DurationSeconds - (ElapsedSeconds + localElapsedSinceJoin)`
- Use `DateTimeOffset.UtcNow` for local elapsed (survives focus loss), not `Time.time`

## Player Identity Flow
- `SessionTokenStore` holds `PlayerId` (set during auth login)
- `MatchmakingClient` sends `x-clashup-player` header via gRPC `Interceptor` on all calls
- Server's `ResolveCurrentPlayerId()` reads this header (fallback: random GUID — should NOT happen)
- Same player ID must be used for Enqueue, Poll, and CheckActiveMatch — otherwise reconnect fails

## MatchContext End-of-Match Callbacks
`MatchContext` has two separate callbacks wired by `MatchRegistry.Register`:
- `OnMatchEndedEarly`: fires `NotifyMatchEndedAsync` (GS → Services, marks DB "Ended") — invoked **before** broadcasting to clients, giving DB time to update
- `OnMatchEnded`: removes from registry + disposes context — invoked **after** 2s client broadcast window
- Never merge these — the DB notification must lead the cleanup by ~2s to prevent lobby reconnect loops.
- `IsEnded` + `EndResult` on `MatchContext` are set at the same moment as `OnMatchEndedEarly`, for late-join replay in `JoinAsync`.

## ConfigSeeder
- `ConfigSeeder` always upserts (no skip-if-exists) so config values take effect on server restart without manual DB cleanup.
- Current match config: `{"NumberOfTeams":1,"TeamSize":1,"DurationSeconds":20,"ObjectiveType":"survival"}`

## Player Visual Setup (Character Prefab System)

- **Player prefab**: `Assets/Core/Gameplay/Art/Prefabs/Player.prefab` — physics-only root with `Transform` + `AetherCircleCollider` (radius 0.5). No mesh or renderer — visual comes from character prefab child.
- **Character prefabs**: `Assets/Core/Gameplay/Art/Prefabs/Characters/` — one per character (e.g., `Brawler.prefab`). Contains the visual mesh/renderer. No collider (physics on player root).
- **CharacterPrefabMap**: ScriptableObject at `Assets/Core/Gameplay/Art/Config/CharacterPrefabMap.asset` — maps `CharacterId.Value` (string) → prefab, with `_fallbackPrefab` for unknown ids. Script at `Scripts/Config/CharacterPrefabMap.cs`.
- **DI wiring**: `MatchLifetimeScope` has serialized `_playerPrefab` (GameObject) and `_characterPrefabMap` (CharacterPrefabMap), registered as instances
- **PlayerViewSystem**: Instantiates `_playerPrefab` (root), then instantiates character prefab as child via `_characterMap.Get(characterId)`. Also sets world-space TMP name label from `PlayerSummary.DisplayName`.
- **AetherClientSimulation**: Reads `AetherCircleCollider.Radius` from the prefab to construct `MatchPhysicsWorld` with matching radius — single source of truth
- **Billboard name labels**: `Player.prefab` has a `NameLabel` child (world-space Canvas, scale 0.01) with `BillboardLabel` MonoBehaviour + `TextMeshProUGUI` grandchild. `BillboardLabel` faces `CameraService.Instance.ActiveCamera` in `LateUpdate` — uses the match camera during gameplay, `Camera.main` fallback otherwise.
- **Camera.main**: `MatchCameraRig.BuildMainCamera()` tags the camera as `MainCamera`. During match, `CameraService.ActiveCamera` returns the match camera directly (no Camera.main lookup needed).
- **Old PlayerMaterialMap (deprecated)**: Still exists in codebase but no longer wired into DI. Color tinting was dropped in favor of character-specific prefabs.

## Match Reconnection Pattern
- Server: `MatchContext` tracks players as connected/disconnected (not removed on disconnect)
- Server: `OnDisconnected` → `MarkDisconnected` + broadcast `OnPlayerLeft(Disconnect)`, keep in player list
- Server: `JoinAsync` checks `IsPlayerInMatch` → reconnect (MarkConnected) vs new join (AddPlayer)
- Server: `CheckActiveMatchAsync` on `IMatchmakingService` — looks up active match by player ID, issues fresh token
- Server: If GS instance is gone, marks orphaned match as "Ended" and returns no active match
- Client: `LobbyEntryPoint` checks for active match on startup → skip lobby, go straight to match
- Client: `GameFlowController.EnterMatchFromLobby()` — lobby → match (bypasses matchmaking)
