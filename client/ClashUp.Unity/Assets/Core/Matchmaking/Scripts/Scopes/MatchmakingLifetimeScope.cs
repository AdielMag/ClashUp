using VContainer;
using VContainer.Unity;

namespace ClashUp.Client.Matchmaking
{
    public class MatchmakingLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<MatchmakingEntryPoint>();
        }
    }
}
