using ClashUp.Client.CoreStarter;
using ClashUp.Client.Gameplay;
using ClashUp.Client.Networking;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.Match
{
    public sealed class MatchLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private CharacterPrefabMap _characterPrefabMap;
        [SerializeField] private MapRegistry _mapRegistry;
        [SerializeField] private CinemachineCamera _virtualCamera;
        [SerializeField] private AbilityVisualRegistry _abilityVisualRegistry;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_playerPrefab);
            builder.RegisterInstance(_characterPrefabMap);
            builder.RegisterInstance(_mapRegistry);
            builder.RegisterInstance(_virtualCamera);
            if (_abilityVisualRegistry != null)
                builder.RegisterInstance(_abilityVisualRegistry);
            var flow = Parent.Container.Resolve<GameFlowController>();
            builder.RegisterInstance(new MatchHandoffHolder { Value = flow.PendingHandoff });
            builder.Register<MatchCharactersHolder>(Lifetime.Singleton);
            builder.Register<MatchAbilitiesHolder>(Lifetime.Singleton);

            builder.Register<MatchHubReceiver>(Lifetime.Singleton);
            builder.Register<MatchSession>(Lifetime.Singleton);

            builder.Register<MatchInputGate>(Lifetime.Singleton);

            builder.Register<AetherClientSimulation>(Lifetime.Singleton);
            builder.Register<IClientSimulation>(
                c => c.Resolve<AetherClientSimulation>(), Lifetime.Singleton);
            builder.Register<RemotePlayerInterpolator>(Lifetime.Singleton);
            builder.Register<ClientPredictionWorld>(Lifetime.Singleton);

            builder.RegisterEntryPoint<JoystickInputProvider>().AsSelf().As<IMovementInput>();
            builder.RegisterEntryPoint<AbilityInputProvider>().AsSelf().As<IAbilityInput>();
            builder.RegisterEntryPoint<PlayerSpawner>();
            builder.RegisterEntryPoint<LocalInputPublisher>().AsSelf();
            builder.RegisterEntryPoint<PlayerViewSystem>().AsSelf();
            builder.RegisterEntryPoint<MatchCameraRig>();

            builder.RegisterEntryPoint<MatchSessionRunner>();
            builder.RegisterEntryPoint<RespawnScreenController>();

            if (_abilityVisualRegistry != null)
            {
                builder.RegisterEntryPoint<AbilityVisualHandler>();
                builder.RegisterEntryPoint<TelegraphController>();
            }
        }
    }
}
