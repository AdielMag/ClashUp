using ClashUp.Client.Core;
using ClashUp.Client.Networking;

using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.AppStarter
{
    public sealed class AppStarterLifetimeScope : LifetimeScope
    {
        [SerializeField] private EnvironmentConfig environmentConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(environmentConfig);
            builder.RegisterInstance(new ClashUpEndpoints(environmentConfig));

            builder.Register<IDebugLogger, UnityDebugLogger>(Lifetime.Singleton);
            builder.Register<IDeviceIdStore, PlayerPrefsDeviceIdStore>(Lifetime.Singleton);
            builder.Register<ISceneLoader, UniTaskSceneLoader>(Lifetime.Singleton);
            builder.Register<MagicOnionChannelProvider>(Lifetime.Singleton);
            builder.Register<PingHubClient>(Lifetime.Singleton);

            builder.RegisterEntryPoint<BootBootstrapper>();
        }
    }
}
