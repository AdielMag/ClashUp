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

Coverage of the **logic** systems is ~70–100% per class. Intentionally NOT unit-tested (need live infra): MagicOnion hubs (MatchHub, PingHub), Mongo repositories, background/hosted services (MatchTickLoop, Heartbeat, GracefulDrain, CcuMetricReporter, HostMetricsReporter, Matchmaker), the Docker ProcessSupervisor, GCP glue, MatchRegistry/MatchContext DI orchestration, and Program.cs bootstrap. Note: sim classes use `[..6]` id slices in debug logs, so tests that drive ability/projectile hits must use player ids >= 6 chars (see [[feedback-client-authority]]).
