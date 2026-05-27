# ClashUp.Unity

The Unity 6 LTS client project. This folder is intentionally checked in **without** Unity-generated state (`Library/`, full `ProjectSettings/`, etc.). When you first open it:

1. Open `client/ClashUp.Unity/` in Unity Hub with Unity 6 LTS (see `ProjectSettings/ProjectVersion.txt` for the pinned version).
2. Let Unity regenerate `Library/`, `Logs/`, `Temp/`, and the full `ProjectSettings/` asset set. These directories are gitignored.
3. Open Window → NuGet (NuGetForUnity) and install the NuGet packages listed in [`docs/rules/il2cpp-aot.md`](../../docs/rules/il2cpp-aot.md): `MagicOnion.Client`, `MessagePack`, `Grpc.Net.Client`, etc. The install dir is `Assets/Packages/` and **is** committed.
4. Verify asmdef references resolve. If Unity complains about a missing assembly, install the matching NuGet/UPM package.

After import, the BootScene's `AppStarterLifetimeScope` will run on play and call `IPingHub.PingAsync` against `http://localhost:5001`. Start the Services host (`dotnet run --project src/Server/ClashUp.Services`) before pressing Play.

See [`docs/rules/`](../../docs/rules/) for project rules — most relevantly [`unity-folder-structure.md`](../../docs/rules/unity-folder-structure.md) and [`vcontainer-scopes.md`](../../docs/rules/vcontainer-scopes.md).
