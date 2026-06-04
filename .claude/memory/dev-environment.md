---
name: dev-environment
description: "Dev environment setup — CLASHUP_DEV define, Tailscale for phone testing, ServerEnvironment enum"
metadata: 
  node_type: memory
  type: project
  originSessionId: a0e57f9c-98d8-4ff9-ab33-4feb69afdb2e
---

## CLASHUP_DEV Scripting Define

Environment picker and related dev-only code is gated by `#if CLASHUP_DEV || UNITY_EDITOR` (not `DEVELOPMENT_BUILD`).

**Why:** `DEVELOPMENT_BUILD` only activates when the "Development Build" checkbox is ticked in Build Settings. The user needs the env picker on Release builds too (e.g. Android APKs for phone testing). `CLASHUP_DEV` is a custom scripting define added to Player Settings for Standalone, Android, and iOS.

**How to apply:** When adding dev-only code, use `CLASHUP_DEV` not `DEVELOPMENT_BUILD`. To ship without dev features, remove `CLASHUP_DEV` from Player Settings > Scripting Define Symbols.

## Tailscale for Phone→Local Server Testing

User has Tailscale installed on PC (IP: `100.68.118.109`) and phone. The `ServerEnvironment` enum has three values:

| Enum | URL | Use case |
|------|-----|----------|
| Local | `http://localhost:5001` | Editor / same-machine testing |
| Tailscale | `http://100.68.118.109:5001` | Phone builds connecting to local server |
| Dev | `https://dev.clashup.example.com` | Remote dev server |

**How to apply:** When the user mentions phone testing or mobile builds connecting locally, the Tailscale environment is already set up. The server must bind to `0.0.0.0:5001` (not just localhost) for Tailscale connections to work.

## Android Emulator Testing

Emulators do NOT share the host's Tailscale VPN. Use `adb reverse` instead:

```bash
adb reverse tcp:5001 tcp:5001   # Services
adb reverse tcp:5101 tcp:5101   # GameServer
```

Then use the **Local** environment (`localhost:5001`). Must re-run after each emulator restart.

**Warning**: The Tailscale environment does NOT work for emulators even if Services is reachable — the `MatchHandoff` URL uses Docker's `PublicEndpoint` (`localhost:5101`), which the emulator can't reach over Tailscale. Always use `adb reverse` + Local for emulators.

**adb path**: `C:\Users\Adiel\AppData\Local\Android\Sdk\platform-tools\adb.exe`  
**Package name**: `com.DefaultCompany.ClashUp.Unity`

## Android Build Gotchas

- **MagicOnion Source Generator**: Required for IL2CPP builds — `[MagicOnionClientGeneration]` attribute in Networking assembly. See [[debugging]].
- **Shader stripping**: `CreatePrimitive()` uses Standard shader which gets stripped. Add `fileID: 46` to `AlwaysIncludedShaders`.
- **Custom AndroidManifest.xml**: Do NOT add one unless necessary — it can break the launcher activity. Unity already generates `usesCleartextTraffic="true"`.

Related: [[debugging]], [[patterns]]
