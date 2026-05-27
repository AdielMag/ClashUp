using ClashUp.Client.Gameplay;
using ClashUp.Client.Match.Match.Scripts;
using ClashUp.Client.Networking;
using ClashUp.Client.Networking.Networking.Scripts;

using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.Match
{
    /// <summary>
    /// Child of CoreStarterLifetimeScope. Created on match join, disposed on
    /// match end. Owns the MatchSession, hub receiver, and the client-side
    /// prediction world.
    /// </summary>
    public sealed class MatchLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<MatchHubReceiver>(Lifetime.Singleton);
            builder.Register<MatchSession>(Lifetime.Singleton);

            // Swap NullClientSimulation for the AetherNet adapter once the
            // submodule lands under Plugins/AetherNet/.
            builder.Register<IClientSimulation, NullClientSimulation>(Lifetime.Singleton);
            builder.Register<ClientPredictionWorld>(Lifetime.Singleton);

            builder.Register<MatchHandoffHolder>(Lifetime.Singleton);
            builder.RegisterEntryPoint<MatchSessionRunner>();
        }
    }
}
