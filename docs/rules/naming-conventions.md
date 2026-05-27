# Naming Conventions

**When this applies:** Project-wide. New namespaces, files, types, asmdefs.

## Namespaces

Pattern: `ClashUp.<Tier>.<Domain>[.<Subarea>]`.

**Client and Shared code must use block-scoped namespaces** (with braces). Unity's C# version is 9.0, which does not support file-scoped namespaces (`namespace Foo.Bar;`). Always write:

```csharp
namespace ClashUp.Client.Networking
{
    // ...
}
```

Server projects target modern .NET and may use either style.

| Tier      | Example namespace                       | Where it lives                              |
|-----------|-----------------------------------------|---------------------------------------------|
| Shared    | `ClashUp.Shared.Hubs`                   | `src/Shared/ClashUp.Shared/Hubs/`           |
| Server    | `ClashUp.Server.Common.Auth`            | `src/Server/ClashUp.Server.Common/Auth/`    |
| Server    | `ClashUp.Server.Services.Matchmaking`   | `src/Server/ClashUp.Services/Matchmaking/`  |
| Server    | `ClashUp.Server.GameServer.Match`       | `src/Server/ClashUp.GameServer/Match/`      |
| Client    | `ClashUp.Client.Networking`             | `client/ClashUp.Unity/Assets/Networking/`   |

One namespace per folder. File system path mirrors the namespace.

## Asmdefs (Unity)

- Name: `ClashUp.<Domain>` (e.g. `ClashUp.Networking`).
- File: `ClashUp.<Domain>.asmdef` in the domain's `Scripts/` folder. See [`unity-folder-structure.md`](unity-folder-structure.md).
- Root namespace in the asmdef matches the namespace pattern: `ClashUp.Client.<Domain>`.

## Files

- One public type per file. The file is named after the type (`MatchHub.cs`, not `Hub.cs`).
- Internal helpers may share a file when small and tightly coupled to the public type they support.
- Partial classes split across files: `MatchHub.cs` + `MatchHub.Reconnect.cs` (suffix describes the slice).

## Types

- Interfaces: `I<Name>` (`IMatchHub`, `IAccountRepository`).
- Async methods: `<Name>Async`.
- MagicOnion service interfaces: `I<Domain>Service` (e.g. `IAuthService`).
- MagicOnion hub interfaces: `I<Domain>Hub` (e.g. `IMatchHub`); their receiver counterparts: `I<Domain>HubReceiver`.
- Mongo document classes (the wire-to-storage type, NOT the domain type): suffix `Doc` (`AccountDoc`, `MatchDoc`).
- Options/configuration classes: suffix `Options` (`JwtOptions`, `MatchmakingOptions`).
- Background services: suffix `BackgroundService`.

## Members

- Public members: PascalCase.
- Private fields: `_camelCase` with leading underscore.
- Constants: `PascalCase` (not `SCREAMING_CASE`).
- Local variables and parameters: `camelCase`.

## Match / player IDs

- Use strongly-typed wrappers (`readonly struct PlayerId`, `readonly struct MatchId`) over `Guid`/`string` so type confusion is a compile error, not a runtime bug.
- Wrappers live in `Shared/` and are MessagePack-friendly.
