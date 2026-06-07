using ClashUp.Client.CoreStarter;
using ClashUp.Client.Gameplay;
using ClashUp.Client.Networking;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.Match
{
    public sealed class MatchLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private PlayerMaterialMap _playerMaterialMap;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_playerPrefab);
            builder.RegisterInstance(_playerMaterialMap);
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
