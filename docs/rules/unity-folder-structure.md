# Unity Folder Structure

**When this applies:** Every file added under `client/ClashUp.Unity/Assets/`.

## The rule

Everything in `Assets/` lives under a **domain folder**. A domain folder may contain a `Scripts/` subfolder (for code + its asmdef) and a `Content/` subfolder (for non-code assets, each asset type in its own sub-subfolder).

```
Assets/
  <Domain>/
    Scripts/
      ClashUp.<Domain>.asmdef
      <code .cs files>
    Content/
      Prefabs/
      Scenes/
      Textures/
      Materials/
      Audio/
      Fonts/
```

No loose scripts at `Assets/` root. No mixing asset types inside a flat `Content/`. Prefabs and Scenes always live under `Content/`, never directly under the domain folder.

**Scenes and prefabs go under Content:** LifetimeScope prefabs live in `Content/Prefabs/`, scene files live in `Content/Scenes/`. A scene references its LifetimeScope via a prefab instance — do not put the MonoBehaviour directly in the scene.

## Good vs bad

**Good:**

```
Assets/Match/Scripts/MatchController.cs
Assets/Match/Scripts/ClashUp.Match.asmdef
Assets/Match/Content/Prefabs/MatchHud.prefab
Assets/Match/Content/Scenes/Match.unity
Assets/UI/Content/Textures/HudBackground.png
```

**Bad:**

```
Assets/MatchController.cs                # loose script at root
Assets/Prefabs/MatchHud.prefab           # asset-type folder at root, not under a domain
Assets/Match/MatchHud.prefab             # asset directly in domain, not under Content/
Assets/Match/Content/MatchHud.prefab     # asset directly in Content/, not under Prefabs/
Assets/Match/Scenes/Match.unity          # Scenes/ directly under domain, not under Content/
Assets/Match/Prefabs/MatchScope.prefab   # Prefabs/ directly under domain, not under Content/
```

## Meta files

Unity `.meta` files for folders are placed as **siblings** of the folder, not inside it. For example, the meta for `Content/` is `Content.meta` in the parent directory:

```
Assets/AppStarter/
  Content.meta            <-- meta for the Content/ folder
  Content/
    Prefabs.meta          <-- meta for the Prefabs/ folder
    Prefabs/
      MyPrefab.prefab
      MyPrefab.prefab.meta
    Scenes.meta           <-- meta for the Scenes/ folder
    Scenes/
      MyScene.unity
      MyScene.unity.meta
```

Never place a folder meta inside the folder itself (e.g. `Content/Content.meta` is wrong).

## Asmdef placement

- Exactly one asmdef per domain.
- The asmdef lives in `Scripts/`, named `ClashUp.<Domain>.asmdef`.
- Asmdef references explicit. See [`naming-conventions.md`](naming-conventions.md).

## What stays outside the convention

The following folders are infrastructure, not domains, and sit at `Assets/` root:

- `Plugins/` — third-party native/managed plugins (including `AetherNet/`).
- `Packages/` — NuGetForUnity install dir (yes, the inner `Assets/Packages/` is real and **committed**).
- `StreamingAssets/` — Unity's runtime-accessible bundle dir, if used.
- `link.xml` — IL2CPP link directives. See [`il2cpp-aot.md`](il2cpp-aot.md).

## Asmdef hierarchy and references

Every domain asmdef follows a layered dependency graph. Reference only what you actually need.

```
ClashUp.Shared          (leaf — no asmdef refs, only precompiledReferences)
   ^
ClashUp.Core            (refs: Shared, UniTask, VContainer)
   ^
ClashUp.Networking      (refs: Core, Shared, MagicOnion/gRPC libs, UniTask, VContainer)
ClashUp.Gameplay        (refs: Core, Shared, UniTask, VContainer)
   ^
ClashUp.Match           (refs: Core, Networking, Gameplay, Shared, UniTask, VContainer)
   ^
ClashUp.CoreStarter     (refs: Core, Networking, Match, Shared, UniTask, VContainer)
ClashUp.AppStarter      (refs: Core, Networking, Shared, UniTask, VContainer)
ClashUp.UI              (refs: Core, AppStarter, Networking, UniTask, VContainer, ugui)
```

**Key rules:**

- **Shared** uses `overrideReferences: true` + `precompiledReferences` because it references NuGet DLLs directly (`MagicOnion.Abstractions.dll`, `MagicOnion.Shared.dll`, `MessagePack.dll`, `MessagePack.Annotations.dll`). It has `noEngineReferences: true` since it contains no Unity API calls.
- **Networking** is the only domain that references MagicOnion/gRPC asmdef assemblies (`MagicOnion.Client`, `MagicOnion.Shared`, `Grpc.Net.Client`, `Grpc.Core.Api`, `Cysharp.Net.Http.YetAnotherHttpHandler`). Other domains talk to the network through interfaces in Core, not by referencing Networking directly (except Starters and Match which wire things up).
- **Core** is the common dependency — abstractions, interfaces, session state. Keep it thin.
- **Gameplay** holds client-prediction / simulation code. It depends on Core and Shared but NOT Networking.
- **Match** orchestrates a match session — it pulls in Networking and Gameplay.
- **Starters** (AppStarter, CoreStarter) are composition roots for VContainer. They reference whatever they need to wire up.
- **UI** references AppStarter to access boot-time services and Core for shared interfaces.

**When adding a new asmdef:**

1. Place it at the right layer. If it only needs Core + Shared, don't reference Networking.
2. Every asmdef must include `UniTask` and `VContainer` if it uses async or DI (almost always).
3. Never add a reference "just in case" — circular or unnecessary refs slow compilation and break layering.
4. If you need NuGet DLLs directly, set `overrideReferences: true` and list them in `precompiledReferences`. Do NOT put DLL assembly names in `references` (that field is for asmdef-to-asmdef only).

## Adding a new domain

1. Pick a name. Use PascalCase, single word if possible.
2. Update [`README.md`](README.md) if it warrants a top-level mention.
3. Create `Assets/<Domain>/Scripts/ClashUp.<Domain>.asmdef` with the references you actually need (avoid blanket "all").
4. Create `Assets/<Domain>/Content/` only when you have a non-code asset for it.
