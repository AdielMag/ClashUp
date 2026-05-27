using ClashUp.Client.Core;
using ClashUp.Client.Networking;
using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.AppStarter;

/// <summary>
/// Root LifetimeScope for boot-time / app-lifetime services. Standalone:
/// not parented from CoreStarter. See docs/rules/vcontainer-scopes.md.
/// </summary>
public sealed class AppStarterLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(new ClashUpEndpoints());

        builder.Register<IDeviceIdStore, PlayerPrefsDeviceIdStore>(Lifetime.Singleton);
        builder.Register<MagicOnionChannelProvider>(Lifetime.Singleton);
        builder.Register<PingHubClient>(Lifetime.Singleton);

        builder.RegisterEntryPoint<BootBootstrapper>();
    }
}
