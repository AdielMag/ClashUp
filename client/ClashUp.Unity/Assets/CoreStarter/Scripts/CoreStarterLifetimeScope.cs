using ClashUp.Client.Core;
using ClashUp.Client.Networking;
using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.CoreStarter;

/// <summary>
/// Root LifetimeScope for the gameplay session. Standalone — NOT a
/// child of AppStarter. Data comes across via SessionHandoff. See
/// docs/rules/vcontainer-scopes.md.
///
/// Match scopes parent under this scope.
/// </summary>
public sealed class CoreStarterLifetimeScope : LifetimeScope
{
    /// <summary>Populated from AppStarter before the scope builds.</summary>
    public SessionHandoff SessionHandoff { get; set; }

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(SessionHandoff);

        // Re-register the shared channel infrastructure for the gameplay tier.
        // (Same singletons live in AppStarter for menu calls; gameplay gets its
        // own copies so the boot scope can be torn down independently.)
        builder.RegisterInstance(new ClashUpEndpoints());
        builder.Register<MagicOnionChannelProvider>(Lifetime.Singleton);
        builder.Register<SessionTokenStore>(Lifetime.Singleton);
        builder.Register<MatchmakingClient>(Lifetime.Singleton);
        builder.Register<ResolveMatchClient>(Lifetime.Singleton);
        builder.Register<GameServerChannelFactory>(Lifetime.Singleton);
    }
}
