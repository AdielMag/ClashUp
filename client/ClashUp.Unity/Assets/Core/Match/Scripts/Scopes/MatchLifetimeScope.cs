using ClashUp.Client.CoreStarter;
using ClashUp.Client.Gameplay;
using ClashUp.Client.Networking;

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
            var flow = Parent.Container.Resolve<GameFlowController>();
            builder.RegisterInstance(new MatchHandoffHolder { Value = flow.PendingHandoff });

            builder.Register<MatchHubReceiver>(Lifetime.Singleton);
            builder.Register<MatchSession>(Lifetime.Singleton);

            builder.Register<MatchInputGate>(Lifetime.Singleton);

            builder.Register<IClientSimulation, NullClientSimulation>(Lifetime.Singleton);
            builder.Register<ClientPredictionWorld>(Lifetime.Singleton);

            builder.RegisterEntryPoint<JoystickInputProvider>().As<IMovementInput>();
            builder.RegisterEntryPoint<PlayerSpawner>().AsSelf();
            builder.RegisterEntryPoint<MatchCameraRig>();

            builder.RegisterEntryPoint<MatchSessionRunner>();
        }
    }
}
