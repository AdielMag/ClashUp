# Async Discipline

**When this applies:** Every method whose body contains `await` or whose return type is async.

## The rule

- **Server code** returns `Task` or `ValueTask`. Use `ValueTask` for hot paths where the result is often available synchronously.
- **Unity client code** returns `UniTask` (or `UniTask<T>`).
- **Shared MagicOnion interfaces** in `src/Shared/ClashUp.Shared/` use `Task<T>`. MagicOnion's Unity client source generator emits a UniTask-returning proxy from the same `Task<T>` interface (with `MagicOnion.Client.Unity` configured for UniTask integration). Server implementations return `Task<T>` directly. Do not declare client-only and server-only variants of the same interface.
- **Non-MagicOnion Unity-only async APIs** (player input, scene loading, animations) return `UniTask` directly — they never cross the wire.

## Forbidden

- `.Result`, `.Wait()`, `GetAwaiter().GetResult()` — deadlock risk, defeats async. The few legitimate uses (sync `Main`, `IDisposable.Dispose` chains) need an inline comment justifying it.
- Fire-and-forget `Task` returned from a method without explicit `.Forget()` (Unity) or assignment to `_ =` (server). Silent unobserved tasks swallow exceptions.
- `async void` outside of event handlers (and even there, prefer `UniTaskVoid` / `void` with explicit error handling).

## CancellationToken

- Every long-running or external-IO method accepts a `CancellationToken`.
- Background services link to the host's `ApplicationStopping` token: `using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, ...)`.
- The match tick loop links to the `MatchContext`'s cancellation token; disposing the match scope cancels the loop.
- `CancellationToken` is the **last** parameter, by convention.

## Unity main thread

- MagicOnion + UniTask integration delivers continuations on Unity's main thread by default. Don't add `await UniTask.SwitchToMainThread()` unless you've explicitly switched off it (e.g. via `UniTask.RunOnThreadPool`).
- Updating any `UnityEngine` API (Transforms, Renderers, UI) must happen on the main thread. If it's not obviously on the main thread, switch first.

## Server thread model

- ASP.NET Core + MagicOnion already use the thread pool effectively. Don't add `Task.Run` around hub or service methods.
- The match tick loop runs on a dedicated long-running task (`Task.Factory.StartNew(..., TaskCreationOptions.LongRunning)`). It is the **only** place we intentionally take a thread off the pool. See [`magiconion-hub-discipline.md`](magiconion-hub-discipline.md).
