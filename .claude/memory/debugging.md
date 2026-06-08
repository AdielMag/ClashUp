# Debugging & Common Pitfalls

## Where to Check Errors — Always Ask First

Before pulling logs, confirm which context the error is in:
- **Unity Editor play mode** → `console-get-logs` MCP tool
- **Unity Device Simulator** (Window → General → Device Simulator, runs inside the editor) → also `console-get-logs`
- **Android Emulator** (separate emulator process running the APK) → `adb logcat` or user pastes the error

Never assume editor console has the relevant errors when the user says "simulator" or "emulator" — always ask if it's ambiguous.

## dotnet CLI on Windows/bash
- `dotnet` is NOT on PATH in the bash shell
- Always use: `"/c/Program Files/dotnet/dotnet.exe"`
- Don't try broad PATH searches — user will reject them. Just use the full path.

## Unity Local Package (com.clashup.shared) DLL Clash
- `src/Shared/ClashUp.Shared/` is imported by Unity via `file:` reference
- If `dotnet build` runs and outputs `bin/obj` inside that folder, Unity sees the compiled DLL and conflicts with the asmdef
- Fix: `Directory.Build.props` redirects output to `.artifacts/`. If stale `bin/obj` appear, delete them AND restart Unity (not just reimport)
- Empty `bin/obj` dirs can reappear even after deletion — `rmdir` them, don't just `rm -rf` (which leaves empty dirs)
- Unity caches package state in `Library/` — if error persists after cleanup, delete `Library/` for full reimport

## Bulk Namespace Renames
- After sed-replacing namespaces (e.g. `Foo.Bar.Baz` -> `Foo.Bar`), always check for **duplicate using lines**
- The old `using Foo.Bar.Baz;` and `using Foo.Bar;` both become `using Foo.Bar;` — two identical lines
- Run a dedup pass after every bulk rename: `grep -rn "duplicate pattern" --include="*.cs"`

## MagicOnion Hub Broadcast Methods
- Hub receiver methods (e.g. `OnSnapshot`) return `void` — they are fire-and-forget
- In MagicOnion v7, `group.All.OnSnapshot()` returns void, NOT a Task
- Do NOT `await` broadcast calls — use plain call and return `Task.CompletedTask` from the calling method

## NuGet Package Gotchas
- MagicOnion 7.10.1 does NOT exist — latest is 7.10.0. Always verify versions on nuget.org before upgrading.
- Server projects that call other servers as gRPC clients need BOTH `MagicOnion.Server` AND `MagicOnion.Client`
- `Grpc.Net.Client` must be explicitly referenced for `GrpcChannel` — it's NOT pulled in transitively by `Grpc.AspNetCore.Server`
- MessagePack v3 `MsgPack017` analyzer is strict about `init` properties with initializers — suppress in shared projects targeting netstandard2.1

## Unity ScriptableObject Assets
- `.asset` files CANNOT be created from CLI — they contain serialized GUIDs and binary references
- Must be created in Unity Editor via CreateAssetMenu
- When designing ScriptableObject-based config, flag upfront that the user needs to create the asset in Unity
- **Enum-keyed dictionaries in .asset files**: `SerializedDictionary<SomeEnum, T>` serializes enum values as integers (`key: 0`, `key: 1`). Inserting a new enum value in the middle shifts all subsequent keys — must update the `.asset` YAML in sync or values get mismatched.

## Precompiled DLL .meta File — Must Have Full PluginImporter Block

**Symptom**: `CS0246: The type or namespace name 'X' could not be found` for types in a DLL that is physically present in `Assets/Packages/` and listed in an asmdef's `precompiledReferences`.

**Root cause**: Unity auto-generates a minimal `.meta` file for a DLL it first sees (just `fileFormatVersion` + `guid`, no `PluginImporter`). Without the `PluginImporter` block, Unity does not treat the file as a plugin and the assembly is never loaded.

**Fix**: The `.meta` file for every precompiled DLL must have a full `PluginImporter` section:
```yaml
PluginImporter:
  externalObjects: {}
  serializedVersion: 3
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
    Any:
      enabled: 1
      settings: {}
    Editor:
      enabled: 0
      settings:
        DefaultValueInitialized: true
    WindowsStoreApps:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData:
  assetBundleName:
  assetBundleVariant:
```

**Prevention**: When committing a new DLL to the repo, always check its `.meta` has this block — not just a bare guid line.

## VContainer Interface Migration — Search All Consumers

When replacing a concrete type registration with an interface (e.g. `MovementClientSimulation` → `IClientSimulation`), search **all** files for the old concrete type, not just the DI scope file. Constructor-injected dependencies in other classes (e.g. `MatchSessionRunner`) will keep the old concrete type and cause a VContainer "No such registration" error at runtime.

**Fix pattern**:
```bash
grep -rn "ConcreteTypeName" --include="*.cs"
```
Update every hit that isn't the class definition itself.

## Unity asmdef References
- When a script uses types from a package (e.g. `SerializedDictionary` from editor-toolbox), the asmdef must reference that package's runtime asmdef
- Toolbox package runtime asmdef name: `Toolbox`
- Missing asmdef reference = `CS0246: type or namespace not found`

## Moving Unity Files
- Every file AND folder in Unity has a `.meta` file — they must move together to preserve GUIDs
- If `.meta` files are lost, Unity regenerates new GUIDs which breaks all references (prefab fields, scene refs, etc.)
- `git mv` fails on untracked files — use plain `mv` for newly created files, then `git add`
- After moving files, create `.meta` files for any new directories using `python tools/generate-guid.py`

## FindAnyObjectByType and Inactive Objects
- `FindAnyObjectByType<T>()` does NOT find inactive GameObjects (default behavior)
- If a MonoBehaviour calls `gameObject.SetActive(false)` in `Awake()`, it becomes unfindable
- **Fix**: Use `CanvasGroup.alpha = 0` + `blocksRaycasts = false` instead of `SetActive(false)` for objects that need to be found later

## VContainer RegisterEntryPoint and Concrete Type Injection
- `RegisterEntryPoint<T>()` only registers `T` as `IAsyncStartable` (or `IStartable`, etc.)
- If another class injects `T` by concrete type, VContainer throws "No such registration"
- **Fix**: Chain `.AsSelf()` — e.g. `builder.RegisterEntryPoint<GameFlowController>().AsSelf();`
- This registers as both `IAsyncStartable` AND `GameFlowController`

## Editor Scripts: Scene Creation
- `EditorSceneManager.NewScene(EmptyScene, Additive)` makes the new scene the active scene
- Any `new GameObject()` created after this is automatically placed in that scene
- `SceneManager.MoveGameObjectToScene()` is unnecessary and causes namespace ambiguity with `UnityEditor.SceneManagement`
- Always create one-time editor setup scripts (menu items) instead of leaving manual steps for the user

## Docker Dockerfiles — Must COPY All Root-Level MSBuild Props Files

When adding a new root-level MSBuild props file (e.g. `AetherNet.refs.props`), it must be added to the COPY line in **both** `ops/docker/services.Dockerfile` and `ops/docker/gameserver.Dockerfile`:
```dockerfile
COPY global.json Directory.Build.props Directory.Packages.props AetherNet.refs.props ./
```
Without this, the file doesn't exist in the Docker build context and `<Import Project="...">` fails with MSB4019.

## Docker + Directory.Build.props
- `Directory.Build.props` redirects output to `.artifacts/` using `$(MSBuildThisFileDirectory)` absolute paths
- Inside Docker, this creates paths like `/src/.artifacts/bin/...` which break `dotnet run` (but NOT `dotnet publish -o`)
- Fix: Condition the redirect with `Condition="'$(DOTNET_RUNNING_IN_CONTAINER)' != 'true'"` — official .NET Docker images set this env var
- **Docker layer caching**: Changing `Directory.Build.props` may NOT invalidate cached build layers if the COPY step content hash is unchanged from Docker's perspective. Use `docker compose build --no-cache` to force rebuild.
- `pull_policy: build` in docker-compose rebuilds images on `up`, but does NOT recreate containers from stale images — use `docker compose down` then `up`, or `up --force-recreate`

## JwtSecurityTokenHandler Claim Mapping
- Default `JwtSecurityTokenHandler` maps `sub` → `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`
- `FindFirst("sub")` returns null even though the JWT contains `sub`
- **Fix**: Set `MapInboundClaims = false` on the handler before calling `ValidateToken`
- This affects `MatchTokenValidator` — any code validating JWTs and reading claims by short name

## Docker Dual-Endpoint (PublicEndpoint vs InternalEndpoint)
- In Docker, services calls the GS via internal DNS (`gameserver:5101`), but clients connect via `localhost:5101`
- `PublicEndpoint` = what clients receive in MatchHandoff (e.g. `http://localhost:5101`)
- `InternalEndpoint` = what services uses for PrepareMatchAsync (e.g. `http://gameserver:5101`)
- Both are stored in `GameServerInstanceDoc` and `GsRegistration`
- Matchmaker uses `InternalEndpoint` for admin calls, `PublicEndpoint` for handoff
- docker-compose sets both: `GameServer__PublicEndpoint` and `GameServer__InternalEndpoint`
- `InternalEndpoint` defaults to `PublicEndpoint` if empty (non-Docker local dev)

## Stale GS Instance Records
- GS restarts generate new instance IDs, leaving orphaned "Healthy" records in MongoDB
- The matchmaker picks these dead instances and PrepareMatchAsync fails
- **Fix**: On registration, `DrainOthersByEndpointAsync` marks all other instances with the same InternalEndpoint as "Draining"
- Also: `CheckActiveMatchAsync` should end orphaned matches when the GS instance is gone

## MagicOnion Client Headers (gRPC Metadata)
- `MagicOnionClient.Create<T>(channel, ...)` — 2nd param is `IClientFilter[]`, NOT `CallOptions`
- `IClientFilter.RequestContext.CallOptions` is **read-only** — cannot set headers via filters
- **Fix**: Use `Grpc.Core.Interceptors.Interceptor` with `channel.Intercept(interceptor)`:
  ```csharp
  var invoker = channel.Intercept(new MyInterceptor());
  var client = MagicOnionClient.Create<T>(invoker);
  ```
- Override `AsyncUnaryCall` to inject headers via `ClientInterceptorContext`
- See `PlayerIdInterceptor` in `MatchmakingClient.cs`

## Match-End Frozen at 00:00 — Full Sequence of Fixes

**Symptom**: Client shows "Match in progress" with 00:00 frozen after match ends.

There are three distinct root causes, each fixed separately:

### 1. Late-join replay (client reconnects during 2s grace window)
- **Cause**: After the tick loop broadcasts `OnMatchEnded`, it waits 2s then cleans up. A client that joins DURING those 2s misses the broadcast.
- **Fix**: `MatchContext.IsEnded` + `EndResult` are set *before* broadcasting. `MatchHub.JoinAsync` checks `context.IsEnded` after adding client to group and calls `Client.OnMatchEnded(endResult)` directly.

### 2. DB race: lobby reconnects to a just-ended match
- **Cause**: `NotifyMatchEndedAsync` (GS → Services → MongoDB) was fire-and-forget launched *after* the 2s wait. Lobby's `CheckActiveMatchAsync` fires before MongoDB updates → sends client back into match.
- **Fix**: `MatchContext.OnMatchEndedEarly` fires `NotifyMatchEndedAsync` immediately at match end (before broadcast + 2s wait). This gives MongoDB 2+ seconds to update before any client finishes returning to lobby.
- Two callbacks on `MatchContext`: `OnMatchEndedEarly` (notify DB) and `OnMatchEnded` (remove from registry + dispose).

### 3. Match not in GS registry — client gets stuck
- **Cause**: If client returns to lobby after the 2s window (match fully cleaned up), GS throws Internal error. Client was stuck showing "Failed:...".
- **Fix A**: `MatchHub.JoinAsync` re-fires `ReportMatchEndedAsync` when match not found — ensures DB catches up even if the original notify was lost.
- **Fix B**: `MatchSessionRunner.StartAsync` calls `ReturnToLobbyAsync()` on any connect failure instead of showing a stuck error screen.

### 4. Near-end match guard (< 10s)
- **Cause**: After pause/reset, lobby reconnects to a match with < 10s remaining — useless join.
- **Fix**: `CheckActiveMatchAsync` checks `(UtcNow - MatchDoc.CreatedAt).TotalSeconds` vs `MatchDoc.DurationSeconds`. If remaining < 10s, marks match Ended and returns Queued.
- `MatchDoc.DurationSeconds` was added for this purpose; set by `Matchmaker` when creating the doc.

**Rule**: The client is dumb — it never synthesizes match-end on its own. The server must always deliver `OnMatchEnded`. `SessionResetHandler` handles the pause case separately (full boot reset on unpause).

## Prediction & Interpolation Debugging

### Rubber-banding / jitter on local player
- **Most likely**: reconciliation acking by tick instead of `SequenceId`. Tick clocks drift with latency, so the wrong inputs get dropped → over/under-replay. Always drop by `input.SequenceId <= ackedSeq`.
- **Check**: Log `(ackedSeq, pendingCount)` after each reconcile. Pending queue should stay small (≈ RTT / tickInterval). If it grows unbounded, the server isn't echoing the seq correctly.
- **Determinism**: Aether.Physics2D is float-based. x86 server vs ARM client can produce slightly different positions for the same inputs → small corrections each snapshot. This is expected and tolerable for now.

### Remote players stuttering
- **Interp buffer underrun**: If packets arrive late or are dropped, the render clock catches up to the newest sample and clamps. Increase `InterpolationDelayMs` (default: 2 × tick interval = ~66ms).
- **Interp clock too far behind**: After a long stall (tab switch, GC spike), the render clock snaps forward. This is correct — a brief visual pop is better than remote players being stuck in the past.
- **Wrong data path**: Remote DTOs must go to `RemotePlayerInterpolator`, NOT the physics world. If `AetherClientSimulation.ReconcileTo` still processes remote ids, they'll snap instead of interpolate.

### Local player visually stepping (not smooth)
- `RenderAlpha` not being set: Check that `LocalInputPublisher.Tick()` writes `_prediction.RenderAlpha` each frame.
- Alpha always 0 or 1: Tick interval might be zero (division guard). Confirm `Configure(tickRateHz)` was called.

## Unity Input System — Cross-Platform Pitfalls

### InputSystemUIInputModule vs StandaloneInputModule
- **DO NOT** swap `StandaloneInputModule` for `InputSystemUIInputModule` programmatically without confirming Unity Player Settings Active Input Handling = "Input System Package (New)"
- `InputSystemUIInputModule` requires an `InputActionAsset` wired up; without it clicks/touches silently fail
- The safe default: keep `StandaloneInputModule` for UI buttons (Back to Lobby, etc.) and use raw Input System polling (`Touchscreen.current`, `Mouse.current`) for custom controls like joysticks
- Player Settings → Active Input Handling should be **"Both"** for projects mixing legacy UI with new Input System

### Keyboard.current — Never #if UNITY_EDITOR
- `Keyboard.current` is part of `UnityEngine.InputSystem` and works in all builds — never guard it with `#if UNITY_EDITOR`
- Wrapping keyboard input in `#if UNITY_EDITOR` silently strips it from all builds; WASD won't work in standalone

### EventSystem created early in boot (EnvironmentPickerUI)
- `EnvironmentPickerUI` (`#if CLASHUP_DEV || UNITY_EDITOR`) creates an EventSystem with `StandaloneInputModule` and calls `DontDestroyOnLoad` early in boot
- Any later `EnsureEventSystem` check sees `EventSystem.current != null` and skips — the `StandaloneInputModule` EventSystem persists for the whole session
- This is fine; joystick should use raw polling (not UI events) so it doesn't depend on the module type

### Raw Input System polling for joystick
- Use `Touchscreen.current` + `Mouse.current` directly in `MonoBehaviour.Update()` — zero EventSystem dependency
- Check `wasPressedThisFrame` for drag start (not `isPressed`) to avoid activating when finger slides in from outside the zone
- `RectTransformUtility.RectangleContainsScreenPoint(zoneRect, screenPos, null)` works for Screen Space Overlay with null camera

## Unity RectTransform Anchor vs Canvas Coordinate Mismatch

- `ScreenPointToLocalPointInRectangle(canvas, screenPos, null, out pos)` returns coordinates in the canvas's **local space** — (0,0) = canvas center when pivot is (0.5, 0.5)
- If the child RectTransform has `anchorMin = anchorMax = Vector2.zero` (bottom-left), `anchoredPosition = (0,0)` = bottom-left, NOT canvas center
- **Mismatch symptom**: UI element appears at bottom-left regardless of where you tap (or flies off-screen)
- **Fix**: Set `anchorMin = anchorMax = new Vector2(0.5f, 0.5f)` so `anchoredPosition` coordinates match the canvas local space origin

## Unity Time.time Pauses on Focus Loss
- `Time.time` stops when Unity Editor loses focus (OnApplicationPause)
- For timers that should keep ticking (match countdown), use `DateTimeOffset.UtcNow` instead
- Pattern: store `_joinWallClock = DateTimeOffset.UtcNow` at start, compute elapsed as `(UtcNow - _joinWallClock).TotalSeconds`

## Unity Android Build — "latest installed SDK on the system is 0"

**Symptom**: Build fails with "Minimum SDK of AndroidApiLevel23 but the latest installed SDK on the system is 0" even though SDK platforms are installed.

**Root cause**: `C:\Windows\System32` was missing from Unity's process PATH. Unity's `AndroidSDKTools.ListTargetPlatforms()` shells out to external tools (and `powershell` for sdkmanager) that need System32 binaries like `findstr.exe`. Without System32 in PATH, platform detection returns 0.

**Secondary issue**: Platform directories also lacked `package.xml` files (had `android.jar` and `source.properties` but not `package.xml`). These can be created manually but require admin access to `C:\Program Files\Unity\...`.

**Diagnosis steps**:
1. Check `AndroidExternalToolsSettings.sdkRootPath` — is it set?
2. Check `EditorPrefs.GetBool("SdkUseEmbedded")` — should be true for Unity Hub installs
3. Check `System.Environment.GetEnvironmentVariable("PATH")` inside Unity — does it include `C:\Windows\System32`?
4. Use reflection: `AndroidSDKTools.CreateAndroidSDKTools(sdkRoot)` then `ListTargetPlatforms()` to test detection

**Runtime fix** (process-level, lost on restart):
```csharp
var path = System.Environment.GetEnvironmentVariable("PATH");
System.Environment.SetEnvironmentVariable("PATH", @"C:\Windows\System32;C:\Windows\System32\WindowsPowerShell\v1.0;C:\Windows;" + path);
```

**Permanent fix**: Add `C:\Windows\System32` back to the system PATH via admin PowerShell:
```powershell
$p = [Environment]::GetEnvironmentVariable("Path","Machine")
[Environment]::SetEnvironmentVariable("Path","C:\Windows\System32;C:\Windows\System32\WindowsPowerShell\v1.0;C:\Windows;$p","Machine")
```

**Key API**: `UnityEditor.Android.AndroidExternalToolsSettings` (public type); `UnityEditor.Android.AndroidSDKTools` (internal, use reflection).

## Unity 6 — Legacy UI.Text Fonts Broken

**Symptom**: `UnityEngine.UI.Text` components render invisible — no text shown anywhere.

**Root cause**: In Unity 6, `Resources.GetBuiltinResource<Font>("Arial.ttf")` **throws an exception** (not null). `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` returns null in some contexts. The `??` fallback chain crashes before reaching any working fallback.

**Fix**: Don't use legacy `UnityEngine.UI.Text` for code-generated UI. Use `TextMeshProUGUI` + `TMP_Dropdown` instead, with a proper prefab that serializes font references.

**Key details**:
- `LegacyRuntime.ttf` works in editor script-execute but may fail at runtime
- `Arial.ttf` throws `"Arial.ttf is no longer a valid built in font. Please use LegacyRuntime.ttf"` — this is an exception, NOT a null return
- `Font.CreateDynamicFontFromOSFont("Arial", size)` didn't render visibly either
- The reliable path: prefab with `TextMeshProUGUI` using `LiberationSans SDF` TMP_FontAsset (at `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`)
- When creating prefabs via editor scripts, `AssetDatabase.FindAssets("t:TMP_FontAsset")` may return the Fallback font first — use `TMP_Settings.defaultFontAsset` or load by explicit path

## Unity UI Mask + Alpha Gotcha

**Symptom**: Dropdown items (or any masked content) are invisible even though text, font, and structure are correct.

**Root cause**: `Mask` component requires an `Image` with alpha > 0 to define the clipping region. Setting `Image.color = Color.clear` (alpha 0) makes the mask clip everything to nothing. `showMaskGraphic = false` hides the mask's visual but the alpha still matters for clipping.

**Fix**: Set the Viewport `Image` color to `new Color(1, 1, 1, 1)` (or any alpha > 0), then use `showMaskGraphic = false` to hide it visually.

## MagicOnion IL2CPP / AOT — Source Generator Required

**Symptom**: App works in Unity Editor but fails on Android/iOS with: `Unable to find a client factory of type 'X'. If the application is running on IL2CPP or AOT, the runtime and MagicOnion do not support dynamic code generation. Please use pre-generated code with Source Generator instead`

**Root cause**: MagicOnion uses runtime code generation (reflection emit) for hub/service clients. IL2CPP strips this capability. The Source Generator DLL ships with the NuGet package but needs an explicit trigger.

**Fix**: Add a file in the Networking assembly with `[MagicOnionClientGeneration]` listing all hub/service interfaces:
```csharp
[MagicOnionClientGeneration(typeof(IPingHub), typeof(IMatchHub), typeof(IMatchmakingService), typeof(IAuthService))]
internal static partial class MagicOnionGeneratedClientInitializer { }
```

**Key details**:
- The attribute is **class-level** (not assembly-level) — `CS0592` error if placed on assembly
- The class must be `partial` — the Source Generator fills in the generated code
- The Source Generator DLL (`MagicOnion.Client.SourceGenerator.dll`) must have the `RoslynAnalyzer` label in its `.meta` file
- File: `Assets/Core/Networking/Scripts/MagicOnionGeneratedClientInitializer.cs`
- When adding new hubs/services, add their interface types to this attribute

## Android Emulator — Getting Unity Errors via adb

To pull Unity errors from a running emulator build in one shot:
```bash
"C:\Users\Adiel\AppData\Local\Android\Sdk\platform-tools\adb.exe" logcat -s Unity:E -d
```
`-s Unity:E` filters to Unity error tag only. `-d` dumps and exits (no tail).

## Android Emulator Networking

**Symptom**: App connects from Unity Editor but not from Android emulator.

**Root cause**: Android emulator has its own isolated network stack — it does NOT share the host machine's VPN (Tailscale) or localhost.

**Fix**: Use `adb reverse` to forward ports from emulator to host:
```bash
adb reverse tcp:5001 tcp:5001   # Services
adb reverse tcp:5101 tcp:5101   # GameServer
```
Then connect to `localhost:5001` / `localhost:5101` from the app (use the "Local" environment).

**Key details**:
- Must forward ALL server ports (Services AND GameServer) — forgetting GameServer causes match connect failures
- `adb reverse` must be re-run after each emulator restart
- `adb reverse --list` to verify active forwards
- `10.0.2.2` is the emulator's alias for the host, but `adb reverse` + localhost is cleaner
- Tailscale IP does NOT work from emulator for GameServer — even if Services is reachable via Tailscale, the MatchHandoff URL uses Docker's `PublicEndpoint` (`localhost:5101`) which the emulator can't reach
- Tailscale IP does NOT work from emulator (no Tailscale client running inside it)
- Android 9+ blocks cleartext HTTP by default, BUT Unity already generates `usesCleartextTraffic="true"` in the manifest

## Custom AndroidManifest.xml — Launcher Activity Pitfall

**Symptom**: APK installs but app doesn't appear in the app drawer. `adb shell monkey -p <pkg> -c LAUNCHER 1` says "No activities found".

**Root cause**: Adding a custom `Assets/Plugins/Android/AndroidManifest.xml` overrides Unity's generated manifest. If the custom manifest is missing the launcher `<activity>` entry, the app has no entry point.

**Rule**: Do NOT add a custom AndroidManifest.xml unless absolutely necessary. Unity's generated manifest already includes `usesCleartextTraffic="true"` and the launcher activity. If you must customize, start from Unity's full generated manifest (found in `Library/Bee/Android/Prj/IL2CPP/Gradle/`).

## Magenta Materials on Prefabs Created via MCP/Scripts

**Symptom**: All meshes in a prefab render magenta (pink).

**Root cause**: Prefab was created programmatically without assigning materials. MeshRenderer's `m_Materials` contains `{fileID: 0}` (null) for every slot.

**Fix**: After creating a prefab with MeshRenderers, assign materials. Use `script-execute` with `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`:
```csharp
var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
    r.sharedMaterial = myMaterial;
PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
PrefabUtility.UnloadPrefabContents(prefab);
```

**Prevention**: When creating prefabs with renderers, always create and assign materials in the same step. Use `assets-material-create` MCP tool + `script-execute` to set colors/properties.

## Standard Shader Stripped on Android Builds

**Symptom**: Objects created via `GameObject.CreatePrimitive()` appear magenta on Android but look fine in Editor.

**Root cause**: The `Standard` shader is not referenced by any material asset in the project — Unity's shader stripping removes it from the build. `CreatePrimitive()` assigns a default material using `Standard`, which is missing at runtime.

**Fix**: Add the Standard shader (fileID: 46) to `AlwaysIncludedShaders` in `ProjectSettings/GraphicsSettings.asset`:
```yaml
- {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
```

**Alternative**: Create explicit material assets referencing the shaders you need — this is more robust than relying on always-included shaders.

## Reconnect Loop — Match Fail → Lobby → Match → Infinite

**Symptom**: Client rapidly loops between Match and Lobby scenes (~500ms per cycle).

**Root cause**: Match connect fails → `ReturnToLobbyAsync()` → `LobbyEntryPoint.CheckActiveMatchAsync()` finds active match on server → `EnterMatchFromLobby()` → match connect fails again → infinite loop.

**Fix**: `LobbyEntryPoint` tracks consecutive reconnect failures via static counter. After 3 failures, stays in lobby. Counter resets on successful match connect (called from `MatchSessionRunner`).

**Key files**: `LobbyEntryPoint.cs` (static `_reconnectFailures` + `ResetReconnectFailures()`), `MatchSessionRunner.cs` (calls reset after successful join).

## PlayerPrefs Shared Between Editor and Builds on Same Machine

**Symptom**: Two clients on the same machine (Unity Editor + simulator/standalone build) can't enter the same match — matchmaker pairs the same player with itself.

**Root cause**: `PlayerPrefs` storage is shared between Editor and player builds on the same machine. `PlayerPrefsDeviceIdStore` stores `clashup.deviceId` once and reuses it — both instances get the same device ID → same player identity on the server → matchmaker creates a match with the same player in both slots.

**Secondary issue**: `MatchmakingQueue.TryDrain()` had no duplicate-player detection, so two tickets with the same `PlayerId` could fill a batch.

**Fix (client)**: Use `#if UNITY_EDITOR` to separate the PlayerPrefs key — editor uses `clashup.deviceId.editor`, builds use `clashup.deviceId`. File: `PlayerPrefsDeviceIdStore.cs`.

**Fix (server)**: `TryDrain()` uses a `HashSet<string>` to skip duplicate `PlayerId` entries in the same batch. File: `MatchmakingQueue.cs`.

**Side effect**: This also caused wrong player colors on reconnect — both clients had the same `ColorSlot` because they were the same player. `PlayerViewSystem` assigns colors via `PlayerSummary.ColorSlot`, which is set once on first join (`context.GetPlayers().Count`) and preserved on reconnect.

## PlayerViewSystem Color Race on Reconnect

**Symptom**: After reconnect (or late join), a player's capsule appears with the wrong color (always slot 0 / blue).

**Root cause**: `PlayerViewSystem.Tick()` (VContainer `ITickable`) runs every frame. When a client joins, it's added to the MagicOnion group and starts receiving snapshots *before* `JoinResult` returns. `ReconcileTo` adds players to `_sim.Players` from snapshot data. `Tick()` sees them, calls `SpawnCapsule()`, but `_colorSlots` is empty (not yet filled by `RegisterPlayer` from `JoinResult.Players`). The fallback `_colorSlots.TryGetValue(...) ? s : 0` gives slot 0. Color is baked into the material at spawn time and never updated.

**Fix**: `RegisterPlayer` now checks if a capsule already exists for that player and retroactively updates its material color. File: `PlayerViewSystem.cs`.

**Also**: Don't clear `_colorSlots` in `UnregisterPlayer` — the server keeps sending snapshots for disconnected players, and `Tick()` will respawn the capsule from sim data. Without the color slot, it gets the wrong color.

## Emulator GameServer Connection Refused via Tailscale

**Symptom**: Emulator connects to Services (matchmaking, login) via Tailscale IP but fails with "Connection refused (os error 111)" when trying to join a match on the GameServer.

**Root cause**: The Docker `GameServer__PublicEndpoint` is `http://localhost:5101`. This URL is embedded in the `MatchHandoff` that Services returns to the client. The editor on the host machine can reach `localhost:5101` (Docker port mapping), but the emulator's `localhost` is itself — not the host. So the emulator can reach Services via Tailscale but not the GameServer because the handoff URL says `localhost`.

**Fix**: Use `adb reverse tcp:5101 tcp:5101` (in addition to `tcp:5001`) and switch the emulator to the **Local** environment. Both ports then route through `adb reverse` to the host's Docker containers.

**Key insight**: Tailscale only works for the initial Services connection (client chooses the URL). The GameServer URL comes from the server's `PublicEndpoint` config, which is always `localhost:5101` in Docker. So Tailscale environment + Docker = broken GameServer connection for emulators.

## SessionResetHandler — Skip Reset on Boot Scene

**Symptom**: `SessionResetHandler` shows the reset popup even when the app is already on the AppStarter scene (nothing to reset).

**Fix**: Check `SceneManager.sceneCount == 1 && GetSceneAt(0).buildIndex == 0` before showing the popup. If only the boot scene is loaded, skip the reset. File: `SessionResetHandler.cs`.

## Docker Volume Persistence

**Symptom**: Changed MongoDB config but the change didn't take effect after container restart.

**Root cause**: Docker volumes (`clashup-mongo-data`) persist data across container restarts. `docker compose restart` or `up` reuses the existing volume.

**Fix**: `docker compose down -v` removes volumes, then `docker compose up -d` starts fresh. Warning: this deletes all stored data.

## Server JWT Configuration
- `JwtKeyProvider` requires `Jwt:EndUserSigningKey` and `Jwt:InterTierSigningKey` (min 32 bytes each)
- Dev keys are in `appsettings.Development.json` for both Services and GameServer
- Docker-compose provides them via `Jwt__EndUserSigningKey` env vars (ASP.NET `__` separator convention)
- If server crashes with "Jwt:EndUserSigningKey is not configured", check ASPNETCORE_ENVIRONMENT=Development is set
