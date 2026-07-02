---
name: server-test-suite
description: "Where the server/shared unit tests live, how to run them, and what is covered vs intentionally skipped"
metadata: 
  node_type: memory
  type: project
  originSessionId: cc07d5b0-a60e-41af-aa0a-5f02600c2677
---

The consolidated server test project is `src/Server/ClashUp.GameServer.Tests` (xUnit + FluentAssertions + coverlet.collector). It references GameServer, Services, Server.Common, Gateway, and Shared, plus `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (for Gateway HttpContext tests). It mirrors the GameServer's `Maps/Data/*.json` and `Abilities/Data/ability_*.json` into its output via `<Content Include ... Link=...>` so the disk-backed `ServerMapStore`/`ServerAbilityStore` can be exercised.

Tests are organized in subfolders: `Shared/` (HealthTable, MovementModel, DeterministicRng, SpawnResolver, CharacterCatalog, MatchPhysicsWorld, MessagePack round-trips), `GameServer/` (InputBuffer, ActiveAbility, AbilityExecutor, Projectile/PointOrb/Movement/Aether sims, ServerMapStore/ServerAbilityStore, MatchCharactersHolder), `Common/` (JWT issuer/validator/keyprovider, GraceCcuCounter, ServerVersion), `Services/` (Match/Character ConfigProvider), `Gateway/` (VersionForwarder).

Run: `"/c/Program Files/dotnet/dotnet.exe" test src/Server/ClashUp.GameServer.Tests/ClashUp.GameServer.Tests.csproj`. Add `--collect:"XPlat Code Coverage"` for a Cobertura report.

Coverage of the **logic** systems is ~70–100% per class. Intentionally NOT unit-tested (need live infra): MagicOnion hubs (MatchHub, PingHub), Mongo repositories, background/hosted services (MatchTickLoop, Heartbeat, GracefulDrain, CcuMetricReporter, HostMetricsReporter, Matchmaker), the Docker ProcessSupervisor, GCP glue, MatchRegistry/MatchContext DI orchestration, and Program.cs bootstrap.

## `[..6]` id-slice crash — use player ids >= 6 chars

`AbilityExecutor.EvaluateHitbox` and other sim classes do `casterId[..6]`/`targetId[..6]` in debug `Console.WriteLine` log lines. Real player ids are GUIDs so this never fires in production, but ANY test that calls `Step()` while two players are within auto-attack/hitbox range and uses a short literal id (e.g. `"bot"`, `"ally"`, `"enemy"` — anything under 6 chars) throws `ArgumentOutOfRangeException` the moment a hit lands. **Always use player ids >= 6 characters** in tests that call `Step()` (e.g. `"botunit"`, `"enemyone"`, not `"bot"`, `"enemy"`). This is a real pre-existing latent bug (crashes on any real short id too) but out of scope to fix incidentally — just avoid tripping it in test fixtures. (Previously mis-linked to [[feedback-client-authority]], which is unrelated — corrected 2026-07-02.)

## Avoid simulating movement for position-dependent tests — place players via SpawnArea instead

If a test only needs players AT specific coordinates (not testing movement itself), do NOT drive them there with `ApplyInput` + `Step()` loops. Two solid dynamic player colliders are real physics bodies — if their walked paths cross (or they're walked toward a point another stationary player already occupies), they collide and shove each other off the intended coordinates, silently corrupting the test (observed: two players "walked" toward opposite zones ended up shoved together near the midpoint instead, ~10 units off target, with no error — just a wrong-looking result that took several iterations to diagnose).

**Fix**: for any test whose assertions are pure reads of position/team/zone state (`CanSee`, `EncodeDeltaFor`, `TryGetBotView`, etc.), build a custom `MapData` with explicit `SpawnAreas` giving each player the exact coordinate the scenario needs, then call ONLY `EnsurePlayer` — **never call `Step()`**. `EnsurePlayer` sets position immediately via `_world.EnsurePlayer(...)`; with no `Step()` call there is no physics resolution pass, so hand-placed coordinates stay exactly as configured. Only reach for real `ApplyInput`+`Step()` movement when the test is specifically about movement/physics (like the existing `ApplyInput_MovesThePlayer` test). See [[grass-stealth]] `GrassStealthTests.cs` for the pattern (`TeamAt(team, x)` helper building single-slot `SpawnArea`s).
