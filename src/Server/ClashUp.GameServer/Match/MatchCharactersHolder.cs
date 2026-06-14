namespace ClashUp.Server.GameServer.Match;

using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;

public sealed class MatchCharactersHolder
{
    public CharacterCatalog Catalog { get; private set; } = CharacterCatalog.FromConfig(CharactersConfig.Default);

    public void Initialize(CharactersConfig config)
    {
        Catalog = CharacterCatalog.FromConfig(config ?? CharactersConfig.Default);
    }
}
