---
name: feedback-no-singletons
description: User rejects singleton pattern — use DI-registered services instead
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 53d337e4-9754-428d-a73c-ce1d6a8bab4d
---

Do not use the singleton pattern for server services. Use ASP.NET Core DI with interface registration instead.

**Why:** User explicitly rejected `PlayerSessionCache` as a singleton. Prefers proper DI with `services.AddSingleton<IService, Implementation>()` (DI-managed lifetime) over static singleton classes.

**How to apply:** When designing server-side services that need long-lived state (caches, registries), register via DI with appropriate lifetime — never use `static Instance` pattern. On the Unity client side, VContainer handles DI similarly.
