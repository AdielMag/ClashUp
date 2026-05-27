# Shared Contracts

**When this applies:** Adding or changing any type that crosses the client/server boundary — MagicOnion service or hub interfaces, DTOs, wire formats, enums sent over the wire.

## The rule

`src/Shared/ClashUp.Shared/` is the **only** place cross-tier types live. The same source is compiled into the server (`ClashUp.Shared.csproj`) and into Unity (via the local UPM package reference in `client/ClashUp.Unity/Packages/manifest.json`).

Never duplicate a hub interface, DTO, or wire type on the client or server side. If you find yourself wanting to, the type belongs in `Shared` instead.

## Wire format

- All DTOs that cross the wire are MessagePack-serializable: `[MessagePackObject]` on the class, with `[Key]` on each member (or `[MessagePackObject(true)]` keyed by name for low-churn types).
- Numeric components on AetherNet wire types are quantized to fixed-point integers at the Shared boundary. The wire format must be deterministic across Unity client and server.
- Use `MessagePack` containers only — no `BinaryFormatter`, no `System.Text.Json` over the wire.

## Folder layout inside Shared

```
src/Shared/ClashUp.Shared/
  Hubs/                  # IMatchHub, IMatchHubReceiver
  Services/              # IAuthService, IMatchmakingService, ...
  MessagePackObjects/    # cross-tier DTOs
  AetherNet/             # wire types for inputs/snapshots
```

Namespaces follow the folders: `ClashUp.Shared.Hubs`, `ClashUp.Shared.Services`, etc. See [`naming-conventions.md`](naming-conventions.md).

## Unity-specific code in Shared

Avoid `#if UNITY_*` in Shared. If absolutely required:

- Isolate it behind a `partial class` or a small shim type, not sprinkled through business logic.
- Call it out in the PR.

If a piece of code is fundamentally client-only or server-only, it does not belong in Shared.

## Workflow

When adding a new RPC, hub method, or wire DTO:

1. Add the interface / type in `Shared/` first.
2. Implement the server side.
3. Implement / consume on the client side.

Reversing this order tends to produce duplicated types — push back in review if you see it.
