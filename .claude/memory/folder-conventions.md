# Client Folder & Script Conventions

## Domain Structure

Every domain (assembly) follows this layout:

```
Core/{DomainName}/
  Scripts/
    ClashUp.{DomainName}.asmdef
    Interfaces/        — Interfaces (IFoo.cs)
    Services/          — Implementations, providers, stores
    Clients/           — RPC/API client wrappers
    Models/            — Data classes, structs, DTOs
    Config/            — ScriptableObjects, config classes
    Scopes/            — VContainer LifetimeScope subclasses
    EntryPoints/       — Bootstrappers, runners (IAsyncStartable, etc.)
    Presenters/        — MonoBehaviour UI presenters
    UI/                — Code-generated UI (no prefab)
    Receivers/         — Hub receivers, event handlers
  Content/
    Scenes/
    Prefabs/
    ...
```

## Rules

1. **Scripts go in typed subfolders** — never loose at the Scripts/ root (except the .asmdef).
2. **Subdomains are full domains** — if a subdomain has its own Content/ (scenes, prefabs), it gets its own `Scripts/` folder with an asmdef. E.g., `Core/UI/` is a domain, not a subfolder of `Core/Scripts/`.
3. **Subfolders within Scripts/ are for grouping, not subdomains** — e.g., `Core/Scripts/SceneLoading/` is a topic grouping inside the Core assembly, not a separate domain.
4. **Topic subfolders repeat the typed structure** — `SceneLoading/Interfaces/`, `SceneLoading/Services/`, etc.
5. **Only create subfolders that have files** — don't create empty Clients/ if there are no clients.

## Domain vs. Subfolder Decision

| Has its own Content/? | Has 3+ scripts? | → Treatment |
|----------------------|-----------------|-------------|
| Yes | Any | Full domain (own asmdef) |
| No | Yes | Topic subfolder under parent Scripts/ |
| No | No | Just place in parent's typed subfolder |

## Examples

```
Core/
  Scripts/                          ← ClashUp.Core assembly
    ClashUp.Core.asmdef
    Interfaces/IDeviceIdStore.cs
    Models/SessionHandoff.cs
    SceneLoading/
      Interfaces/ISceneLoader.cs
      Models/SceneHandle.cs
      Services/UniTaskSceneLoader.cs

  Networking/Scripts/                ← ClashUp.Networking assembly
    ClashUp.Networking.asmdef
    Clients/PingHubClient.cs
    Services/MagicOnionChannelProvider.cs
    Config/EnvironmentConfig.cs

  UI/                               ← Full domain (has Content/)
    Scripts/                         ← ClashUp.UI assembly
      ClashUp.UI.asmdef
      Interfaces/ILoadingScreen.cs
      Presenters/LoadingScreenPresenter.cs
    Content/Scenes/PersistentUI.unity

_Bootstrap/AppStarter/Scripts/       ← ClashUp.AppStarter assembly
    Scopes/AppStarterLifetimeScope.cs
    EntryPoints/BootBootstrapper.cs
    Services/PlayerPrefsDeviceIdStore.cs
    UI/EnvironmentPickerUI.cs
```
