using ClashUp.Client.CoreStarter;
using ClashUp.Client.Gameplay;
using ClashUp.Client.Networking;

using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.Match
{
    public sealed class MatchLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var flow = Parent.Container.Resolve<GameFlowController>();
            builder.RegisterInstance(new MatchHandoffHolder { Value = flow.PendingHandoff });

            builder.Register<MatchHubReceiver>(Lifetime.Singleton);
            builder.Register<MatchSession>(Lifetime.Singleton);

            builder.Register<MatchInputGate>(Lifetime.Singleton);

            builder.Register<AetherClientSimulation>(Lifetime.Singleton);
            builder.Register<IClientSimulation>(
                c => c.Resolve<AetherClientSimulation>(), Lifetime.Singleton);
            builder.Register<ClientPredictionWorld>(Lifetime.Singleton);

            builder.RegisterEntryPoint<JoystickInputProvider>().As<IMovementInput>();
            builder.RegisterEntryPoint<PlayerSpawner>();
            builder.RegisterEntryPoint<PlayerViewSystem>().AsSelf();
            builder.RegisterEntryPoint<MatchCameraRig>();
            builder.RegisterEntryPoint<LocalInputPublisher>().AsSelf();

            builder.RegisterEntryPoint<MatchSessionRunner>();
        }
    }
}
