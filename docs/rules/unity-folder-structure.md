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

No loose scripts at `Assets/` root. No mixing asset types inside a flat `Content/`.

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
```

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

## Adding a new domain

1. Pick a name. Use PascalCase, single word if possible.
2. Update [`README.md`](README.md) if it warrants a top-level mention.
3. Create `Assets/<Domain>/Scripts/ClashUp.<Domain>.asmdef` with the references you actually need (avoid blanket "all").
4. Create `Assets/<Domain>/Content/` only when you have a non-code asset for it.
