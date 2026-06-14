using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay
{
    public sealed class MatchCharactersHolder
    {
        public CharacterCatalog Catalog { get; private set; } = CharacterCatalog.FromConfig(CharactersConfig.Default);

        public void Initialize(CharactersConfig config)
        {
            Catalog = CharacterCatalog.FromConfig(config ?? CharactersConfig.Default);
        }
    }
}
