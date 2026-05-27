using ClashUp.Client.Networking;
using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.Match;

/// <summary>
/// Child of CoreStarterLifetimeScope. Created on match join, disposed on
/// match end. Owns the MatchSession, hub receiver, and (later) the
/// client-side prediction world.
/// </summary>
public sealed class MatchLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<MatchHubReceiver>(Lifetime.Singleton);
        builder.Register<MatchSession>(Lifetime.Singleton);

        builder.RegisterEntryPoint<MatchSessionRunner>();
    }
}
