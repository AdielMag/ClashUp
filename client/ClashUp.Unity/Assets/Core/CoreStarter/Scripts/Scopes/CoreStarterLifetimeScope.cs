using ClashUp.Client.Core;
using ClashUp.Client.Networking;

using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.CoreStarter
{
    /// <summary>
    /// Root LifetimeScope for the gameplay session. Standalone — NOT a
    /// child of AppStarter. Registers all session services.
    /// Match and Lobby scopes parent under this scope.
    /// </summary>
    public sealed class CoreStarterLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IDebugLogger, UnityDebugLogger>(Lifetime.Singleton);

            builder.RegisterInstance(new ClashUpEndpoints());
            builder.Register<MagicOnionChannelProvider>(Lifetime.Singleton);
            builder.Register<SessionTokenStore>(Lifetime.Singleton);
            builder.Register<MatchmakingClient>(Lifetime.Singleton);
            builder.Register<ResolveMatchClient>(Lifetime.Singleton);
            builder.Register<GameServerChannelFactory>(Lifetime.Singleton);

            builder.Register<AuthClient>(Lifetime.Singleton);
            builder.Register<IDeviceIdStore, PlayerPrefsDeviceIdStore>(Lifetime.Singleton);
            builder.Register<ISceneLoader, UniTaskSceneLoader>(Lifetime.Singleton);

            builder.RegisterEntryPoint<GameFlowController>().AsSelf();
        }
    }
}
