# IL2CPP / AOT

**When this applies:** Unity client code (anything under `client/ClashUp.Unity/Assets/`) and any code in `src/Shared/ClashUp.Shared/` that ends up compiled into Unity.

## The rule

Release builds of the Unity client ship as IL2CPP. Anything that relies on runtime IL emission is banned in Unity-bound assemblies.

## Forbidden

- `System.Reflection.Emit.*`
- `System.Linq.Expressions.Expression.Compile()` (silently falls back to interpreted mode that fails on some platforms)
- `DynamicMethod`
- `Activator.CreateInstance(string typeName)` — type-by-name resolution drops dead under IL2CPP stripping
- `Type.GetType("...")` for stripping-sensitive types — declare a typed reference instead

## Source generators

MessagePack and MagicOnion **must** be wired to Unity's Roslyn analyzer pipeline so they generate formatters / hub proxies at build time instead of needing reflection at runtime:

- The `com.cysharp.magiconion.client.unity` UPM package ships the analyzers; verify they're enabled in Player Settings → Roslyn Analyzers.
- After a fresh import, confirm generated files appear (Library/Bee/artifacts/...).
- If a `MessagePackSerializationException` shows up at runtime for a known DTO, it's almost always a missing/disabled generator.

## link.xml

`Assets/link.xml` is maintained by us. Any namespace that's reflected on (configuration binding, MessagePack formatter discovery, IoC) needs an entry to prevent IL2CPP stripping.

When you add:

- A new MessagePack-serializable type in a new namespace → add a `<assembly>` entry preserving that namespace.
- A new VContainer-resolved interface that's looked up by string (rare) → preserve it.

## Generics on value types

Generic virtual methods on value types are a code-size landmine under IL2CPP (every instantiation gets a separate native function). Prefer concrete generic methods or interface dispatch.

## Verification

- Build once with the `Development Build` + IL2CPP scripting backend before merging any change that touches Shared or Networking.
- Watch for "MissingMethodException" / "Could not produce class for type ..." in the Player log — those are the signature of a stripping or generator gap.
